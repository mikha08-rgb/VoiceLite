import { NextResponse } from 'next/server';
import { sendMagicLinkEmail } from '@/lib/email';

export async function GET() {
  try {
    console.log('=== EMAIL TEST ENDPOINT ===');
    console.log('RESEND_API_KEY exists:', !!process.env.RESEND_API_KEY);
    console.log('RESEND_FROM_EMAIL:', process.env.RESEND_FROM_EMAIL);

    await sendMagicLinkEmail({
      email: 'test@example.com',
      magicLinkUrl: 'https://test.com/link',
      otpCode: '12345678',
      deepLinkUrl: 'voicelite://test',
      expiresInMinutes: 15,
    });

    return NextResponse.json({ success: true, message: 'Email sent' });
  } catch (error) {
    console.error('Test email failed:', error);
    return NextResponse.json({
      success: false,
      error: error instanceof Error ? error.message : 'Unknown error'
    }, { status: 500 });
  }
}
