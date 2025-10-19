import { NextRequest, NextResponse } from 'next/server'
import { randomBytes } from 'crypto'
import { prisma } from '@/lib/prisma'

/**
 * ADMIN ENDPOINT: Generate test licenses
 *
 * SECURITY NOTES:
 * - Only works in development (NODE_ENV !== 'production')
 * - Requires ADMIN_SECRET environment variable
 * - Logs all license generations for audit trail
 * - Rate limited via Vercel (10 req/min per IP)
 *
 * WARNING: This endpoint should NEVER be enabled in production!
 * For production license generation, use Stripe webhooks only.
 */

function generateLicenseKey(): string {
  // Generate format: VL-XXXX-XXXX-XXXX-XXXX
  const segments = []
  for (let i = 0; i < 4; i++) {
    const segment = randomBytes(2).toString('hex').toUpperCase()
    segments.push(segment)
  }
  return `VL-${segments.join('-')}`
}

function isValidEmail(email: string): boolean {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return emailRegex.test(email)
}

export async function POST(request: NextRequest) {
  try {
    // SECURITY: Disable in production
    if (process.env.NODE_ENV === 'production') {
      console.error('⚠️  SECURITY ALERT: Admin license generation attempted in production!')
      return NextResponse.json(
        { error: 'This endpoint is disabled in production. Use Stripe webhooks to generate licenses.' },
        { status: 403 }
      )
    }

    // SECURITY: Check for admin secret
    const adminSecret = request.headers.get('x-admin-secret')
    const expectedSecret = process.env.ADMIN_SECRET

    if (!expectedSecret) {
      console.error('❌ ADMIN_SECRET not configured in environment')
      return NextResponse.json(
        { error: 'Server configuration error' },
        { status: 500 }
      )
    }

    if (!adminSecret || adminSecret !== expectedSecret) {
      console.warn('⚠️  Unauthorized admin endpoint access attempt')
      return NextResponse.json(
        { error: 'Unauthorized - Invalid admin secret' },
        { status: 401 }
      )
    }

    // Parse and validate request body
    const body = await request.json()
    const email = body.email || 'test@example.com'

    if (!isValidEmail(email)) {
      return NextResponse.json(
        { error: 'Invalid email format' },
        { status: 400 }
      )
    }

    // Generate unique license key
    const licenseKey = generateLicenseKey()

    // Check for duplicate key (extremely rare, but possible)
    const existing = await prisma.license.findUnique({
      where: { licenseKey: licenseKey }
    })

    if (existing) {
      // Retry with new key
      console.warn(`⚠️  Duplicate license key generated: ${licenseKey}. Retrying...`)
      return NextResponse.json(
        { error: 'Key collision detected. Please retry.' },
        { status: 500 }
      )
    }

    // Create license in database
    const license = await prisma.license.create({
      data: {
        licenseKey: licenseKey,
        email: email,
        type: 'LIFETIME',
        status: 'ACTIVE',
        // createdAt and updatedAt are set automatically by Prisma
      },
    })

    // AUDIT LOG
    console.log('✅ Test license generated:', {
      key: license.licenseKey,
      email: license.email,
      timestamp: new Date().toISOString(),
      environment: process.env.NODE_ENV,
    })

    return NextResponse.json({
      success: true,
      message: 'Test license generated successfully',
      license: {
        key: license.licenseKey,
        email: license.email,
        type: license.type,
        status: license.status,
      },
    })
  } catch (error) {
    console.error('❌ Error generating test license:', error)
    return NextResponse.json(
      {
        error: 'Failed to generate license',
        details: error instanceof Error ? error.message : 'Unknown error'
      },
      { status: 500 }
    )
  }
}
