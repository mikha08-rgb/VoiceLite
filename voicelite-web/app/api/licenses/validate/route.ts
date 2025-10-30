import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { checkRateLimit } from '@/lib/ratelimit';
import { recordLicenseActivation } from '@/lib/licensing';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

const bodySchema = z.object({
  licenseKey: z.string().min(10),
  machineId: z.string().min(10).max(100).optional(), // Required for device tracking
  machineLabel: z.string().max(255).optional(), // e.g., "John's Desktop"
  machineHash: z.string().max(64).optional(), // SHA-256 of hardware IDs
});

// Rate limiter: 5 validation attempts per hour per IP
// Prevents license key brute-forcing
const isConfigured = Boolean(
  process.env.UPSTASH_REDIS_REST_URL && process.env.UPSTASH_REDIS_REST_TOKEN
);

const redis = isConfigured
  ? new Redis({
      url: process.env.UPSTASH_REDIS_REST_URL!,
      token: process.env.UPSTASH_REDIS_REST_TOKEN!,
    })
  : null;

const licenseValidationRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(5, '1 h'),
      analytics: true,
      prefix: 'ratelimit:license-validation',
    })
  : null;

export async function POST(request: NextRequest) {
  try {
    // Rate limit by IP to prevent brute-forcing license keys
    const ip = request.headers.get('x-forwarded-for')?.split(',')[0].trim()
      || request.headers.get('x-real-ip')
      || 'unknown';

    const rateLimit = await checkRateLimit(ip, licenseValidationRateLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json(
        { error: 'Too many validation attempts. Please try again later.' },
        { status: 429 }
      );
    }

    const body = await request.json();
    const { licenseKey, machineId, machineLabel, machineHash } = bodySchema.parse(body);

    // Look up license in database
    const license = await prisma.license.findUnique({
      where: { licenseKey },
      select: {
        id: true,
        status: true,
        type: true,
        expiresAt: true,
        activations: {
          where: { status: 'ACTIVE' },
        },
      },
    });

    // If license doesn't exist or is not active, return invalid
    if (!license || license.status !== 'ACTIVE') {
      return NextResponse.json({
        valid: false,
        tier: 'free',
      });
    }

    // If machineId provided, record activation (enforces 3-device limit)
    if (machineId) {
      try {
        await recordLicenseActivation({
          licenseId: license.id,
          machineId,
          machineLabel,
          machineHash,
        });
      } catch (error: any) {
        // Check if it's the activation limit error
        if (error.message && error.message.startsWith('ACTIVATION_LIMIT_REACHED')) {
          return NextResponse.json({
            valid: false,
            tier: 'free',
            error: 'Maximum 3 device activations reached. Deactivate a device in your account to continue.',
            activationsUsed: 3,
            maxActivations: 3,
          }, { status: 403 });
        }
        throw error; // Re-throw other errors
      }
    }

    // License is valid!
    return NextResponse.json({
      valid: true,
      tier: 'pro',
      license: {
        type: license.type,
        expiresAt: license.expiresAt,
        activationsUsed: license.activations.length,
        maxActivations: 3,
      },
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }

    console.error('License validation failed:', error);
    return NextResponse.json({ error: 'Unable to validate license' }, { status: 500 });
  }
}
