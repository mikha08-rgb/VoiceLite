/**
 * Manual License Issuance Script
 * Use this if webhook failed to send email
 *
 * Usage: node manual-license.mjs <customer-email>
 */

import { randomUUID } from 'crypto';

const email = process.argv[2];

if (!email || !email.includes('@')) {
  console.error('Usage: node manual-license.mjs <customer-email>');
  process.exit(1);
}

const licenseKey = randomUUID();

console.log('\n===============================================');
console.log('   VoiceLite Pro - Manual License Key');
console.log('===============================================\n');
console.log(`Email:       ${email}`);
console.log(`License Key: ${licenseKey}\n`);
console.log('Copy this license key and:');
console.log('1. Email it to the customer');
console.log('2. Test activation in VoiceLite app\n');
console.log('===============================================\n');

// Output JSON for easy copying
console.log('API Test Format:');
console.log(JSON.stringify({ licenseKey }, null, 2));
console.log('\n');
