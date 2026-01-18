import { NextRequest, NextResponse } from 'next/server';
import { LicenseType } from '@prisma/client';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { upsertLicenseFromStripe, recordLicenseEvent } from '@/lib/licensing';
import { isAdminAuthenticated } from '@/lib/admin-auth';

export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
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

    console.log(`🔧 MANUAL: Creating license for ${email}, Payment Intent: ${paymentIntentId}`);

    // Create license in database
    const license = await upsertLicenseFromStripe({
      email,
      type: LicenseType.LIFETIME,
      stripeCustomerId: customerId || 'manual',
      stripePaymentIntentId: paymentIntentId,
    });

    console.log(`✅ License created: ${license.licenseKey}`);

    // Send email
    const emailResult = await sendLicenseEmail({
      email,
      licenseKey: license.licenseKey,
    });

    if (emailResult.success) {
      console.log(`✅ Email sent to ${email}`);
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
      console.error(`❌ Email failed for ${email}`);
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
    console.error('❌ Manual license creation failed:', error);
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
