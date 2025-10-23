import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  console.log('Checking recent licenses...\n');

  const licenses = await prisma.license.findMany({
    include: {
      user: {
        select: {
          email: true
        }
      }
    },
    orderBy: {
      createdAt: 'desc'
    },
    take: 5
  });

  console.log(`Found ${licenses.length} licenses:\n`);

  for (const license of licenses) {
    console.log(`License Key: ${license.licenseKey}`);
    console.log(`Email: ${license.user.email}`);
    console.log(`Status: ${license.status}`);
    console.log(`Type: ${license.type}`);
    console.log(`Created: ${license.createdAt}`);
    console.log('---\n');
  }

  // Check webhook events
  const webhookEvents = await prisma.webhookEvent.findMany({
    orderBy: {
      createdAt: 'desc'
    },
    take: 5
  });

  console.log(`Found ${webhookEvents.length} webhook events:\n`);

  for (const event of webhookEvents) {
    console.log(`Event ID: ${event.eventId}`);
    console.log(`Created: ${event.createdAt}`);
    console.log('---\n');
  }
}

main()
  .catch(console.error)
  .finally(() => prisma.$disconnect());
