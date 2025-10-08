import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

// Rate limiting: 50 batch uploads/hour per anonymousUserId (500 metrics total)
const ratelimit = new Ratelimit({
  redis: Redis.fromEnv(),
  limiter: Ratelimit.slidingWindow(50, '1 h'),
  analytics: true,
  prefix: '@upstash/ratelimit/metrics',
});

// Validation schema for telemetry metric
const telemetryMetricSchema = z.object({
  timestamp: z.string().datetime(),
  anonymousUserId: z.string().min(32).max(128), // SHA256 hash
  metricType: z.string().min(1).max(100),
  value: z.number(),
  metadata: z.record(z.string(), z.any()).optional(),
});

// Validation schema for batch upload
const batchUploadSchema = z.object({
  metrics: z.array(telemetryMetricSchema).min(1).max(100), // Max 100 metrics per batch
});

export async function POST(req: NextRequest) {
  try {
    // Parse and validate request body
    const body = await req.json();
    const validatedData = batchUploadSchema.parse(body);

    // Get anonymousUserId from first metric (all should be same user)
    const anonymousUserId = validatedData.metrics[0].anonymousUserId;

    // Rate limiting check
    const { success, limit, remaining, reset } = await ratelimit.limit(anonymousUserId);

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

    // Batch insert telemetry metrics
    await prisma.telemetryMetric.createMany({
      data: validatedData.metrics.map((metric) => ({
        anonymousUserId: metric.anonymousUserId,
        metricType: metric.metricType,
        value: metric.value,
        metadata: metric.metadata ? JSON.stringify(metric.metadata) : null,
        timestamp: new Date(metric.timestamp),
      })),
      skipDuplicates: true, // Prevent duplicate metric insertion
    });

    // Return success
    return NextResponse.json(
      {
        success: true,
        metricsUploaded: validatedData.metrics.length,
      },
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
          details: error.issues,
        },
        { status: 400 }
      );
    }

    // Log server errors but don't expose details
    console.error('Metrics upload error:', error);
    return NextResponse.json(
      { error: 'Failed to process metrics upload' },
      { status: 500 }
    );
  }
}
