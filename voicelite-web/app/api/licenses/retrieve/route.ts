import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { ipAddress } from '@vercel/edge';
import { prisma } from '@/lib/prisma';
import { checkRateLimit, emailResendRateLimit, fallbackEmailResendLimit } from '@/lib/ratelimit';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { recordLicenseEvent } from '@/lib/licensing';

const bodySchema = z.object({
  email: z.string().email('Invalid email address'),
});

/**
 * Self-service license retrieval endpoint.
 * Customers can request their license key be resent to their email.
 * Rate limited to prevent enumeration and spam.
 */
export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { email } = bodySchema.parse(body);
    const normalizedEmail = email.toLowerCase().trim();

    // Rate limit by IP (3 requests/hour)
    const ip = ipAddress(request) || 'unknown';
    const rateLimit = await checkRateLimit(ip, emailResendRateLimit);

    // Fallback to in-memory if Upstash not configured
    if (!emailResendRateLimit) {
      const allowed = await fallbackEmailResendLimit.check(ip);
      if (!allowed) {
        return NextResponse.json(
          { error: 'Too many requests. Please try again later.' },
          { status: 429 }
        );
      }
    } else if (!rateLimit.allowed) {
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

    // Always return same response to prevent email enumeration
    const successMessage = 'If a license exists for this email, you will receive it shortly. Please check your spam folder.';

    if (!license) {
      console.log(`📧 License retrieval: No license found for ${normalizedEmail}`);
      // Don't reveal that no license exists
      return NextResponse.json({ success: true, message: successMessage });
    }

    // Send the license email
    const emailResult = await sendLicenseEmail({
      email: license.email,
      licenseKey: license.licenseKey,
    });

    if (emailResult.success) {
      console.log(`✅ License retrieved and sent to ${normalizedEmail}`);
      await recordLicenseEvent(license.id, 'email_resent', {
        email: license.email,
        messageId: emailResult.messageId,
        source: 'self_service_retrieval',
        ip,
      });
    } else {
      console.error(`❌ Failed to send license retrieval email to ${normalizedEmail}:`, emailResult.error);
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

    console.error('License retrieval failed:', error);
    return NextResponse.json({ error: 'Unable to process request' }, { status: 500 });
  }
}
