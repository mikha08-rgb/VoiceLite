// Generate New Ed25519 Keypairs for VoiceLite
// Run: node generate-new-keys.js

const crypto = require('crypto');

console.log('==========================================');
console.log('ED25519 KEYPAIR GENERATOR');
console.log('==========================================');
console.log('');

// Generate License Signing Keypair
console.log('[1/3] Generating LICENSE signing keypair...');
const licenseKeypair = crypto.generateKeyPairSync('ed25519', {
  publicKeyEncoding: { type: 'spki', format: 'der' },
  privateKeyEncoding: { type: 'pkcs8', format: 'der' }
});

const licensePrivate = licenseKeypair.privateKey.toString('base64');
const licensePublic = licenseKeypair.publicKey.toString('base64');

console.log('✓ Generated');
console.log('');

// Generate CRL Signing Keypair
console.log('[2/3] Generating CRL signing keypair...');
const crlKeypair = crypto.generateKeyPairSync('ed25519', {
  publicKeyEncoding: { type: 'spki', format: 'der' },
  privateKeyEncoding: { type: 'pkcs8', format: 'der' }
});

const crlPrivate = crlKeypair.privateKey.toString('base64');
const crlPublic = crlKeypair.publicKey.toString('base64');

console.log('✓ Generated');
console.log('');

// Generate Migration Secret
console.log('[3/3] Generating MIGRATION secret (256-bit)...');
const migrationSecret = crypto.randomBytes(32).toString('hex');

console.log('✓ Generated');
console.log('');

// Display results
console.log('==========================================');
console.log('NEW CREDENTIALS - COPY THESE TO VERCEL');
console.log('==========================================');
console.log('');

console.log('1. LICENSE SIGNING KEYS');
console.log('------------------------');
console.log('LICENSE_SIGNING_PRIVATE_B64=');
console.log(licensePrivate);
console.log('');
console.log('LICENSE_SIGNING_PUBLIC_B64=');
console.log(licensePublic);
console.log('');

console.log('2. CRL SIGNING KEYS');
console.log('-------------------');
console.log('CRL_SIGNING_PRIVATE_B64=');
console.log(crlPrivate);
console.log('');
console.log('CRL_SIGNING_PUBLIC_B64=');
console.log(crlPublic);
console.log('');

console.log('3. MIGRATION SECRET');
console.log('-------------------');
console.log('MIGRATION_SECRET=');
console.log(migrationSecret);
console.log('');

// Save to file
const fs = require('fs');
const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
const filename = `NEW_CREDENTIALS_${timestamp}.txt`;

const fileContent = `# NEW VOICELITE CREDENTIALS
# Generated: ${new Date().toISOString()}

# ===========================================
# VERCEL ENVIRONMENT VARIABLES
# ===========================================

LICENSE_SIGNING_PRIVATE_B64="${licensePrivate}"
LICENSE_SIGNING_PUBLIC_B64="${licensePublic}"
CRL_SIGNING_PRIVATE_B64="${crlPrivate}"
CRL_SIGNING_PUBLIC_B64="${crlPublic}"
MIGRATION_SECRET="${migrationSecret}"

# ===========================================
# DESKTOP APP UPDATE REQUIRED
# ===========================================

File: VoiceLite/VoiceLite/Services/LicenseValidator.cs

Find this line (around line 16):
    private const string LICENSE_PUBLIC_KEY_B64 = "...";

Replace with:
    private const string LICENSE_PUBLIC_KEY_B64 = "${licensePublic}";

Then rebuild:
    dotnet build VoiceLite/VoiceLite.sln -c Release

# ===========================================
# VERCEL DEPLOYMENT COMMANDS
# ===========================================

cd voicelite-web

echo "${licensePrivate}" | vercel env add LICENSE_SIGNING_PRIVATE_B64 production
echo "${licensePublic}" | vercel env add LICENSE_SIGNING_PUBLIC_B64 production
echo "${crlPrivate}" | vercel env add CRL_SIGNING_PRIVATE_B64 production
echo "${crlPublic}" | vercel env add CRL_SIGNING_PUBLIC_B64 production
echo "${migrationSecret}" | vercel env add MIGRATION_SECRET production

vercel deploy --prod

# ===========================================
# IMPORTANT NOTES
# ===========================================

1. Store this file in a PASSWORD MANAGER (1Password, LastPass, etc.)
2. NEVER commit this file to git
3. Delete this file after adding to Vercel
4. Update desktop app BEFORE releasing new version
5. All old licenses will become invalid after deployment

# ===========================================
# INVALIDATION NOTICE
# ===========================================

These credentials REPLACE the compromised keys:
- Old LICENSE_SIGNING_PUBLIC: fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc
- Old CRL_SIGNING_PUBLIC: 19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M

After deployment:
- All licenses signed with old keys will fail validation
- Users will need to re-purchase/re-download licenses
- CRL checks will use new signing key
`;

fs.writeFileSync(filename, fileContent);

console.log('==========================================');
console.log('CREDENTIALS SAVED TO FILE');
console.log('==========================================');
console.log('');
console.log(`File: ${filename}`);
console.log('');
console.log('⚠️  IMPORTANT:');
console.log('1. Copy this file to your password manager');
console.log('2. Add credentials to Vercel (see commands in file)');
console.log('3. Update LicenseValidator.cs with new public key');
console.log('4. DELETE this file after setup');
console.log('');
console.log('Next steps:');
console.log('1. Add to Vercel: Use commands in the file');
console.log('2. Update desktop app: See instructions in file');
console.log('3. Deploy backend: vercel deploy --prod');
console.log('4. Test license validation');
console.log('');
