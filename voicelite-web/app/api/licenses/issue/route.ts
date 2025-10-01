import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { generateSignedLicense, recordLicenseEvent } from '@/lib/licensing';
import { prisma } from '@/lib/prisma';
import { checkRateLimit, licenseRateLimit } from '@/lib/ratelimit';

const bodySchema = z.object({
  deviceFingerprint: z.string().min(6),
});

/**
 * POST /api/licenses/issue
 * Generate a signed license file for the authenticated user's active license.
 * Requires user to have an active purchase/license.
 */
export async function POST(request: NextRequest) {
  try {
    const sessionToken = getSessionTokenFromRequest(request);
    if (!sessionToken) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    const session = await getSessionFromToken(sessionToken);
    if (!session) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    // Rate limit: 30 license operations per day per user
    const rateLimit = await checkRateLimit(session.userId, licenseRateLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json({ error: 'Daily license operation limit reached' }, { status: 429 });
    }

    const body = await request.json();
    const { deviceFingerprint } = bodySchema.parse(body);

    // Find user's active license
    const activeLicense = await prisma.license.findFirst({
      where: {
        userId: session.userId,
        status: 'ACTIVE',
      },
      orderBy: { createdAt: 'desc' },
    });

    if (!activeLicense) {
      return NextResponse.json(
        { error: 'No active license found. Please purchase a license first.' },
        { status: 404 }
      );
    }

    // Generate signed license
    const signedLicense = await generateSignedLicense(activeLicense.id, deviceFingerprint);

    // Record event
    await recordLicenseEvent(activeLicense.id, 'issued', {
      deviceFingerprint,
      issuedAt: new Date().toISOString(),
    });

    return NextResponse.json({
      signedLicense,
      licenseKey: activeLicense.licenseKey,
      type: activeLicense.type,
      expiresAt: activeLicense.expiresAt,
    });
  } catch (error) {
    console.error('License issue failed:', error);
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }
    if (error instanceof Error) {
      return NextResponse.json({ error: error.message }, { status: 500 });
    }
    return NextResponse.json({ error: 'Unable to issue license' }, { status: 500 });
  }
}
