import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';
import { LicenseType } from '@prisma/client';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import {
  upsertLicenseFromStripe,
  updateLicenseStatusBySubscriptionId,
  recordLicenseEvent,
  revokeLicense,
} from '@/lib/licensing';
import { prisma } from '@/lib/prisma';
import { logger } from '@/lib/logger';

// MED-5 FIX: Rate limit email sending (1 per 15 minutes per license).
// The window is 15 minutes (not 5) so it can actually dedupe the email a
// stale-claim TAKEOVER re-sends: a takeover only happens when the claim is at
// least STALE_CLAIM_MS (5 min) old, so a 5-minute window had always expired by
// the time the takeover reprocessed the event - the dedupe could never fire.
// Keep this comfortably larger than STALE_CLAIM_MS.
const EMAIL_RATE_LIMIT_MINUTES = 15;

// CRITICAL-2 FIX: Email format validation regex
// Validates email format before processing to prevent data integrity issues
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

type WebhookOutcome =
  | { kind: 'success' }
  | { kind: 'retry'; message: string }
  | { kind: 'permanentFailure'; message: string };

const SUCCESS: WebhookOutcome = { kind: 'success' };

function retry(message: string): WebhookOutcome {
  return { kind: 'retry', message };
}

function permanentFailure(message: string): WebhookOutcome {
  return { kind: 'permanentFailure', message };
}

async function canSendLicenseEmail(licenseId: string): Promise<boolean> {
  const recentEmail = await prisma.licenseEvent.findFirst({
    where: {
      licenseId,
      type: 'email_sent',
      createdAt: {
        gte: new Date(Date.now() - EMAIL_RATE_LIMIT_MINUTES * 60 * 1000),
      },
    },
    orderBy: { createdAt: 'desc' },
  });

  if (recentEmail) {
    logger.info('Email rate limited', { licenseId, lastSent: recentEmail.createdAt.toISOString() });
    return false;
  }
  return true;
}

// Configure route to receive raw body for Stripe signature verification
export const dynamic = 'force-dynamic';

// Give processing (Stripe API + DB + email) headroom so we aren't killed mid-flight
// by the default function timeout, stranding the idempotency claim.
export const maxDuration = 60;

// Idempotency claim state: WebhookEvent.processedAt is non-nullable in the schema,
// so we use the Unix epoch as a sentinel meaning "claimed but not yet processed".
// A real (recent) processedAt means processing completed successfully.
const UNPROCESSED_SENTINEL = new Date(0);

// If a claim is older than this and still unprocessed, the claiming invocation is
// presumed dead (Vercel timeout/OOM/deploy kill) and the claim can be taken over.
const STALE_CLAIM_MS = 5 * 60 * 1000;

function isProcessed(processedAt: Date): boolean {
  return processedAt.getTime() !== UNPROCESSED_SENTINEL.getTime();
}

// Lazy initialization of Stripe client to allow builds without env vars
function getStripeClient() {
  if (!process.env.STRIPE_SECRET_KEY || process.env.STRIPE_SECRET_KEY === 'sk_test_placeholder') {
    throw new Error('STRIPE_SECRET_KEY must be configured');
  }
  if (!process.env.STRIPE_WEBHOOK_SECRET || process.env.STRIPE_WEBHOOK_SECRET === 'whsec_placeholder') {
    throw new Error('STRIPE_WEBHOOK_SECRET must be configured');
  }
  return new Stripe(process.env.STRIPE_SECRET_KEY, {
    apiVersion: '2025-09-30.clover',
  });
}

