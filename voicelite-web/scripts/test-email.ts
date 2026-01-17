/**
 * Test Email Sending Script
 *
 * Tests the license email sending functionality to diagnose email delivery issues
 *
 * Usage: npx tsx scripts/test-email.ts <test-email@example.com>
 */

import { sendLicenseEmail } from '../lib/emails/license-email';

const testEmail = process.argv[2];

if (!testEmail) {
  console.error('❌ Error: Please provide a test email address');
  console.log('Usage: npx tsx scripts/test-email.ts <test-email@example.com>');
  process.exit(1);
}

// Validate email format
const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
if (!emailRegex.test(testEmail)) {
  console.error('❌ Error: Invalid email format');
  process.exit(1);
}

async function testEmailSending() {
  console.log('🧪 Testing Email Sending...\n');

  // Check environment variables
  console.log('📋 Environment Check:');
  console.log(`   RESEND_API_KEY: ${process.env.RESEND_API_KEY ? '✅ Set' : '❌ Missing'}`);
  console.log(`   RESEND_FROM_EMAIL: ${process.env.RESEND_FROM_EMAIL || 'basementhustlellc@gmail.com'}`);
  console.log('');

  const testLicenseKey = 'TEST-1234-5678-90AB-CDEF';

  console.log('📧 Sending test email...');
  console.log(`   To: ${testEmail}`);
  console.log(`   License Key: ${testLicenseKey}`);
  console.log('');

  try {
    const result = await sendLicenseEmail({
      email: testEmail,
      licenseKey: testLicenseKey,
    });

    if (result.success) {
      console.log('✅ Email sent successfully!');
      console.log(`   Message ID: ${result.messageId}`);
      console.log('');
      console.log('📬 Check your inbox (and spam folder)');
      console.log('💡 If you don\'t receive it, check:');
      console.log('   1. Resend dashboard for delivery status');
      console.log('   2. Email provider spam/blocklist settings');
      console.log('   3. Resend domain verification status');
    } else {
      console.error('❌ Email sending failed!');
      console.error('   Error:', result.error);
      console.log('');
      console.log('🔍 Common causes:');
      console.log('   1. Invalid RESEND_API_KEY');
      console.log('   2. Domain not verified in Resend');
      console.log('   3. Email address in Resend blocklist');
      console.log('   4. Rate limiting or quota exceeded');
    }
  } catch (error) {
    console.error('💥 Unexpected error:', error);
  }
}

testEmailSending();