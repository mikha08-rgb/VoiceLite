import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { isAdminAuthenticated } from '@/lib/admin-auth';

// Same email format validation as the webhook route (CRITICAL-2 FIX there)
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

// Temporary admin endpoint to get license by email
export async function POST(request: NextRequest) {
  // Authenticate admin request
  if (!isAdminAuthenticated(request)) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  try {
    const { email, sendEmail } = await request.json();

    if (!email || typeof email !== 'string' || !EMAIL_REGEX.test(email.trim())) {
      return NextResponse.json({ error: 'Valid email required' }, { status: 400 });
    }

    // Find the license. Storage lowercases emails (upsertLicenseFromStripe), so
    // the lookup must too - a case-mismatched query returned false 404s, which
    // led operators to mint duplicate licenses.
    const license = await prisma.license.findFirst({
      where: { email: email.trim().toLowerCase() },
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
    // Mirror process-payment's sanitization: never leak raw error internals
    // (Prisma constraint details etc.) outside development.
    const sanitizedError = process.env.NODE_ENV === 'development'
      ? (error instanceof Error ? error.message : String(error))
      : 'License lookup failed. Please try again or contact support.';
    return NextResponse.json(
      { error: sanitizedError },
      { status: 500 }
    );
  }
}