export async function POST(request: NextRequest) {
  const stripe = getStripeClient();

  const body = await request.text();
  const signature = request.headers.get('stripe-signature');

  if (!signature) {
    return NextResponse.json({ error: 'Missing signature' }, { status: 400 });
  }

  let event: Stripe.Event;

  try {
    event = stripe.webhooks.constructEvent(
      body,
      signature,
      process.env.STRIPE_WEBHOOK_SECRET!
    );
  } catch (error) {
    logger.error('Webhook signature verification failed', error);
    return NextResponse.json({ error: 'Invalid signature' }, { status: 400 });
  }

  // HIGH-5 FIX: Atomic idempotency check using INSERT-or-exists pattern
  // The previous upsert approach had a race condition: two simultaneous requests
  // would both create with identical timestamps and both pass the timestamp check.
  // Now we use CREATE with unique constraint - only ONE request can succeed.
  //
  // STALE-CLAIM FIX: the claim is created with the UNPROCESSED_SENTINEL and only
  // stamped with a real processedAt AFTER processing succeeds. Previously the claim
  // was created already "processed", so a hard crash (timeout/OOM/deploy kill)
  // between claim and completion stranded the claim forever and every Stripe retry
  // short-circuited as cached — customer paid, no license. Now a claim that is
  // still unprocessed after STALE_CLAIM_MS can be taken over atomically by a retry.
  const now = new Date();

  try {
    await prisma.webhookEvent.create({
      data: {
        eventId: event.id,
        seenAt: now,
        processedAt: UNPROCESSED_SENTINEL,
      },
    });
    // INSERT succeeded - we're the first processor
  } catch (error: any) {
    // Check if this is a unique constraint violation (record already exists)
    if (error?.code === 'P2002') {
      const existing = await prisma.webhookEvent.findUnique({
        where: { eventId: event.id },
      });

      if (!existing || isProcessed(existing.processedAt)) {
        // Fully processed (or claim vanished between create and read) - genuine duplicate
        logger.info('Event already processed, skipping', { eventId: event.id });
        return NextResponse.json({ received: true, cached: true });
      }

      const staleCutoff = new Date(Date.now() - STALE_CLAIM_MS);
      if (existing.seenAt >= staleCutoff) {
        // Another invocation claimed this recently and is presumably still processing
        logger.info('Event claimed by a live invocation, skipping', { eventId: event.id });
        return NextResponse.json({ received: true, cached: true });
      }

      // Stale unprocessed claim from a crashed invocation - take it over ATOMICALLY.
      // The updateMany's where clause guarantees only one competing retry wins.
      const takeover = await prisma.webhookEvent.updateMany({
        where: {
          eventId: event.id,
          processedAt: UNPROCESSED_SENTINEL,
          seenAt: { lt: staleCutoff },
        },
        data: { seenAt: new Date() },
      });

      if (takeover.count !== 1) {
        logger.info('Lost stale-claim takeover race, skipping', { eventId: event.id });
        return NextResponse.json({ received: true, cached: true });
      }

      // We own the stale claim now - fall through and process. Worst case a crashed
      // invocation got partway through: reprocessing is tolerable because
      // upsertLicenseFromStripe is keyed on stripePaymentIntentId/subscriptionId
      // (no duplicate license; at most a duplicate email, rate-limited to 1/15min
      // by canSendLicenseEmail - the window deliberately exceeds STALE_CLAIM_MS).
      logger.warn('Took over stale webhook claim from crashed invocation', { eventId: event.id });
    } else {
      // Unexpected error - re-throw
      throw error;
    }
  }

  let outcome: WebhookOutcome;
  try {
    outcome = await processEvent(stripe, event);
  } catch (error) {
    // Fail safe for customer fulfillment: database, Stripe, email, and other
    // infrastructure exceptions are retriable unless a handler explicitly
    // classified the event payload itself as permanently invalid.
    logger.error('Webhook processing failure', error, { eventId: event.id });
    outcome = retry(error instanceof Error ? error.message : 'Unknown infrastructure failure');
  }

  if (outcome.kind === 'retry') {
    // The claim was created before processing. Release it on every retriable
    // outcome so Stripe's next delivery can actually re-run fulfillment.
    try {
      await prisma.webhookEvent.delete({ where: { eventId: event.id } });
    } catch (releaseError) {
      // If we can't release the claim, stale-claim takeover remains the recovery
      // backstop for a later delivery/reconciliation run.
      logger.error('Failed to release webhook idempotency claim after transient error', releaseError, { eventId: event.id });
    }
    return NextResponse.json({ error: outcome.message, eventId: event.id }, { status: 500 });
  }

  if (outcome.kind === 'permanentFailure') {
    logger.warn('Webhook event permanently rejected', {
      eventId: event.id,
      eventType: event.type,
      reason: outcome.message,
    });
  }

  // Processing succeeded - stamp the claim as processed so future retries are
  // recognized as genuine duplicates (and never eligible for stale takeover).
  //
  // STAMP-FAILURE FIX: a failed stamp must NOT bubble into a 500 - processing
  // already succeeded, and a 500 would make Stripe redeliver and fully reprocess
  // (duplicate email). Log loudly and still return 200. The claim then stays at
  // the unprocessed sentinel; the worst case is a redelivery >= STALE_CLAIM_MS
  // later taking it over, which is tolerable (license upsert is idempotent and
  // the email is deduped by canSendLicenseEmail's 15-minute window).
  try {
    await prisma.webhookEvent.update({
      where: { eventId: event.id },
      data: { processedAt: new Date() },
    });
  } catch (stampError) {
    logger.error('Failed to stamp webhook claim as processed AFTER successful processing - returning 200 anyway', stampError, { eventId: event.id });
  }

  return NextResponse.json({
    received: true,
    eventId: event.id,
    ...(outcome.kind === 'permanentFailure' ? { ignored: true } : {}),
  });
}

