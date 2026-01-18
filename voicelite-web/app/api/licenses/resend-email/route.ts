import { NextRequest, NextResponse } from 'next/server';
import { ipAddress } from '@vercel/edge';
import { z } from 'zod';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { prisma } from '@/lib/prisma';
import { recordLicenseEvent } from '@/lib/licensing';
import { emailResendRateLimit, fallbackEmailResendLimit } from '@/lib/ratelimit';
import { logger } from '@/lib/logger';

import { LicenseStatus } from '@prisma/client';

// HIGH-12 FIX: Add Zod schema validation for request body
const bodySchema = z.object({
  email: z.string().email('Invalid email address').optional(),
  licenseKey: z.string().min(10, 'Invalid license key format').max(100, 'License key too long').optional(),
}).refine(data => data.email || data.licenseKey, {
  message: 'Either email or licenseKey is required',
});
/**
 * Manual License Email Resend API
 *
 * Allows manually resending a license email for support cases
 * where the initial email failed or was lost
 *
 * POST /api/licenses/resend-email
 * Body: { email: string } or { licenseKey: string }
 */
export async function POST(request: NextRequest) {
  // Rate limiting by IP to prevent enumeration attacks
  // Use Vercel's trusted IP detection (prevents X-Forwarded-For spoofing)
  const ip = ipAddress(request) || 'unknown';

  // Check rate limit
  if (emailResendRateLimit) {
    const { success } = await emailResendRateLimit.limit(ip);
    if (!success) {
      return NextResponse.json(
        { error: 'Too many requests. Please try again later.' },
        { status: 429 }
      );
    }
  } else {
    // Fallback to in-memory rate limiter
    const allowed = await fallbackEmailResendLimit.check(ip);
    if (!allowed) {
      return NextResponse.json(
        { error: 'Too many requests. Please try again later.' },
        { status: 429 }
      );
    }
  }

  try {
    const body = await request.json();
    // HIGH-12 FIX: Validate with Zod schema
    const parseResult = bodySchema.safeParse(body);
    if (!parseResult.success) {
      return NextResponse.json(
        { error: parseResult.error.errors[0]?.message || 'Invalid request' },
        { status: 400 }
      );
    }
    const { email, licenseKey } = parseResult.data;

    // Find the license by email or license key
    const license = await prisma.license.findFirst({
      where: email
        ? { email: email }
        : { licenseKey: licenseKey },
      orderBy: {
        createdAt: 'desc', // Get most recent if multiple exist
      },
    });

    // Add small random delay to prevent timing attacks (50-150ms)
    await new Promise(resolve => setTimeout(resolve, 50 + Math.random() * 100));

    // Use generic message to prevent email enumeration
    if (!license) {
      return NextResponse.json(
        {
          success: true,
          message: 'If this license exists, an email will be sent shortly.'
        },
        { status: 200 }
      );
    }

    // Check if license is active (but still return generic message)
    if (license.status !== LicenseStatus.ACTIVE) {
      return NextResponse.json(
        {
          success: true,
          message: 'If this license exists, an email will be sent shortly.'
        },
        { status: 200 }
      );
    }

    // Send the email
    logger.info('Manual resend requested', { licenseId: license.id, email: license.email });

    const emailResult = await sendLicenseEmail({
      email: license.email,
      licenseKey: license.licenseKey,
    });

    if (emailResult.success) {
      // Record the manual resend event
      await recordLicenseEvent(license.id, 'email_resent_manual', {
        messageId: emailResult.messageId,
        email: license.email,
        requestedAt: new Date().toISOString(),
      });

      return NextResponse.json({
        success: true,
        message: 'License email resent successfully',
        email: license.email,
        messageId: emailResult.messageId,
      });
    } else {
      // Record the failure
      await recordLicenseEvent(license.id, 'email_resend_failed', {
        error: emailResult.error instanceof Error
          ? emailResult.error.message
          : String(emailResult.error),
        email: license.email,
      });

      // Don't expose detailed error in production
      return NextResponse.json(
        { error: 'Failed to send email. Please try again later.' },
        { status: 500 }
      );
    }
  } catch (error) {
    logger.error('Error in resend-email endpoint', error);
    // Don't expose error details in production
    return NextResponse.json(
      { error: 'Internal server error. Please try again later.' },
      { status: 500 }
    );
  }
}