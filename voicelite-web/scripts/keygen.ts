/**
 * Generate Ed25519 keypairs for license and CRL signing.
 * Usage: npx tsx scripts/keygen.ts
 */
import { generateKeypair } from '../lib/ed25519';

async function main() {
  console.log('Generating Ed25519 keypairs...\n');

  const licenseKeys = await generateKeypair();
  console.log('📝 License Signing Keys:');
  console.log(`LICENSE_SIGNING_PRIVATE_B64="${licenseKeys.privateKey}"`);
  console.log(`LICENSE_SIGNING_PUBLIC_B64="${licenseKeys.publicKey}"`);

  console.log('\n📝 CRL Signing Keys (can reuse license keys or generate separate):');
  const crlKeys = await generateKeypair();
  console.log(`CRL_SIGNING_PRIVATE_B64="${crlKeys.privateKey}"`);
  console.log(`CRL_SIGNING_PUBLIC_B64="${crlKeys.publicKey}"`);

  console.log('\n✅ Add these to your .env.local file');
  console.log('⚠️  Keep private keys SECRET and never commit them to version control');
}

main().catch(console.error);
