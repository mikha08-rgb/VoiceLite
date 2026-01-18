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

// MED-5 FIX: Rate limit email sending (1 per 5 minutes per license)
const EMAIL_RATE_LIMIT_MINUTES = 5;

// Custom error class for transient errors that should trigger Stripe retry
class RetriableError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'RetriableError';
  }
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

  // Verify request origin for additional CSRF protection
  const origin = request.headers.get('origin');
  const allowedOrigins = ['https://stripe.com', 'https://dashboard.stripe.com'];

  if (origin && !allowedOrigins.includes(origin)) {
    logger.warn('Suspicious webhook origin', { origin });
    // Still allow - Stripe doesn't always set origin, but log for monitoring
  }

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
  const now = new Date();
  let isFirstProcessing = false;

  try {
    await prisma.webhookEvent.create({
      data: {
        eventId: event.id,
        seenAt: now,
        processedAt: now,
      },
    });
    // INSERT succeeded - we're the first processor
    isFirstProcessing = true;
  } catch (error: any) {
    // Check if this is a unique constraint violation (record already exists)
    if (error?.code === 'P2002') {
      // Record already exists - another request is processing or has processed this event
      logger.info('Event already claimed, skipping', { eventId: event.id });
      return NextResponse.json({ received: true, cached: true });
    }
    // Unexpected error - re-throw
    throw error;
  }

  if (!isFirstProcessing) {
    return NextResponse.json({ received: true, cached: true });
  }

  try {
    switch (event.type) {
      case 'checkout.session.completed':
        await handleCheckoutCompleted(stripe, event.data.object as Stripe.Checkout.Session);
        break;
      case 'customer.subscription.updated':
        await handleSubscriptionUpdated(event.data.object as Stripe.Subscription);
        break;
      case 'customer.subscription.deleted':
        await handleSubscriptionDeleted(event.data.object as Stripe.Subscription);
        break;
      case 'charge.refunded':
        await handleChargeRefunded(event.data.object as Stripe.Charge);
        break;
      default:
        logger.warn('Unhandled event type', { eventType: event.type });
    }
  } catch (error) {
    logger.error('Webhook processing failure', error);

    // Return 500 for transient errors (Stripe will retry)
    // Return 200 for permanent errors (no point retrying)
    if (error instanceof RetriableError) {
      return NextResponse.json({ error: error.message, eventId: event.id }, { status: 500 });
    }

    // Permanent failure - don't retry
    return NextResponse.json({ error: 'Processing error', eventId: event.id }, { status: 200 });
  }

  return NextResponse.json({ received: true, eventId: event.id });
}

