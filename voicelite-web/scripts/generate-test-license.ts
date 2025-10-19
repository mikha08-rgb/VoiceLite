/**
 * Generate a test license key directly in the database
 * Usage: npx tsx scripts/generate-test-license.ts
 */

import { PrismaClient } from '@prisma/client'
import { randomBytes } from 'crypto'

const prisma = new PrismaClient()

function generateLicenseKey(): string {
  // Generate format: VL-XXXX-XXXX-XXXX-XXXX
  const segments = []
  for (let i = 0; i < 4; i++) {
    const segment = randomBytes(2).toString('hex').toUpperCase()
    segments.push(segment)
  }
  return `VL-${segments.join('-')}`
}

async function main() {
  const email = process.argv[2] || 'test@example.com'
  const licenseKey = generateLicenseKey()

  console.log('\n🔑 Generating test license...\n')

  const license = await prisma.license.create({
    data: {
      licenseKey: licenseKey,
      email: email,
      type: 'LIFETIME',
      status: 'ACTIVE',
      // createdAt and updatedAt are set automatically by Prisma
    },
  })

  console.log('✅ Test license created successfully!\n')
  console.log('License Details:')
  console.log('================')
  console.log(`Email:        ${license.email}`)
  console.log(`License Key:  ${license.licenseKey}`)
  console.log(`Type:         ${license.type}`)
  console.log(`Status:       ${license.status}`)
  console.log('\n📋 Next Steps:')
  console.log('1. Copy the license key above')
  console.log('2. Launch VoiceLite app')
  console.log('3. Enter the license key when prompted')
  console.log('4. The app will activate it on your machine\n')

  await prisma.$disconnect()
}

main().catch((error) => {
  console.error('❌ Error generating license:', error)
  process.exit(1)
})
