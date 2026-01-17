import { NextRequest, NextResponse } from 'next/server';
import { ipAddress } from '@vercel/edge';
import { prisma } from '@/lib/prisma';
import { z } from 'zod';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';
import { logger } from '@/lib/logger';

// Lazy initialization of rate limiter to allow builds without env vars
function getRateLimiter(): Ratelimit | null {
  const url = process.env.UPSTASH_REDIS_REST_URL;
  const token = process.env.UPSTASH_REDIS_REST_TOKEN;

  if (!url || !token) {
    return null;
  }

  return new Ratelimit({
    redis: new Redis({ url, token }),
    limiter: Ratelimit.slidingWindow(5, '1 h'),
    analytics: true,
  });
}

const feedbackSchema = z.object({
  type: z.enum(['BUG', 'FEATURE_REQUEST', 'GENERAL', 'QUESTION']),
  subject: z.string().min(5).max(200),
  message: z.string().min(10).max(5000),
  email: z.string().email().optional().or(z.literal('')),
  metadata: z.object({
    appVersion: z.string().optional(),
    osVersion: z.string().optional(),
    browser: z.string().optional(),
    url: z.string().optional(),
  }).optional(),
});

export async function POST(req: NextRequest) {
  try {
    // Rate limiting: 5 feedback submissions per hour per IP
    // Use Vercel's trusted IP detection (prevents X-Forwarded-For spoofing)
    const ip = ipAddress(req) || 'unknown';

    const ratelimit = getRateLimiter();
    if (ratelimit) {
      const { success, limit, remaining, reset } = await ratelimit.limit(`feedback:${ip}`);
      if (!success) {
        return NextResponse.json(
          { error: 'Rate limit exceeded. Please try again later.' },
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
    } else {
      // Development/fallback: log warning but allow request
      logger.warn('Rate limiter not configured, allowing feedback request');
    }

    // Parse and validate request body
    const body = await req.json();
    const validatedData = feedbackSchema.parse(body);

    // Create feedback entry
    const feedback = await prisma.feedback.create({
      data: {
        email: validatedData.email || null,
        type: validatedData.type,
        subject: validatedData.subject,
        message: validatedData.message,
        metadata: validatedData.metadata ? JSON.stringify(validatedData.metadata) : null,
        status: 'OPEN',
        priority: validatedData.type === 'BUG' ? 'HIGH' : 'MEDIUM',
      },
    });

    return NextResponse.json(
      {
        success: true,
        message: 'Feedback submitted successfully. Thank you!',
        feedbackId: feedback.id,
      },
      {
        status: 201,
      }
    );
  } catch (error) {
    logger.error('Feedback submission error', error);

    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: 'Invalid feedback data', details: error.issues },
        { status: 400 }
      );
    }

    return NextResponse.json(
      { error: 'Failed to submit feedback. Please try again.' },
      { status: 500 }
    );
  }
}
