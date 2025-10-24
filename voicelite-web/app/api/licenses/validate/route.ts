import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { checkRateLimit } from '@/lib/ratelimit';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

const bodySchema = z.object({
  licenseKey: z.string().min(10),
});

// Lazy rate limiter initialization (deferred until first API call)
// Prevents license key brute-forcing
// Environment validation ensures Redis credentials exist at runtime
let licenseValidationRateLimit: Ratelimit | null = null;

function getRateLimiter(): Ratelimit {
  if (!licenseValidationRateLimit) {
    const redis = new Redis({
      url: process.env.UPSTASH_REDIS_REST_URL!,
      token: process.env.UPSTASH_REDIS_REST_TOKEN!,
    });
    licenseValidationRateLimit = new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(5, '1 h'),
      analytics: true,
      prefix: 'ratelimit:license-validation',
    });
  }
  return licenseValidationRateLimit;
}

export async function POST(request: NextRequest) {
  try {
    // Rate limit by IP to prevent brute-forcing license keys
    const ip = request.headers.get('x-forwarded-for')?.split(',')[0].trim()
      || request.headers.get('x-real-ip')
      || 'unknown';

    const rateLimit = await checkRateLimit(ip, getRateLimiter());
    if (!rateLimit.allowed) {
      return NextResponse.json(
        { error: 'Too many validation attempts. Please try again later.' },
        { status: 429 }
      );
    }

    const body = await request.json();
    const { licenseKey } = bodySchema.parse(body);

    // Look up license in database
    const license = await prisma.license.findUnique({
      where: { licenseKey },
      select: {
        id: true,
        status: true,
        type: true,
        expiresAt: true,
      },
    });

    // If license doesn't exist or is not active, return invalid
    if (!license || license.status !== 'ACTIVE') {
      return NextResponse.json({
        valid: false,
        tier: 'free',
      });
    }

    // License is valid!
    return NextResponse.json({
      valid: true,
      tier: 'pro',
      license: {
        type: license.type,
        expiresAt: license.expiresAt,
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
