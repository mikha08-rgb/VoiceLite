import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';
import { sendLicenseEmail } from '@/lib/email';
import {
  createLicenseFromStripe,
  revokeLicense,
} from '@/lib/licensing';
import { prisma } from '@/lib/prisma';

// Lazy initialization of Stripe client to allow builds without env vars
function getStripeClient() {
  if (!process.env.STRIPE_SECRET_KEY || process.env.STRIPE_SECRET_KEY === 'sk_test_placeholder') {
    throw new Error('STRIPE_SECRET_KEY must be configured');
  }
  if (!process.env.STRIPE_WEBHOOK_SECRET || process.env.STRIPE_WEBHOOK_SECRET === 'whsec_placeholder') {
    throw new Error('STRIPE_WEBHOOK_SECRET must be configured');
  }
  return new Stripe(process.env.STRIPE_SECRET_KEY, {
    apiVersion: '2025-08-27.basil',
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
        await handleCheckoutCompleted(event.data.object as Stripe.Checkout.Session);
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

async function handleCheckoutCompleted(session: Stripe.Checkout.Session) {
  const email = session.customer_email || session.customer_details?.email;
  const stripeCustomerId = (session.customer as string) ?? '';

  if (!email || !stripeCustomerId) {
    throw new Error('Missing customer email or ID on checkout session');
  }

  const paymentIntentId = typeof session.payment_intent === 'string'
    ? session.payment_intent
    : session.payment_intent?.id;

  if (!paymentIntentId) {
    throw new Error('Missing payment intent for payment');
  }

  // Create or get existing license (handles duplicate purchases)
  const license = await createLicenseFromStripe({
    email,
    stripeCustomerId,
    stripePaymentIntentId: paymentIntentId,
  });

  // Send license email (even for existing licenses - user may have lost email)
  await sendLicenseEmail({
    email,
    licenseKey: license.licenseKey,
    plan: 'lifetime',
  });

  console.log(`License ${license.licenseKey} issued/resent to ${email}`);
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
    await revokeLicense(license.id);
    console.log(`License ${license.licenseKey} revoked due to charge refund`);
  }
}