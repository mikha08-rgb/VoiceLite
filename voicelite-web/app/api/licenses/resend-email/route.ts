import { NextRequest, NextResponse } from 'next/server';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { prisma } from '@/lib/prisma';
import { recordLicenseEvent } from '@/lib/licensing';
import { emailResendRateLimit, fallbackEmailResendLimit } from '@/lib/ratelimit';

import { LicenseStatus } from '@prisma/client';
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
  const ip = request.headers.get('x-forwarded-for')?.split(',').pop()?.trim()
    || request.headers.get('x-real-ip')
    || 'unknown';

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
    const { email, licenseKey } = body;

    if (!email && !licenseKey) {
      return NextResponse.json(
        { error: 'Either email or licenseKey is required' },
        { status: 400 }
      );
    }

    // Find the license by email or license key
    const license = await prisma.license.findFirst({
      where: email
        ? { email: email }
        : { licenseKey: licenseKey },
      orderBy: {
        createdAt: 'desc', // Get most recent if multiple exist
      },
    });

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
    console.log(`🔄 Manual resend requested for license ${license.id} (${license.email})`);

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

      return NextResponse.json(
        {
          error: 'Failed to send email',
          details: emailResult.error instanceof Error
            ? emailResult.error.message
            : 'Unknown error'
        },
        { status: 500 }
      );
    }
  } catch (error) {
    console.error('Error in resend-email endpoint:', error);
    return NextResponse.json(
      {
        error: 'Internal server error',
        message: error instanceof Error ? error.message : 'Unknown error'
      },
      { status: 500 }
    );
  }
}