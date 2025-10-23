import { prisma } from '../lib/prisma';

async function main() {
  const licenses = await prisma.license.findMany({
    orderBy: { createdAt: 'desc' },
    take: 5,
    select: {
      licenseKey: true,
      createdAt: true,
      stripePaymentIntentId: true,
      user: {
        select: {
          email: true,
        },
      },
    },
  });

  console.log('Recent licenses:');
  console.log(JSON.stringify(licenses, null, 2));
}

main()
  .catch(console.error)
  .finally(() => prisma.$disconnect());