async function handleCheckoutCompleted(stripe: Stripe, session: Stripe.Checkout.Session) {
  logger.info('Webhook: checkout.session.completed', { sessionId: session.id });

  const email = session.customer_email || session.customer_details?.email;
  const stripeCustomerId = (session.customer as string) ?? '';

  logger.debug('Checkout details', { email, stripeCustomerId });

  if (!email || !stripeCustomerId) {
    logger.error('Missing customer email or ID');
    throw new Error('Missing customer email or ID on checkout session');
  }

  const plan = session.metadata?.plan ?? (session.mode === 'subscription' ? 'quarterly' : 'lifetime');
  logger.debug('Plan details', { plan, mode: session.mode });

  if (plan === 'quarterly') {
    const subscriptionId = session.subscription as string | undefined;
    if (!subscriptionId) {
      throw new Error('Missing subscription id for quarterly plan');
    }

    // Wrap Stripe API call - network errors should trigger retry
    let subscription: any;
    try {
      subscription = await stripe.subscriptions.retrieve(subscriptionId);
    } catch (stripeError) {
      logger.error('Failed to retrieve subscription', stripeError, { subscriptionId });
      throw new RetriableError(`Stripe API error: Failed to retrieve subscription ${subscriptionId}`);
    }

    const currentPeriodEnd = subscription.current_period_end
      ? new Date(subscription.current_period_end * 1000)
      : undefined;

    // Wrap database call - errors should trigger retry
    let license;
    try {
      license = await upsertLicenseFromStripe({
        email,
        type: LicenseType.SUBSCRIPTION,
        stripeCustomerId,
        stripeSubscriptionId: subscriptionId,
        subscriptionStatus: subscription.status,
        periodEndsAt: currentPeriodEnd,
      });
    } catch (dbError) {
      logger.error('Database error creating subscription license', dbError, { email });
      throw new RetriableError(`Database error creating subscription license for ${email}`);
    }

    logger.info('Sending license email', { email, licenseKey: license.licenseKey });

    // MED-5 FIX: Check rate limit before sending
    if (await canSendLicenseEmail(license.id)) {
      const emailResult = await sendLicenseEmail({
        email,
        licenseKey: license.licenseKey,
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
        // Throw RetriableError so Stripe will retry - customer MUST get their email
        throw new RetriableError(`Email sending failed for ${email}`);
      }
    } else {
      // Log that email was skipped due to rate limit
      await recordLicenseEvent(license.id, 'email_skipped_rate_limit', {
        email: email,
        reason: 'Rate limit: email already sent within 5 minutes',
      });
    }
  } else {
    logger.info('Processing lifetime payment');
    const paymentIntentId = typeof session.payment_intent === 'string'
      ? session.payment_intent
      : session.payment_intent?.id;

    logger.debug('Payment intent', { paymentIntentId });

    if (!paymentIntentId) {
      logger.error('Missing payment intent for lifetime plan');
      throw new Error('Missing payment intent for lifetime plan');
    }

    logger.debug('Creating license in database');

    let license;
    try {
      license = await upsertLicenseFromStripe({
        email,
        type: LicenseType.LIFETIME,
        stripeCustomerId,
        stripePaymentIntentId: paymentIntentId,
      });
      logger.info('License created', { licenseKey: license.licenseKey, licenseId: license.id });
    } catch (dbError) {
      logger.error('Database error creating lifetime license', dbError, { email });
      throw new RetriableError(`Database error creating lifetime license for ${email}`);
    }

    logger.info('Sending license email', { email, licenseKey: license.licenseKey });

    // MED-5 FIX: Check rate limit before sending
    if (await canSendLicenseEmail(license.id)) {
      const emailResult = await sendLicenseEmail({
        email,
        licenseKey: license.licenseKey,
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
        // Throw RetriableError so Stripe will retry - customer MUST get their email
        throw new RetriableError(`Email sending failed for ${email}`);
      }
    } else {
      // Log that email was skipped due to rate limit
      await recordLicenseEvent(license.id, 'email_skipped_rate_limit', {
        email: email,
        reason: 'Rate limit: email already sent within 5 minutes',
      });
    }
  }
}

async function handleSubscriptionUpdated(subscription: Stripe.Subscription) {
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
}

async function handleSubscriptionDeleted(subscription: Stripe.Subscription) {
  await updateLicenseStatusBySubscriptionId(subscription.id, 'canceled');

  const license = await prisma.license.findUnique({
    where: { stripeSubscriptionId: subscription.id },
  });
  if (license) {
    await recordLicenseEvent(license.id, 'subscription_deleted', {
      deletedAt: new Date().toISOString(),
    });
  }
}

async function handleChargeRefunded(charge: Stripe.Charge) {
  const paymentIntentId = typeof charge.payment_intent === 'string'
    ? charge.payment_intent
    : charge.payment_intent?.id;

  if (!paymentIntentId) {
    logger.warn('Charge refunded but no payment intent ID found');
    return;
  }

  const license = await prisma.license.findFirst({
    where: { stripePaymentIntentId: paymentIntentId },
  });

  if (license) {
    await revokeLicense(license.id, 'charge_refunded');
    logger.warn('License revoked due to charge refund', { licenseId: license.id });
  }
}
