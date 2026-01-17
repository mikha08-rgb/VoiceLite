/**
 * Find Customers Who Didn't Receive License Emails
 *
 * This script identifies all customers who purchased licenses
 * but never received their license key emails, then provides
 * options to resend them.
 */

import { prisma } from '../lib/prisma';
import { sendLicenseEmail } from '../lib/emails/license-email';
import { recordLicenseEvent } from '../lib/licensing';

interface MissingEmailCustomer {
  licenseId: string;
  email: string;
  licenseKey: string;
  status: string;
  type: string;
  createdAt: Date;
  hasEmailSent: boolean;
  hasEmailFailed: boolean;
}

async function findMissingEmails() {
  console.log('🔍 Finding Customers Who Didn\'t Receive License Emails\n');
  console.log('='.repeat(70));

  try {
    // Get all licenses
    const allLicenses = await prisma.license.findMany({
      where: {
        status: 'ACTIVE' // Only active licenses
      },
      orderBy: {
        createdAt: 'desc'
      }
    });

    console.log(`\n📊 Found ${allLicenses.length} active license(s)\n`);

    // Check each license for email events
    const missingEmails: MissingEmailCustomer[] = [];

    for (const license of allLicenses) {
      const events = await prisma.licenseEvent.findMany({
        where: {
          licenseId: license.id,
          type: {
            in: ['email_sent', 'email_resent_manual', 'email_failed', 'email_resend_failed']
          }
        }
      });

      const hasEmailSent = events.some(e => e.type === 'email_sent' || e.type === 'email_resent_manual');
      const hasEmailFailed = events.some(e => e.type === 'email_failed' || e.type === 'email_resend_failed');

      if (!hasEmailSent) {
        missingEmails.push({
          licenseId: license.id,
          email: license.email,
          licenseKey: license.licenseKey,
          status: license.status,
          type: license.type,
          createdAt: license.createdAt,
          hasEmailSent,
          hasEmailFailed
        });
      }
    }

    // Display results
    console.log('='.repeat(70));
    console.log('📋 CUSTOMERS MISSING LICENSE EMAILS');
    console.log('='.repeat(70));

    if (missingEmails.length === 0) {
      console.log('\n✅ Great news! All customers have received their emails.\n');
      return [];
    }

    console.log(`\n⚠️  Found ${missingEmails.length} customer(s) who didn't receive emails:\n`);

    missingEmails.forEach((customer, i) => {
      console.log(`${i + 1}. ${customer.email}`);
      console.log(`   License Key: ${customer.licenseKey}`);
      console.log(`   Type: ${customer.type}`);
      console.log(`   Status: ${customer.status}`);
      console.log(`   Purchased: ${customer.createdAt.toISOString()}`);
      console.log(`   Email Failed Before: ${customer.hasEmailFailed ? 'Yes ❌' : 'No'}`);
      console.log('');
    });

    return missingEmails;

  } catch (error) {
    console.error('❌ Database error:', error);
    throw error;
  }
}

async function resendMissingEmails(customers: MissingEmailCustomer[], autoSend: boolean = false) {
  if (customers.length === 0) {
    return;
  }

  console.log('='.repeat(70));
  console.log('📧 RESENDING LICENSE EMAILS');
  console.log('='.repeat(70));

  if (!autoSend) {
    console.log('\nℹ️  Run with AUTO_SEND=true to automatically resend all emails');
    console.log('   Example: AUTO_SEND=true npx tsx scripts/find-missing-emails.ts\n');
    console.log('📝 To manually resend, use:');
    console.log('   npx tsx scripts/resend-single.ts <email-address>\n');
    return;
  }

  console.log(`\n🚀 Attempting to resend ${customers.length} email(s)...\n`);

  let successCount = 0;
  let failCount = 0;

  for (const customer of customers) {
    console.log(`Sending to ${customer.email}...`);

    try {
      const result = await sendLicenseEmail({
        email: customer.email,
        licenseKey: customer.licenseKey
      });

      if (result.success) {
        console.log(`   ✅ SUCCESS (Message ID: ${result.messageId})`);
        await recordLicenseEvent(customer.licenseId, 'email_resent_manual', {
          messageId: result.messageId,
          email: customer.email,
          reason: 'bulk_resend_after_fix'
        });
        successCount++;
      } else {
        console.log(`   ❌ FAILED: ${result.error}`);
        await recordLicenseEvent(customer.licenseId, 'email_resend_failed', {
          error: result.error instanceof Error ? result.error.message : String(result.error),
          email: customer.email,
          reason: 'bulk_resend_after_fix'
        });
        failCount++;
      }

      // Rate limiting: wait 100ms between emails
      await new Promise(resolve => setTimeout(resolve, 100));

    } catch (error) {
      console.log(`   ❌ ERROR: ${error}`);
      failCount++;
    }

    console.log('');
  }

  console.log('='.repeat(70));
  console.log('📊 RESEND SUMMARY');
  console.log('='.repeat(70));
  console.log(`✅ Successful: ${successCount}`);
  console.log(`❌ Failed: ${failCount}`);
  console.log(`📧 Total: ${customers.length}`);
  console.log('');
}

async function main() {
  const autoSend = process.env.AUTO_SEND === 'true';

  try {
    const missingCustomers = await findMissingEmails();
    await resendMissingEmails(missingCustomers, autoSend);

    console.log('='.repeat(70));
    console.log('✅ Script completed successfully');
    console.log('='.repeat(70));
    console.log('\n💡 Next steps:');
    console.log('   1. Review the list above');
    console.log('   2. If you want to auto-resend all, run:');
    console.log('      AUTO_SEND=true npx dotenv-cli -e .env.local -- npx tsx scripts/find-missing-emails.ts');
    console.log('   3. Or manually resend individual emails using the resend API endpoint\n');

  } catch (error) {
    console.error('\n❌ Script failed:', error);
    console.error('\nPossible issues:');
    console.error('   - Database connection failed (check DATABASE_URL)');
    console.error('   - Network/VPN issues');
    console.error('   - Supabase instance paused/down\n');
    process.exit(1);
  } finally {
    await prisma.$disconnect();
  }
}

main();
