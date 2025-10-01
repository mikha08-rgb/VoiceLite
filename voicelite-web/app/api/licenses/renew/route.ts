import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { generateSignedLicense, recordLicenseEvent } from '@/lib/licensing';
import { prisma } from '@/lib/prisma';
import { LicenseType } from '@prisma/client';
import { checkRateLimit, licenseRateLimit } from '@/lib/ratelimit';

const bodySchema = z.object({
  licenseId: z.string(),
  deviceFingerprint: z.string().min(6),
});

/**
 * POST /api/licenses/renew
 * Renew/refresh a license (extend expiry for subscriptions, refresh signature).
 * For subscriptions, checks if payment is current.
 * For lifetime, just re-signs the license.
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
    const { licenseId, deviceFingerprint } = bodySchema.parse(body);

    const license = await prisma.license.findUnique({
      where: { id: licenseId },
    });

    if (!license) {
      return NextResponse.json({ error: 'License not found' }, { status: 404 });
    }

    if (license.userId !== session.userId) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 403 });
    }

    if (license.status !== 'ACTIVE') {
      return NextResponse.json(
        { error: 'License is not active. Please check your subscription status.' },
        { status: 400 }
      );
    }

    // For subscriptions, extend the expiry date
    if (license.type === LicenseType.SUBSCRIPTION && license.stripeSubscriptionId) {
      const now = new Date();
      const newExpiresAt = new Date(now.getTime() + 90 * 24 * 60 * 60 * 1000); // +90 days

      await prisma.license.update({
        where: { id: licenseId },
        data: { expiresAt: newExpiresAt },
      });
    }

    // Generate fresh signed license
    const signedLicense = await generateSignedLicense(licenseId, deviceFingerprint);

    // Record event
    await recordLicenseEvent(licenseId, 'renewed', {
      deviceFingerprint,
      renewedAt: new Date().toISOString(),
    });

    return NextResponse.json({
      signedLicense,
      expiresAt: license.expiresAt,
    });
  } catch (error) {
    console.error('License renewal failed:', error);
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }
    if (error instanceof Error) {
      return NextResponse.json({ error: error.message }, { status: 500 });
    }
    return NextResponse.json({ error: 'Unable to renew license' }, { status: 500 });
  }
}
