/**
 * Fix Missing Stripe Webhooks
 *
 * This script:
 * 1. Fetches all successful payments from Stripe
 * 2. Creates missing License records in the database
 * 3. Sends license emails to customers who never received them
 *
 * Use this when Stripe webhooks failed to process purchases.
 */

import Stripe from 'stripe';
import { prisma } from '../lib/prisma';
import { sendLicenseEmail } from '../lib/emails/license-email';
import { upsertLicenseFromStripe, recordLicenseEvent } from '../lib/licensing';
import { LicenseType } from '@prisma/client';

function getStripeClient() {
  if (!process.env.STRIPE_SECRET_KEY || process.env.STRIPE_SECRET_KEY === 'sk_test_placeholder') {
    throw new Error('STRIPE_SECRET_KEY must be configured');
  }
  return new Stripe(process.env.STRIPE_SECRET_KEY, {
    apiVersion: '2025-09-30.clover',
  });
}

async function fixMissingWebhooks(dryRun: boolean = true) {
  console.log('🔍 Checking Stripe for Missing Purchases\n');
  console.log('='.repeat(70));

  if (dryRun) {
    console.log('\n⚠️  DRY RUN MODE - No changes will be made');
    console.log('   Set DRY_RUN=false to actually create licenses and send emails\n');
  }

  const stripe = getStripeClient();

  try {
    // Step 1: Get all successful checkout sessions
    console.log('\n📊 Step 1: Fetching Successful Stripe Checkouts...');

    const sessions = await stripe.checkout.sessions.list({
      limit: 100,
      expand: ['data.customer', 'data.subscription', 'data.payment_intent']
    });

    const successfulSessions = sessions.data.filter(s => s.payment_status === 'paid');
    console.log(`   Found ${successfulSessions.length} paid checkout session(s)\n`);

    if (successfulSessions.length === 0) {
      console.log('✅ No paid checkouts found. Either:');
      console.log('   - No purchases have been made yet');
      console.log('   - Or purchases are older than Stripe\'s default limit');
      console.log('\nTo check older purchases, modify the script to add created filter.');
      return;
    }

    // Step 2: Check which ones are missing from database
    console.log('📊 Step 2: Checking Database for Missing Licenses...\n');

    const missingPurchases: Array<{
      session: Stripe.Checkout.Session;
      email: string;
      customerId: string;
      type: LicenseType;
      subscriptionId?: string;
      paymentIntentId?: string;
    }> = [];

    for (const session of successfulSessions) {
      const email = session.customer_email || (session.customer_details?.email);
      const customerId = typeof session.customer === 'string' ? session.customer : session.customer?.id;

      if (!email || !customerId) {
        console.log(`   ⚠️  Skipping session ${session.id} - missing email or customer ID`);
        continue;
      }

      // Check if license exists
      const existingLicense = await prisma.license.findFirst({
        where: {
          stripeCustomerId: customerId
        }
      });

      if (!existingLicense) {
        const plan = session.metadata?.plan ?? (session.mode === 'subscription' ? 'quarterly' : 'lifetime');
        const isSubscription = plan === 'quarterly';

        missingPurchases.push({
          session,
          email,
          customerId,
          type: isSubscription ? LicenseType.SUBSCRIPTION : LicenseType.LIFETIME,
          subscriptionId: isSubscription ? (typeof session.subscription === 'string' ? session.subscription : session.subscription?.id) : undefined,
          paymentIntentId: !isSubscription ? (typeof session.payment_intent === 'string' ? session.payment_intent : session.payment_intent?.id) : undefined
        });
      }
    }

    console.log(`   Found ${missingPurchases.length} purchase(s) missing from database:\n`);

    if (missingPurchases.length === 0) {
      console.log('✅ All Stripe purchases are already in the database!');
      console.log('   The issue might be that emails failed to send (not webhook failure).\n');
      return;
    }

    // Display missing purchases
    missingPurchases.forEach((purchase, i) => {
      console.log(`   ${i + 1}. ${purchase.email}`);
      console.log(`      Type: ${purchase.type}`);
      console.log(`      Customer ID: ${purchase.customerId}`);
      console.log(`      Session ID: ${purchase.session.id}`);
      console.log(`      Amount: $${(purchase.session.amount_total || 0) / 100}`);
      console.log(`      Date: ${new Date(purchase.session.created * 1000).toISOString()}`);
      console.log('');
    });

    if (dryRun) {
      console.log('\n='.repeat(70));
      console.log('📝 DRY RUN SUMMARY');
      console.log('='.repeat(70));
      console.log(`\n✅ Found ${missingPurchases.length} customer(s) who need licenses`);
      console.log('\n🚀 To fix this, run:');
      console.log('   DRY_RUN=false npx dotenv-cli -e .env.local -- npx tsx scripts/fix-stripe-webhooks.ts');
      console.log('\nThis will:');
      console.log('   1. Create License records in database');
      console.log('   2. Generate license keys');
      console.log('   3. Send emails with license keys');
      console.log('   4. Record all events in audit trail\n');
      return;
    }

    // Step 3: Create licenses and send emails
    console.log('\n='.repeat(70));
    console.log('🚀 Step 3: Creating Licenses and Sending Emails...');
    console.log('='.repeat(70));

    let successCount = 0;
    let failCount = 0;

    for (const purchase of missingPurchases) {
      console.log(`\nProcessing: ${purchase.email}`);

      try {
        // Create license
        let periodEndsAt: Date | undefined;
        let subscriptionStatus: string | undefined;

        if (purchase.type === LicenseType.SUBSCRIPTION && purchase.subscriptionId) {
          const subscription = await stripe.subscriptions.retrieve(purchase.subscriptionId);
          subscriptionStatus = subscription.status;
          periodEndsAt = subscription.current_period_end
            ? new Date(subscription.current_period_end * 1000)
            : undefined;
        }

        const license = await upsertLicenseFromStripe({
          email: purchase.email,
          type: purchase.type,
          stripeCustomerId: purchase.customerId,
          stripeSubscriptionId: purchase.subscriptionId,
          stripePaymentIntentId: purchase.paymentIntentId,
          subscriptionStatus,
          periodEndsAt
        });

        console.log(`   ✅ License created: ${license.licenseKey}`);

        // Send email
        const emailResult = await sendLicenseEmail({
          email: purchase.email,
          licenseKey: license.licenseKey
        });

        if (emailResult.success) {
          console.log(`   ✅ Email sent (Message ID: ${emailResult.messageId})`);
          await recordLicenseEvent(license.id, 'email_sent', {
            messageId: emailResult.messageId,
            email: purchase.email,
            reason: 'retroactive_webhook_fix'
          });
          successCount++;
        } else {
          console.log(`   ❌ Email failed: ${emailResult.error}`);
          await recordLicenseEvent(license.id, 'email_failed', {
            error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
            email: purchase.email,
            reason: 'retroactive_webhook_fix'
          });
          failCount++;
        }

        // Rate limiting
        await new Promise(resolve => setTimeout(resolve, 200));

      } catch (error) {
        console.log(`   ❌ Error: ${error}`);
        failCount++;
      }
    }

    console.log('\n' + '='.repeat(70));
    console.log('📊 FINAL SUMMARY');
    console.log('='.repeat(70));
    console.log(`\n✅ Successful: ${successCount}`);
    console.log(`❌ Failed: ${failCount}`);
    console.log(`📧 Total processed: ${missingPurchases.length}`);
    console.log(`\n💡 Next steps:`);
    console.log(`   1. Verify webhook endpoint is configured in Stripe`);
    console.log(`   2. Check https://dashboard.stripe.com/webhooks`);
    console.log(`   3. Ensure endpoint is: https://voicelite.app/api/webhook`);
    console.log(`   4. Deploy the email format fix to production\n`);

  } catch (error) {
    console.error('\n❌ Error:', error);
    throw error;
  } finally {
    await prisma.$disconnect();
  }
}

async function main() {
  const dryRun = process.env.DRY_RUN !== 'false';

  try {
    await fixMissingWebhooks(dryRun);
  } catch (error) {
    console.error('\n💥 Script failed:', error);
    process.exit(1);
  }
}

main();
