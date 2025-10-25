import { NextRequest, NextResponse } from 'next/server';
import { LicenseType } from '@prisma/client';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { upsertLicenseFromStripe, recordLicenseEvent } from '@/lib/licensing';

export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
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
    return NextResponse.json(
      {
        success: false,
        error: error.message,
        details: error.stack,
      },
      { status: 500 }
    );
  }
}
