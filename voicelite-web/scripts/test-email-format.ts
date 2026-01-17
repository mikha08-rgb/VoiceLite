/**
 * Email Format Verification Test
 *
 * Verifies that the email sender format is correct and won't cause
 * the nested format bug that was breaking email sending
 */

import { Resend } from 'resend';

function getResendClient() {
  const key = process.env.RESEND_API_KEY;
  if (!key || key === 're_placeholder') {
    throw new Error('RESEND_API_KEY must be configured');
  }
  return new Resend(key);
}

async function testEmailFormat() {
  console.log('🧪 Testing Email Format Fix\n');
  console.log('='.repeat(60));

  // Check environment
  const fromEmail = process.env.RESEND_FROM_EMAIL || 'VoiceLite <basementhustlellc@gmail.com>';
  console.log('\n📋 Configuration:');
  console.log(`   RESEND_FROM_EMAIL: "${fromEmail}"`);

  // Verify format
  console.log('\n🔍 Format Validation:');

  const nestedPattern = /<.*<.*>.*>/;
  const validPattern = /^[^<>]+\s*<[^<>]+>$|^[^<>]+$/;

  const hasNestedBrackets = nestedPattern.test(fromEmail);
  const hasValidFormat = validPattern.test(fromEmail);

  if (hasNestedBrackets) {
    console.log('   ❌ NESTED FORMAT DETECTED: This will cause emails to fail!');
    console.log(`      Found: "${fromEmail}"`);
    console.log('      Problem: Multiple angle brackets create invalid format');
    return false;
  } else {
    console.log('   ✅ No nested brackets found');
  }

  if (hasValidFormat) {
    console.log('   ✅ Format is valid');
  } else {
    console.log('   ⚠️  Format may be unusual (but might still work)');
  }

  // Test actual sending
  console.log('\n📧 Sending Test Email:');
  console.log(`   From: "${fromEmail}"`);
  console.log(`   To: test@resend.dev (Resend's test address)`);

  try {
    const resend = getResendClient();

    // Resend has a special test email address that always accepts emails
    const result = await resend.emails.send({
      from: fromEmail,
      to: 'delivered@resend.dev', // Resend's test email that always succeeds
      subject: 'VoiceLite Email Format Test',
      html: '<p>This is a test email to verify the sender format is correct.</p>',
      text: 'This is a test email to verify the sender format is correct.',
    });

    console.log('\n✅ TEST PASSED!');
    console.log(`   Email accepted by Resend API`);
    console.log(`   Message ID: ${result.data?.id}`);
    console.log(`   Sender format is working correctly!`);

    return true;
  } catch (error: any) {
    console.log('\n❌ TEST FAILED!');
    console.log(`   Error: ${error.message}`);

    if (error.message.includes('from') || error.message.includes('sender')) {
      console.log('   ⚠️  This appears to be a sender format issue!');
      console.log(`   Current from value: "${fromEmail}"`);
      console.log('   Expected format: "Name <email@domain.com>" or "email@domain.com"');
    }

    return false;
  }
}

async function main() {
  const success = await testEmailFormat();

  console.log('\n' + '='.repeat(60));
  console.log('📝 SUMMARY');
  console.log('='.repeat(60));

  if (success) {
    console.log('✅ Email format fix is working correctly!');
    console.log('✅ Emails should now send successfully after purchases');
    console.log('✅ Resend functionality should work properly');
  } else {
    console.log('❌ Email format issue detected or API error');
    console.log('⚠️  Review the errors above and fix the configuration');
  }

  process.exit(success ? 0 : 1);
}

main();