async function processEvent(stripe: Stripe, event: Stripe.Event): Promise<WebhookOutcome> {
  switch (event.type) {
    case 'checkout.session.completed':
      return handleCheckoutCompleted(stripe, event.data.object as Stripe.Checkout.Session, event.id);
    case 'customer.subscription.updated':
      return handleSubscriptionUpdated(event.data.object as Stripe.Subscription);
    case 'customer.subscription.deleted':
      return handleSubscriptionDeleted(event.data.object as Stripe.Subscription);
    case 'charge.refunded':
      return handleChargeRefunded(event.data.object as Stripe.Charge);
    default:
      logger.warn('Unhandled event type', { eventType: event.type });
      return permanentFailure(`Unsupported Stripe event type: ${event.type}`);
  }
}

async function handleCheckoutCompleted(
  stripe: Stripe,
  session: Stripe.Checkout.Session,
  eventId: string,
): Promise<WebhookOutcome> {
  logger.info('Webhook: checkout.session.completed', { sessionId: session.id });

  const email = session.customer_email || session.customer_details?.email;
  const stripeCustomerId = (session.customer as string) ?? '';

  logger.debug('Checkout details', { email, stripeCustomerId });

  if (!email || !stripeCustomerId) {
    logger.error('Missing customer email or ID');
    return permanentFailure('Missing customer email or ID on checkout session');
  }

  // CRITICAL-2 FIX: Validate email format before processing
  // Prevents data integrity issues from malformed emails
  if (!EMAIL_REGEX.test(email)) {
    logger.error('Invalid email format from Stripe', { email: email.substring(0, 50) });
    return permanentFailure('Invalid email format on checkout session');
  }

  const plan = session.metadata?.plan ?? (session.mode === 'subscription' ? 'quarterly' : 'lifetime');
  logger.debug('Plan details', { plan, mode: session.mode });

  if (plan === 'quarterly') {
    const subscriptionId = session.subscription as string | undefined;
    if (!subscriptionId) {
      return permanentFailure('Missing subscription id for quarterly plan');
    }

    const subscription: any = await stripe.subscriptions.retrieve(subscriptionId);

    const currentPeriodEnd = subscription.current_period_end
      ? new Date(subscription.current_period_end * 1000)
      : undefined;

    const license = await upsertLicenseFromStripe({
      email,
      type: LicenseType.SUBSCRIPTION,
      stripeCustomerId,
      stripeSubscriptionId: subscriptionId,
      subscriptionStatus: subscription.status,
      periodEndsAt: currentPeriodEnd,
    });

    logger.info('Sending license email', { email, licenseKey: license.licenseKey });

    // MED-5 FIX: Check rate limit before sending
    if (await canSendLicenseEmail(license.id)) {
      const emailResult = await sendLicenseEmail({
        email,
        licenseKey: license.licenseKey,
        licenseId: license.id,
        requestId: eventId,
      });

      if (emailResult.success) {
        logger.info('License email sent', { email, messageId: emailResult.messageId });
        await recordLicenseEvent(license.id, 'email_sent', {
          messageId: emailResult.messageId,
          email: email,
        });
      } else {
        logger.error('Failed to send license email', emailResult.error, { email });
        await recordLicenseEvent(license.id, 'email_failed', {
          error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
          email: email,
        });
        return retry('License email sending failed');
      }
    } else {
      // Log that email was skipped due to rate limit
      await recordLicenseEvent(license.id, 'email_skipped_rate_limit', {
        email: email,
        reason: `Rate limit: email already sent within ${EMAIL_RATE_LIMIT_MINUTES} minutes`,
      });
    }
  } else {
    logger.info('Processing lifetime payment');
    const paymentIntentId = typeof session.payment_intent === 'string'
      ? session.payment_intent
      : session.payment_intent?.id;

    logger.debug('Payment intent', { paymentIntentId });

    // PROMO FIX: a 100%-off promotion code (checkout enables allow_promotion_codes)
    // produces a zero-total payment-mode session that has NO PaymentIntent at all.
    // That used to throw a plain (permanent) Error - the customer got nothing,
    // silently, forever. Instead, key the license on session.id: it is unique and
    // stable across Stripe redeliveries, so the upsert keyed on
    // stripePaymentIntentId still dedupes correctly.
    if (!paymentIntentId && session.amount_total !== 0) {
      // No PaymentIntent on a PAID session is transient Stripe weirdness -
      // retry loudly instead of dropping the purchase permanently.
      logger.error(`Missing payment intent for paid lifetime plan (session ${session.id}, amount_total ${session.amount_total})`);
      return retry(`Missing payment intent for paid lifetime plan (session ${session.id})`);
    }

    const paymentRef = paymentIntentId ?? session.id;
    if (!paymentIntentId) {
      logger.info('Zero-total checkout session (100% promo) - keying license on session id', { sessionId: session.id });
    }

    logger.debug('Creating license in database');

    const license = await upsertLicenseFromStripe({
      email,
      type: LicenseType.LIFETIME,
      stripeCustomerId,
      stripePaymentIntentId: paymentRef,
    });
    logger.info('License created', { licenseKey: license.licenseKey, licenseId: license.id });

    logger.info('Sending license email', { email, licenseKey: license.licenseKey });

    // MED-5 FIX: Check rate limit before sending
    if (await canSendLicenseEmail(license.id)) {
      const emailResult = await sendLicenseEmail({
        email,
        licenseKey: license.licenseKey,
        licenseId: license.id,
        requestId: eventId,
      });

      if (emailResult.success) {
        logger.info('License email sent', { email, messageId: emailResult.messageId });
        await recordLicenseEvent(license.id, 'email_sent', {
          messageId: emailResult.messageId,
          email: email,
        });
      } else {
        logger.error('Failed to send license email', emailResult.error, { email });
        await recordLicenseEvent(license.id, 'email_failed', {
          error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
          email: email,
        });
        return retry('License email sending failed');
      }
    } else {
      // Log that email was skipped due to rate limit
      await recordLicenseEvent(license.id, 'email_skipped_rate_limit', {
        email: email,
        reason: `Rate limit: email already sent within ${EMAIL_RATE_LIMIT_MINUTES} minutes`,
      });
    }
  }

  return SUCCESS;
}

