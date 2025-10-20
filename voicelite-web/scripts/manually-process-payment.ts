import Stripe from 'stripe';
import { createLicenseFromStripe } from '../lib/licensing';
import { sendLicenseEmail } from '../lib/email';
import { prisma } from '../lib/prisma';

const stripe = new Stripe(process.env.STRIPE_SECRET_KEY!, {
  apiVersion: '2025-09-30.clover',
});

async function main() {
  const sessionId = process.argv[2];

  if (!sessionId) {
    console.error('Usage: npx tsx scripts/manually-process-payment.ts <session_id>');
    process.exit(1);
  }

  console.log(`Processing session: ${sessionId}\n`);

  // Get the session details
  const session = await stripe.checkout.sessions.retrieve(sessionId, {
    expand: ['payment_intent'],
  });

  console.log(`Customer Email: ${session.customer_details?.email}`);
  console.log(`Payment Status: ${session.payment_status}`);
  console.log(`Amount: $${(session.amount_total || 0) / 100}`);

  if (session.payment_status !== 'paid') {
    console.error('\n❌ Payment not completed yet!');
    process.exit(1);
  }

  const email = session.customer_details?.email;
  if (!email) {
    console.error('\n❌ No customer email found!');
    process.exit(1);
  }

  // Check if license already exists
  const paymentIntent = typeof session.payment_intent === 'string'
    ? session.payment_intent
    : session.payment_intent?.id;

  if (paymentIntent) {
    const existing = await prisma.license.findUnique({
      where: { stripePaymentIntentId: paymentIntent },
    });

    if (existing) {
      console.log(`\n✅ License already exists: ${existing.licenseKey}`);
      console.log(`Email sent: ${existing.emailSent}`);

      if (!existing.emailSent) {
        console.log('\nResending email...');
        await sendLicenseEmail({
          email: existing.email,
          licenseKey: existing.licenseKey,
          plan: 'lifetime',
        });
        await prisma.license.update({
          where: { id: existing.id },
          data: { emailSent: true },
        });
        console.log('✅ Email sent!');
      }
      return;
    }
  }

  // Create license
  console.log('\nCreating license...');

  const customerId = typeof session.customer === 'string'
    ? session.customer
    : session.customer?.id || '';

  const license = await createLicenseFromStripe({
    email: email,
    stripeCustomerId: customerId,
    stripePaymentIntentId: paymentIntent!,
  });
  console.log(`✅ License created: ${license.licenseKey}`);

  // Send email
  console.log('\nSending email...');
  await sendLicenseEmail({
    email: license.email,
    licenseKey: license.licenseKey,
    plan: 'lifetime',
  });

  // Mark email as sent
  await prisma.license.update({
    where: { id: license.id },
    data: { emailSent: true },
  });

  console.log('✅ Email sent successfully!');
}

main()
  .catch(console.error)
  .finally(() => prisma.$disconnect());