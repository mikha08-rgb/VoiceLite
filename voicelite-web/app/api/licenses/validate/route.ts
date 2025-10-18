import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getLicenseByKey } from '@/lib/licensing';
import { LicenseStatus } from '@prisma/client';
import { validationRateLimit, checkRateLimit, getClientIp } from '@/lib/ratelimit';

/**
 * POST /api/licenses/validate
 *
 * Simple license validation endpoint for desktop app.
 * No authentication required - the license key itself is the auth.
 * Returns whether the license is valid and what features it unlocks.
 *
 * SECURITY: Rate limited to 100 requests per hour per IP to prevent brute force
 */

const bodySchema = z.object({
  licenseKey: z.string().min(10, 'License key must be at least 10 characters'),
});

export async function POST(request: NextRequest) {
  // SECURITY FIX: Add rate limiting to prevent brute force license key enumeration
  const clientIp = getClientIp(request);
  const rateLimitResult = await checkRateLimit(clientIp, validationRateLimit);

  if (!rateLimitResult.allowed) {
    return NextResponse.json(
      {
        valid: false,
        error: 'Too many validation attempts',
        message: 'Please wait before trying again.',
        retryAfter: rateLimitResult.reset.toISOString(),
      },
      {
        status: 429,
        headers: {
          'X-RateLimit-Limit': rateLimitResult.limit.toString(),
          'X-RateLimit-Remaining': rateLimitResult.remaining.toString(),
          'X-RateLimit-Reset': rateLimitResult.reset.getTime().toString(),
          'Retry-After': Math.ceil((rateLimitResult.reset.getTime() - Date.now()) / 1000).toString(),
        },
      }
    );
  }

  try {
    const body = await request.json();
    const { licenseKey } = bodySchema.parse(body);

    // Lookup license in database
    const license = await getLicenseByKey(licenseKey);

    if (!license) {
      return NextResponse.json(
        {
          valid: false,
          error: 'License key not found',
        },
        { status: 404 }
      );
    }

    // Check if license is active
    // Note: We only support LIFETIME licenses currently, no expiration
    const valid = license.status === LicenseStatus.ACTIVE;

    return NextResponse.json({
      valid,
      status: license.status,
      type: license.type,
      // Email removed for privacy - not needed by desktop app
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { valid: false, error: 'Invalid request', details: error.issues },
        { status: 400 }
      );
    }

    console.error('License validation error:', error);
    return NextResponse.json(
      { valid: false, error: 'Internal server error' },
      { status: 500 }
    );
  }
}