async function handleSubscriptionUpdated(subscription: Stripe.Subscription): Promise<WebhookOutcome> {
  // Type assertion needed due to Stripe API version differences
  const sub = subscription as any;
  const periodEnd = sub.current_period_end
    ? new Date(sub.current_period_end * 1000)
    : undefined;

  await updateLicenseStatusBySubscriptionId(subscription.id, subscription.status, periodEnd);

  // Record event
  const license = await prisma.license.findUnique({
    where: { stripeSubscriptionId: subscription.id },
  });
  if (license) {
    await recordLicenseEvent(license.id, 'subscription_updated', {
      status: subscription.status,
      periodEnd: periodEnd?.toISOString(),
    });
  }

  return SUCCESS;
}

async function handleSubscriptionDeleted(subscription: Stripe.Subscription): Promise<WebhookOutcome> {
  await updateLicenseStatusBySubscriptionId(subscription.id, 'canceled');

  const license = await prisma.license.findUnique({
    where: { stripeSubscriptionId: subscription.id },
  });
  if (license) {
    await recordLicenseEvent(license.id, 'subscription_deleted', {
      deletedAt: new Date().toISOString(),
    });
  }

  return SUCCESS;
}

async function handleChargeRefunded(charge: Stripe.Charge): Promise<WebhookOutcome> {
  const paymentIntentId = typeof charge.payment_intent === 'string'
    ? charge.payment_intent
    : charge.payment_intent?.id;

  if (!paymentIntentId) {
    logger.warn('Charge refunded but no payment intent ID found');
    return permanentFailure('Refunded charge has no payment intent ID');
  }

  const license = await prisma.license.findFirst({
    where: { stripePaymentIntentId: paymentIntentId },
  });

  if (!license) {
    // Not-found may be event ordering (refund delivered before the checkout
    // event finished creating the license). Retriable so the revocation isn't
    // silently dropped; Stripe's retry schedule caps how long this can loop.
    logger.warn('Refund received but no matching license found - requesting Stripe retry', { paymentIntentId });
    return retry(`No license found for refunded payment intent ${paymentIntentId} (possible event ordering)`);
  }

  // Explicit partial-refund policy: goodwill/partial refunds preserve Pro access.
  // Only a refund of the full original charge amount (or more, defensively) revokes.
  if (charge.amount_refunded < charge.amount) {
    await recordLicenseEvent(license.id, 'charge_partially_refunded', {
      amount: charge.amount,
      amountRefunded: charge.amount_refunded,
      policy: 'license_retained',
    });
    logger.info('Partial charge refund recorded; license remains active', {
      licenseId: license.id,
      amount: charge.amount,
      amountRefunded: charge.amount_refunded,
    });
    return SUCCESS;
  }

  await revokeLicense(license.id, 'charge_refunded_full');
  logger.warn('License revoked due to charge refund', { licenseId: license.id });
  return SUCCESS;
}
