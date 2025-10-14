import { PrismaClient } from '@prisma/client';
import { nanoid } from 'nanoid';

const prisma = new PrismaClient();

async function main() {
  console.log('🎫 Creating test license...\n');

  // Generate a unique license key
  const licenseKey = `VL-${nanoid(6).toUpperCase()}-${nanoid(6).toUpperCase()}-${nanoid(6).toUpperCase()}`;

  // Create a test user
  const user = await prisma.user.upsert({
    where: { email: 'test@voicelite.local' },
    update: {},
    create: {
      email: 'test@voicelite.local',
    },
  });

  console.log('✅ Test user:', user.email);

  // Create the license
  const license = await prisma.license.create({
    data: {
      licenseKey: licenseKey,
      userId: user.id,
      type: 'LIFETIME',
      status: 'ACTIVE',
    },
  });

  console.log('\n🎉 License created successfully!\n');
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
  console.log('📋 License Key:  ', license.licenseKey);
  console.log('👤 User Email:   ', user.email);
  console.log('💼 Type:         ', license.type);
  console.log('✅ Status:       ', license.status);
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n');
  console.log('Copy the license key above and paste it into VoiceLite Settings → Pro License\n');
}

main()
  .then(async () => {
    await prisma.$disconnect();
  })
  .catch(async (e) => {
    console.error('❌ Failed to create license:', e);
    await prisma.$disconnect();
    process.exit(1);
  });
