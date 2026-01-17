/**
 * Comprehensive Email Fix Verification Script
 *
 * This script verifies that the email sending fix is working correctly by:
 * 1. Testing direct email sending
 * 2. Checking for failed email events in the database
 * 3. Testing email resending for failed cases
 */

import { prisma } from '../lib/prisma';
import { sendLicenseEmail } from '../lib/emails/license-email';

async function verifyEmailFix() {
  console.log('🔍 Email Fix Verification Report\n');
  console.log('=' .repeat(60));

  // Test 1: Environment Check
  console.log('\n📋 Step 1: Environment Configuration');
  console.log('-'.repeat(60));
  const hasResendKey = !!process.env.RESEND_API_KEY && process.env.RESEND_API_KEY !== 're_placeholder';
  const fromEmail = process.env.RESEND_FROM_EMAIL || 'VoiceLite <basementhustlellc@gmail.com>';

  console.log(`   RESEND_API_KEY: ${hasResendKey ? '✅ Configured' : '❌ Missing'}`);
  console.log(`   RESEND_FROM_EMAIL: ${fromEmail}`);
  console.log(`   Format Check: ${fromEmail.includes('<') && fromEmail.includes('>') ? '✅ Correct' : '⚠️  Check format'}`);

  if (!hasResendKey) {
    console.log('\n❌ Cannot proceed without RESEND_API_KEY');
    process.exit(1);
  }

  // Test 2: Check for failed email events
  console.log('\n📊 Step 2: Checking Database for Failed Emails');
  console.log('-'.repeat(60));

  try {
    const failedEvents = await prisma.licenseEvent.findMany({
      where: {
        eventType: {
          in: ['email_failed', 'email_resend_failed']
        }
      },
      include: {
        license: {
          select: {
            email: true,
            licenseKey: true,
            status: true
          }
        }
      },
      orderBy: {
        createdAt: 'desc'
      },
      take: 10
    });

    if (failedEvents.length === 0) {
      console.log('   ✅ No failed email events found');
    } else {
      console.log(`   ⚠️  Found ${failedEvents.length} failed email event(s):\n`);
      failedEvents.forEach((event, i) => {
        console.log(`   ${i + 1}. License: ${event.license.licenseKey}`);
        console.log(`      Email: ${event.license.email}`);
        console.log(`      Status: ${event.license.status}`);
        console.log(`      Failed at: ${event.createdAt.toISOString()}`);
        console.log(`      Error: ${event.metadata ? JSON.stringify(event.metadata).substring(0, 100) : 'N/A'}`);
        console.log('');
      });
    }

    // Test 3: Test sending to a failed email (if any exist)
    if (failedEvents.length > 0) {
      console.log('\n📧 Step 3: Testing Email Resend for Failed Cases');
      console.log('-'.repeat(60));

      const testCase = failedEvents[0];
      console.log(`   Testing with license: ${testCase.license.licenseKey}`);
      console.log(`   Email: ${testCase.license.email}`);
      console.log(`   Attempting to send...\n`);

      const result = await sendLicenseEmail({
        email: testCase.license.email,
        licenseKey: testCase.license.licenseKey
      });

      if (result.success) {
        console.log(`   ✅ SUCCESS! Email sent with Message ID: ${result.messageId}`);
        console.log(`   This previously failed email has been fixed!`);
      } else {
        console.log(`   ❌ FAILED: ${result.error}`);
        console.log(`   The issue may still exist or there's a different problem`);
      }
    }

    // Test 4: Check recent successful emails
    console.log('\n✅ Step 4: Recent Successful Emails');
    console.log('-'.repeat(60));

    const successfulEvents = await prisma.licenseEvent.findMany({
      where: {
        eventType: {
          in: ['email_sent', 'email_resent_manual']
        },
        createdAt: {
          gte: new Date(Date.now() - 24 * 60 * 60 * 1000) // Last 24 hours
        }
      },
      orderBy: {
        createdAt: 'desc'
      },
      take: 5
    });

    if (successfulEvents.length === 0) {
      console.log('   ℹ️  No successful emails in the last 24 hours');
    } else {
      console.log(`   Found ${successfulEvents.length} successful email(s) in last 24h:\n`);
      successfulEvents.forEach((event, i) => {
        const metadata = event.metadata as any;
        console.log(`   ${i + 1}. ${event.eventType} at ${event.createdAt.toISOString()}`);
        console.log(`      Message ID: ${metadata?.messageId || 'N/A'}`);
        console.log('');
      });
    }

    // Summary
    console.log('\n' + '='.repeat(60));
    console.log('📝 VERIFICATION SUMMARY');
    console.log('='.repeat(60));
    console.log(`✅ Environment configured correctly`);
    console.log(`${failedEvents.length === 0 ? '✅' : '⚠️ '} Failed events: ${failedEvents.length}`);
    console.log(`${successfulEvents.length > 0 ? '✅' : 'ℹ️ '} Recent successful sends: ${successfulEvents.length}`);

    if (failedEvents.length === 0) {
      console.log('\n🎉 All systems operational! No issues detected.');
    } else {
      console.log('\n⚠️  Action needed: Resend emails for failed cases.');
      console.log('   You can use the /api/licenses/resend-email endpoint');
      console.log('   or contact customers to manually resend their licenses.');
    }

  } catch (error) {
    console.error('\n❌ Database error:', error);
    console.error('\nMake sure DATABASE_URL is configured correctly');
  } finally {
    await prisma.$disconnect();
  }
}

verifyEmailFix();
