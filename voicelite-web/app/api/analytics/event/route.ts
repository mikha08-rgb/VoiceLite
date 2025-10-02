import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';
import { randomUUID } from 'crypto';

// Rate limiting: 100 events/hour per anonymousUserId
const ratelimit = new Ratelimit({
  redis: Redis.fromEnv(),
  limiter: Ratelimit.slidingWindow(100, '1 h'),
  analytics: true,
  prefix: '@upstash/ratelimit/analytics',
});

// Validation schema for analytics events
const analyticsEventSchema = z.object({
  anonymousUserId: z.string().min(32).max(128), // SHA256 hash
  eventType: z.enum([
    'APP_LAUNCHED',
    'TRANSCRIPTION_COMPLETED',
    'MODEL_CHANGED',
    'SETTINGS_CHANGED',
    'ERROR_OCCURRED',
    'PRO_UPGRADE',
  ]),
  tier: z.enum(['FREE', 'PRO']).default('FREE'),
  appVersion: z.string().max(20).optional(),
  osVersion: z.string().max(50).optional(),
  modelUsed: z.string().max(50).optional(),
  metadata: z.record(z.any()).optional(), // JSON object
});

export async function POST(req: NextRequest) {
  try {
    // Parse and validate request body
    const body = await req.json();
    const validatedData = analyticsEventSchema.parse(body);

    // Rate limiting check
    const { success, limit, remaining, reset } = await ratelimit.limit(
      validatedData.anonymousUserId
    );

    if (!success) {
      return NextResponse.json(
        {
          error: 'Rate limit exceeded',
          limit,
          remaining,
          reset: new Date(reset).toISOString(),
        },
        {
          status: 429,
          headers: {
            'X-RateLimit-Limit': limit.toString(),
            'X-RateLimit-Remaining': remaining.toString(),
            'X-RateLimit-Reset': reset.toString(),
          },
        }
      );
    }

    // Extract IP for geo analytics (optional)
    const ipAddress =
      req.headers.get('x-forwarded-for')?.split(',')[0]?.trim() ||
      req.headers.get('x-real-ip') ||
      null;

    // Store analytics event
    await prisma.analyticsEvent.create({
      data: {
        id: randomUUID(),
        anonymousUserId: validatedData.anonymousUserId,
        eventType: validatedData.eventType,
        tier: validatedData.tier,
        appVersion: validatedData.appVersion,
        osVersion: validatedData.osVersion,
        modelUsed: validatedData.modelUsed,
        metadata: validatedData.metadata ? JSON.stringify(validatedData.metadata) : null,
        ipAddress: null, // Privacy: Don't log IP addresses
      },
    });

    // Return success (no response body needed for analytics)
    return NextResponse.json(
      { success: true },
      {
        status: 200,
        headers: {
          'X-RateLimit-Limit': limit.toString(),
          'X-RateLimit-Remaining': remaining.toString(),
          'X-RateLimit-Reset': reset.toString(),
        },
      }
    );
  } catch (error) {
    // Validation errors
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        {
          error: 'Invalid request body',
          details: error.errors,
        },
        { status: 400 }
      );
    }

    // Log server errors but don't expose details
    console.error('Analytics event error:', error);
    return NextResponse.json(
      { error: 'Failed to process analytics event' },
      { status: 500 }
    );
  }
}
