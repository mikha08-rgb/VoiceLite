import { NextRequest, NextResponse } from 'next/server';
import { sendLicenseEmail } from '@/lib/emails/license-email';

export async function POST(request: NextRequest) {
  try {
    const { email, licenseKey } = await request.json();

    console.log('=== LICENSE EMAIL TEST ===');
    console.log('RESEND_API_KEY exists:', !!process.env.RESEND_API_KEY);
    console.log('RESEND_FROM_EMAIL:', process.env.RESEND_FROM_EMAIL);
    console.log('Sending to:', email);
    console.log('License key:', licenseKey);

    const result = await sendLicenseEmail({
      email: email || 'test@example.com',
      licenseKey: licenseKey || 'test-key-12345',
    });

    if (result.success) {
      return NextResponse.json({
        success: true,
        message: 'License email sent successfully',
        messageId: result.messageId
      });
    } else {
      throw result.error;
    }
  } catch (error) {
    console.error('Test email failed:', error);
    return NextResponse.json({
      success: false,
      error: error instanceof Error ? error.message : String(error)
    }, { status: 500 });
  }
}

// Also support GET for quick browser testing
export async function GET() {
  try {
    console.log('=== LICENSE EMAIL TEST (GET) ===');
    console.log('RESEND_API_KEY exists:', !!process.env.RESEND_API_KEY);
    console.log('RESEND_FROM_EMAIL:', process.env.RESEND_FROM_EMAIL);

    const result = await sendLicenseEmail({
      email: 'test@example.com',
      licenseKey: 'test-key-12345-67890',
    });

    if (result.success) {
      return NextResponse.json({
        success: true,
        message: 'License email sent to test@example.com',
        messageId: result.messageId
      });
    } else {
      throw result.error;
    }
  } catch (error) {
    console.error('Test email failed:', error);
    return NextResponse.json({
      success: false,
      error: error instanceof Error ? error.message : String(error)
    }, { status: 500 });
  }
}
