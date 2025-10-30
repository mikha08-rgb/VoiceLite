require('dotenv').config({ path: '.env.local' });
const { PrismaClient } = require('@prisma/client');

async function runMigration() {
  const prisma = new PrismaClient();

  try {
    console.log('Creating composite index...');

    await prisma.$executeRawUnsafe(
      'CREATE INDEX IF NOT EXISTS "LicenseActivation_licenseId_status_idx" ON "LicenseActivation"("licenseId", "status");'
    );

    console.log('✅ Migration complete: Composite index created');
  } catch (error) {
    console.error('❌ Migration failed:', error.message);
    process.exit(1);
  } finally {
    await prisma.$disconnect();
  }
}

runMigration();
