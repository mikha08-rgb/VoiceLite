import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  console.log('Seeding database...');

  // Create products
  const proProduct = await prisma.product.upsert({
    where: { id: 'voicelite-pro' },
    update: {},
    create: {
      id: 'voicelite-pro',
      name: 'VoiceLite Pro',
      plan: 'pro',
      seatsDefault: 1,
    },
  });

  const lifetimeProduct = await prisma.product.upsert({
    where: { id: 'voicelite-lifetime' },
    update: {},
    create: {
      id: 'voicelite-lifetime',
      name: 'VoiceLite Lifetime',
      plan: 'lifetime',
      seatsDefault: 1,
    },
  });

  console.log('✅ Created products:', proProduct.id, lifetimeProduct.id);
  console.log('✅ Seed completed successfully');
}

main()
  .then(async () => {
    await prisma.$disconnect();
  })
  .catch(async (e) => {
    console.error('❌ Seed failed:', e);
    await prisma.$disconnect();
    process.exit(1);
  });
