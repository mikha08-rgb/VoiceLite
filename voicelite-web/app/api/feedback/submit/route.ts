import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { z } from 'zod';
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';
import { headers } from 'next/headers';
import { validateOrigin, getCsrfErrorResponse } from '@/lib/csrf';

// Rate limiting: 5 feedback submissions per hour per IP
const ratelimit = new Ratelimit({
  redis: Redis.fromEnv(),
  limiter: Ratelimit.slidingWindow(5, '1 h'),
  analytics: true,
});

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
  // CSRF protection: Validate request origin
  if (!validateOrigin(req)) {
    return NextResponse.json(getCsrfErrorResponse(), { status: 403 });
  }

  try {
    // Rate limiting: 5 feedback submissions per hour per IP
    const ip = req.headers.get('x-forwarded-for') || req.headers.get('x-real-ip') || 'unknown';

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

    // Parse and validate request body
    const body = await req.json();
    const validatedData = feedbackSchema.parse(body);

    // Get user agent and IP for tracking
    const headersList = await headers();
    const userAgent = headersList.get('user-agent') || undefined;
    const ipAddress = ip !== 'unknown' ? ip : undefined;

    // Check if user is authenticated (optional)
    const sessionCookie = req.cookies.get('session');
    let userId: string | undefined;

    if (sessionCookie) {
      try {
        // Verify session and get userId if valid
        const session = await prisma.session.findUnique({
          where: { sessionHash: sessionCookie.value },
          select: { userId: true, expiresAt: true, revokedAt: true },
        });

        if (session && session.expiresAt > new Date() && !session.revokedAt) {
          userId = session.userId;
        }
      } catch (error) {
        // Session verification failed, continue as anonymous
        console.error('Session verification failed:', error);
      }
    }

    // Create feedback entry
    const feedback = await prisma.feedback.create({
      data: {
        userId,
        email: validatedData.email || null,
        type: validatedData.type,
        subject: validatedData.subject,
        message: validatedData.message,
        metadata: validatedData.metadata ? JSON.stringify(validatedData.metadata) : null,
        status: 'OPEN',
        priority: validatedData.type === 'BUG' ? 'HIGH' : 'MEDIUM',
      },
    });

    // Track activity if user is authenticated
    if (userId) {
      await prisma.userActivity.create({
        data: {
          userId,
          activityType: 'FEEDBACK_SUBMITTED',
          metadata: JSON.stringify({
            feedbackId: feedback.id,
            type: validatedData.type,
          }),
          ipAddress,
          userAgent,
        },
      });
    }

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
