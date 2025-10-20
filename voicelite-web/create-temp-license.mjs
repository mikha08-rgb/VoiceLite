import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function createTempLicense() {
  try {
    const licenseKey = 'VL-TEMP-' + Date.now().toString(36).toUpperCase();

    const license = await prisma.license.create({
      data: {
        email: 'test@voicelite.local',
        licenseKey: licenseKey,
        type: 'LIFETIME',
        status: 'ACTIVE',
        maxDevices: 10,
        emailSent: true,
      }
    });

    console.log('✅ Temporary Pro License Created!\n');
    console.log('License Key:', licenseKey);
    console.log('Email:', license.email);
    console.log('Status:', license.status);
    console.log('Type:', license.type);
    console.log('Max Devices:', license.maxDevices);
    console.log('\n🎯 USE THIS LICENSE KEY IN THE APP:', licenseKey);

  } catch (error) {
    console.error('❌ Error creating license:', error.message);
    process.exit(1);
  } finally {
    await prisma.$disconnect();
  }
}

createTempLicense();
