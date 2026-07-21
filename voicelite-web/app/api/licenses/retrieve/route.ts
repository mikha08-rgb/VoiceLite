import { NextRequest, NextResponse } from 'next/server';
import { randomUUID } from 'node:crypto';
import { z } from 'zod';
import { ipAddress } from '@vercel/edge';
import { prisma } from '@/lib/prisma';
import { checkRateLimit, emailResendRateLimit, fallbackEmailResendLimit } from '@/lib/ratelimit';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { recordLicenseEvent } from '@/lib/licensing';
import { logger } from '@/lib/logger';

const bodySchema = z.object({
  email: z.string().email('Invalid email address'),
});

/**
 * Self-service license retrieval endpoint.
 * Customers can request their license key be resent to their email.
 * Rate limited to prevent enumeration and spam.
 */
export async function POST(request: NextRequest) {
  const requestId = request.headers.get('x-request-id') ?? randomUUID();

  try {
    const body = await request.json();
    const { email } = bodySchema.parse(body);
    const normalizedEmail = email.toLowerCase().trim();

    // Rate limit by IP (3 requests/hour). Passing the fallback limiter makes a
    // Redis outage degrade to in-memory limiting instead of failing closed
    // (checkRateLimit with no fallback blocks ALL requests on Redis failure).
    const ip = ipAddress(request) || 'unknown';
    const rateLimit = await checkRateLimit(ip, emailResendRateLimit, fallbackEmailResendLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json(
        { error: 'Too many requests. Please try again later.' },
        { status: 429 }
      );
    }

    // Find most recent license for this email
    const license = await prisma.license.findFirst({
      where: { email: normalizedEmail },
      orderBy: { createdAt: 'desc' },
      select: {
        id: true,
        licenseKey: true,
        email: true,
        status: true,
      },
    });

    // HIGH-10 FIX: Add small random delay to prevent timing attacks (50-150ms)
    // This ensures response time is consistent regardless of whether license exists
    await new Promise(resolve => setTimeout(resolve, 50 + Math.random() * 100));

    // Always return same response to prevent email enumeration
    const successMessage = 'If a license exists for this email, you will receive it shortly. Please check your spam folder.';

    if (!license) {
      logger.info('License retrieval found no matching license', { requestId });
      // Don't reveal that no license exists
      return NextResponse.json({ success: true, message: successMessage });
    }

    // Send the license email
    const emailResult = await sendLicenseEmail({
      email: license.email,
      licenseKey: license.licenseKey,
      licenseId: license.id,
      requestId,
    });

    if (emailResult.success) {
      logger.info('License retrieval email sent', {
        requestId,
        licenseId: license.id,
        messageId: emailResult.messageId,
      });
      await recordLicenseEvent(license.id, 'email_resent', {
        email: license.email,
        messageId: emailResult.messageId,
        source: 'self_service_retrieval',
        ip,
      });
    } else {
      logger.error('Failed to send license retrieval email', emailResult.error, {
        requestId,
        licenseId: license.id,
      });
      await recordLicenseEvent(license.id, 'email_failed', {
        email: license.email,
        error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
        source: 'self_service_retrieval',
        ip,
      });
    }

    return NextResponse.json({ success: true, message: successMessage });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid email address' }, { status: 400 });
    }

    logger.error('License retrieval failed', error, { requestId });
    return NextResponse.json({ error: 'Unable to process request' }, { status: 500 });
  }
}
