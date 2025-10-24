import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { z } from 'zod';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';
import { headers } from 'next/headers';

// Lazy rate limiter initialization (deferred until first API call)
// Environment validation ensures Redis credentials exist at runtime
let feedbackRateLimit: Ratelimit | null = null;

function getRateLimiter(): Ratelimit {
  if (!feedbackRateLimit) {
    const redis = new Redis({
      url: process.env.UPSTASH_REDIS_REST_URL!,
      token: process.env.UPSTASH_REDIS_REST_TOKEN!,
    });
    feedbackRateLimit = new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(5, '1 h'),
      analytics: true,
      prefix: 'ratelimit:feedback',
    });
  }
  return feedbackRateLimit;
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
    const ip = req.headers.get('x-forwarded-for') || req.headers.get('x-real-ip') || 'unknown';

    const { success, limit, remaining, reset } = await getRateLimiter().limit(`feedback:${ip}`);
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
    console.error('Feedback submission error:', error);

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
