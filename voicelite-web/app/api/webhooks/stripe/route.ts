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
    console.error('Webhook signature verification failed:', error);
    return NextResponse.json({ error: 'Invalid signature' }, { status: 400 });
  }

  // Idempotency check with atomic create to prevent race conditions
  // Use unique constraint on eventId to ensure only one webhook processes this event
  try {
    await prisma.webhookEvent.create({
      data: { eventId: event.id },
    });
  } catch (error: any) {
    // P2002 = Unique constraint violation = event already processed
    if (error.code === 'P2002') {
      console.log(`Event ${event.id} already processed, skipping (race condition prevented)`);
      return NextResponse.json({ received: true, cached: true });
    }
    // Other errors should be thrown
    throw error;
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
        console.log(`Unhandled event type ${event.type}`);
    }
  } catch (error) {
    console.error('Webhook processing failure', error);
    // Return 200 to avoid Stripe retries for permanent failures
    // The event is already marked as processed
    return NextResponse.json({ error: 'Processing error', eventId: event.id }, { status: 200 });
  }

  return NextResponse.json({ received: true, eventId: event.id });
}

async function handleCheckoutCompleted(stripe: Stripe, session: Stripe.Checkout.Session) {
  const email = session.customer_email || session.customer_details?.email;
  const stripeCustomerId = (session.customer as string) ?? '';

  if (!email || !stripeCustomerId) {
    throw new Error('Missing customer email or ID on checkout session');
  }

  const plan = session.metadata?.plan ?? (session.mode === 'subscription' ? 'quarterly' : 'lifetime');

  if (plan === 'quarterly') {
    const subscriptionId = session.subscription as string | undefined;
    if (!subscriptionId) {
      throw new Error('Missing subscription id for quarterly plan');
    }
    const subscription = (await stripe.subscriptions.retrieve(subscriptionId)) as any;
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

    console.log(`📧 Attempting to send license email to ${email} (License: ${license.licenseKey})`);

    const emailResult = await sendLicenseEmail({
      email,
      licenseKey: license.licenseKey,
    });

    if (emailResult.success) {
      console.log(`✅ License email sent successfully to ${email} (MessageID: ${emailResult.messageId})`);
      await recordLicenseEvent(license.id, 'email_sent', {
        messageId: emailResult.messageId,
        email: email,
      });
    } else {
      console.error(`❌ Failed to send license email to ${email}:`, emailResult.error);
      await recordLicenseEvent(license.id, 'email_failed', {
        error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
        email: email,
      });
      // Don't throw - license was created successfully, just log the email failure
    }
  } else {
    const paymentIntentId = typeof session.payment_intent === 'string'
      ? session.payment_intent
      : session.payment_intent?.id;

    if (!paymentIntentId) {
      throw new Error('Missing payment intent for lifetime plan');
    }

    const license = await upsertLicenseFromStripe({
      email,
      type: LicenseType.LIFETIME,
      stripeCustomerId,
      stripePaymentIntentId: paymentIntentId,
    });

    console.log(`📧 Attempting to send license email to ${email} (License: ${license.licenseKey})`);

    const emailResult = await sendLicenseEmail({
      email,
      licenseKey: license.licenseKey,
    });

    if (emailResult.success) {
      console.log(`✅ License email sent successfully to ${email} (MessageID: ${emailResult.messageId})`);
      await recordLicenseEvent(license.id, 'email_sent', {
        messageId: emailResult.messageId,
        email: email,
      });
    } else {
      console.error(`❌ Failed to send license email to ${email}:`, emailResult.error);
      await recordLicenseEvent(license.id, 'email_failed', {
        error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
        email: email,
      });
      // Don't throw - license was created successfully, just log the email failure
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
    console.warn('Charge refunded but no payment intent ID found');
    return;
  }

  const license = await prisma.license.findFirst({
    where: { stripePaymentIntentId: paymentIntentId },
  });

  if (license) {
    await revokeLicense(license.id, 'charge_refunded');
    console.log(`License ${license.id} revoked due to charge refund`);
  }
}