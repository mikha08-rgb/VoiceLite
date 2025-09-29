import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';
import { sendLicenseEmail } from '@/lib/email';
import { saveLicense } from '@/lib/licenses';

export async function POST(request: NextRequest) {
  const stripe = new Stripe(process.env.STRIPE_SECRET_KEY || 'sk_test_placeholder', {
    apiVersion: '2025-08-27.basil',
  });
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

  // Handle the event
  switch (event.type) {
    case 'checkout.session.completed': {
      const session = event.data.object as Stripe.Checkout.Session;

      // Generate a simple license key
      const licenseKey = generateLicenseKey();

      // Get customer email
      const customerEmail = session.customer_email || session.customer_details?.email;

      if (!customerEmail) {
        console.error('No customer email found in session');
        break;
      }

      // Save license (to JSON file for simplicity)
      await saveLicense({
        email: customerEmail,
        licenseKey,
        customerId: session.customer as string,
        subscriptionId: session.subscription as string,
        createdAt: new Date().toISOString(),
        status: 'active',
      });

      // Send email with license key
      await sendLicenseEmail(customerEmail, licenseKey);

      console.log(`License ${licenseKey} created for ${customerEmail}`);
      break;
    }

    case 'customer.subscription.updated':
    case 'customer.subscription.deleted': {
      const subscription = event.data.object as Stripe.Subscription;
      // Update license status if needed
      console.log(`Subscription ${subscription.id} status: ${subscription.status}`);
      break;
    }

    default:
      console.log(`Unhandled event type ${event.type}`);
  }

  return NextResponse.json({ received: true });
}

function generateLicenseKey(): string {
  const timestamp = Date.now().toString(36);
  const randomPart = Math.random().toString(36).substring(2, 11).toUpperCase();
  return `VL-${timestamp.toUpperCase()}-${randomPart}`;
}