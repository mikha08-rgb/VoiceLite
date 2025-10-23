import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';
import { prisma } from '@/lib/prisma';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { randomUUID } from 'crypto';

const stripe = new Stripe(process.env.STRIPE_SECRET_KEY!, {
  apiVersion: '2025-08-27.basil',
});

export async function POST(request: NextRequest) {
  console.log('=== STRIPE WEBHOOK RECEIVED ===');

  const body = await request.text();
  const signature = request.headers.get('stripe-signature');

  console.log('Webhook signature present:', !!signature);
  console.log('STRIPE_WEBHOOK_SECRET configured:', !!process.env.STRIPE_WEBHOOK_SECRET);

  if (!signature) {
    console.error('No Stripe signature in headers');
    return NextResponse.json({ error: 'No signature' }, { status: 400 });
  }

  let event: Stripe.Event;

  try {
    event = stripe.webhooks.constructEvent(
      body,
      signature,
      process.env.STRIPE_WEBHOOK_SECRET!
    );
    console.log('Webhook signature verified successfully');
    console.log('Event type:', event.type);
    console.log('Event ID:', event.id);
  } catch (err) {
    console.error('Webhook signature verification failed:', err);
    return NextResponse.json({
      error: 'Invalid signature',
      details: err instanceof Error ? err.message : String(err)
    }, { status: 400 });
  }

  // Handle checkout.session.completed event
  if (event.type === 'checkout.session.completed') {
    const session = event.data.object as Stripe.Checkout.Session;

    try {
      // Check for duplicate webhook events
      const existingEvent = await prisma.webhookEvent.findUnique({
        where: { eventId: event.id },
      });

      if (existingEvent) {
        console.log('Duplicate webhook event, skipping:', event.id);
        return NextResponse.json({ received: true, skipped: 'duplicate' });
      }

      // Record this event to prevent duplicates
      await prisma.webhookEvent.create({
        data: { eventId: event.id },
      });

      // Extract customer email
      const customerEmail = session.customer_email || session.customer_details?.email;

      if (!customerEmail) {
        console.error('No customer email in session:', session.id);
        return NextResponse.json({ error: 'No customer email' }, { status: 400 });
      }

      // Find or create user
      let user = await prisma.user.findUnique({
        where: { email: customerEmail },
      });

      if (!user) {
        user = await prisma.user.create({
          data: { email: customerEmail },
        });
      }

      // Generate license key (UUID format)
      const licenseKey = randomUUID();

      // Create license in database
      const license = await prisma.license.create({
        data: {
          userId: user.id,
          licenseKey,
          type: 'LIFETIME',
          status: 'ACTIVE',
          stripeCustomerId: session.customer as string | null,
          stripePaymentIntentId: session.payment_intent as string | null,
          activatedAt: new Date(),
        },
      });

      // Log license issuance event
      await prisma.licenseEvent.create({
        data: {
          licenseId: license.id,
          type: 'issued',
          metadata: JSON.stringify({
            sessionId: session.id,
            amount: session.amount_total,
            currency: session.currency,
          }),
        },
      });

      // Log user activity
      await prisma.userActivity.create({
        data: {
          userId: user.id,
          activityType: 'LICENSE_ISSUED',
          metadata: JSON.stringify({
            licenseKey: licenseKey,
            sessionId: session.id,
          }),
        },
      });

      // Send license email
      console.log('Attempting to send license email...');
      console.log('RESEND_API_KEY configured:', !!process.env.RESEND_API_KEY);
      console.log('RESEND_FROM_EMAIL:', process.env.RESEND_FROM_EMAIL);

      const emailResult = await sendLicenseEmail({
        email: customerEmail,
        licenseKey,
      });

      if (!emailResult.success) {
        console.error('❌ Failed to send license email');
        console.error('Error details:', emailResult.error);
        // Don't fail the webhook - license is still created
        // We can retry email sending manually if needed
      } else {
        console.log('✅ License email sent successfully!');
        console.log('Resend message ID:', emailResult.messageId);
      }

      console.log('✅ Webhook processing complete:', {
        userId: user.id,
        email: customerEmail,
        licenseKey,
        emailSent: emailResult.success,
      });

      return NextResponse.json({
        received: true,
        licenseKey,
        emailSent: emailResult.success,
      });
    } catch (error) {
      console.error('Error processing checkout.session.completed:', error);
      return NextResponse.json({ error: 'Processing failed' }, { status: 500 });
    }
  }

  // Return 200 for other event types we don't handle
  return NextResponse.json({ received: true });
}
