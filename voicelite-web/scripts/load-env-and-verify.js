require('dotenv').config({ path: '.env.local' });
require('child_process').execSync('npx tsx scripts/verify-production-readiness.ts', {
  stdio: 'inherit',
  env: process.env
});
