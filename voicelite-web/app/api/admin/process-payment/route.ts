import { NextRequest, NextResponse } from 'next/server';
import { randomUUID } from 'node:crypto';
import { LicenseType } from '@prisma/client';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { upsertLicenseFromStripe, recordLicenseEvent } from '@/lib/licensing';
import { isAdminAuthenticated } from '@/lib/admin-auth';
import { logger } from '@/lib/logger';

// Same email format validation as the webhook route (CRITICAL-2 FIX there)
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  const requestId = request.headers.get('x-request-id') ?? randomUUID();

  // Authenticate admin request
  if (!isAdminAuthenticated(request)) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  try {
    const body = await request.json();
    const { email, paymentIntentId, customerId } = body;

    if (!email || !paymentIntentId) {
      return NextResponse.json(
        { error: 'Missing email or paymentIntentId' },
        { status: 400 }
      );
    }

    if (typeof email !== 'string' || !EMAIL_REGEX.test(email.trim())) {
      return NextResponse.json(
        { error: 'Invalid email format' },
        { status: 400 }
      );
    }

    logger.info('Manual license creation requested', {
      requestId,
      paymentIntentId,
    });

    // Create license in database
    const license = await upsertLicenseFromStripe({
      email,
      type: LicenseType.LIFETIME,
      stripeCustomerId: customerId || 'manual',
      stripePaymentIntentId: paymentIntentId,
    });

    logger.info('Manual license created', {
      requestId,
      licenseId: license.id,
    });

    // Send email
    const emailResult = await sendLicenseEmail({
      email,
      licenseKey: license.licenseKey,
      licenseId: license.id,
      requestId,
    });

    if (emailResult.success) {
      logger.info('Manual license email sent', {
        requestId,
        licenseId: license.id,
        messageId: emailResult.messageId,
      });
      await recordLicenseEvent(license.id, 'email_sent', {
        messageId: emailResult.messageId,
        email: email,
        manual: true,
      });

      return NextResponse.json({
        success: true,
        license: {
          id: license.id,
          licenseKey: license.licenseKey,
          email: license.email,
        },
        email: {
          sent: true,
          messageId: emailResult.messageId,
        },
      });
    } else {
      logger.error('Manual license email failed', emailResult.error, {
        requestId,
        licenseId: license.id,
      });
      await recordLicenseEvent(license.id, 'email_failed', {
        error: emailResult.error instanceof Error ? emailResult.error.message : String(emailResult.error),
        email: email,
        manual: true,
      });

      return NextResponse.json({
        success: true,
        license: {
          id: license.id,
          licenseKey: license.licenseKey,
          email: license.email,
        },
        email: {
          sent: false,
          error: emailResult.error,
        },
      });
    }
  } catch (error: any) {
    logger.error('Manual license creation failed', error, { requestId });
    // HIGH-11 FIX: Sanitize error message to prevent leaking Prisma constraint details
    const sanitizedError = process.env.NODE_ENV === 'development'
      ? error.message
      : 'License creation failed. Please try again or contact support.';
    return NextResponse.json(
      {
        success: false,
        error: sanitizedError,
        // HIGH-8 FIX: Only expose stack traces in development
        details: process.env.NODE_ENV === 'development' ? error.stack : undefined,
      },
      { status: 500 }
    );
  }
}
