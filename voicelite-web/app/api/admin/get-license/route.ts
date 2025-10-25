import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { sendLicenseEmail } from '@/lib/emails/license-email';

// Temporary admin endpoint to get license by email
export async function POST(request: NextRequest) {
  try {
    const { email, sendEmail } = await request.json();

    if (!email) {
      return NextResponse.json({ error: 'Email required' }, { status: 400 });
    }

    // Find the license
    const license = await prisma.license.findFirst({
      where: { email },
      orderBy: { createdAt: 'desc' },
    });

    if (!license) {
      return NextResponse.json({ error: 'License not found' }, { status: 404 });
    }

    let emailResult = null;
    if (sendEmail) {
      emailResult = await sendLicenseEmail({
        email: license.email,
        licenseKey: license.licenseKey,
      });
    }

    return NextResponse.json({
      success: true,
      license: {
        id: license.id,
        email: license.email,
        licenseKey: license.licenseKey,
        type: license.type,
        status: license.status,
        createdAt: license.createdAt,
      },
      emailSent: emailResult?.success || false,
      emailMessageId: emailResult?.messageId,
    });
  } catch (error) {
    console.error('Error getting license:', error);
    return NextResponse.json(
      { error: 'Internal server error', details: error instanceof Error ? error.message : String(error) },
      { status: 500 }
    );
  }
}
