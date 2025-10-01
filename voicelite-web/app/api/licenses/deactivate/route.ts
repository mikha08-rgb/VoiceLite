import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { deactivateLicenseActivation, recordLicenseEvent } from '@/lib/licensing';
import { prisma } from '@/lib/prisma';

const bodySchema = z.object({
  licenseId: z.string(),
  machineId: z.string().min(6),
});

/**
 * POST /api/licenses/deactivate
 * Deactivate a device/machine for a given license.
 * Allows user to free up a seat.
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

    const body = await request.json();
    const { licenseId, machineId } = bodySchema.parse(body);

    const license = await prisma.license.findUnique({
      where: { id: licenseId },
    });

    if (!license) {
      return NextResponse.json({ error: 'License not found' }, { status: 404 });
    }

    if (license.userId !== session.userId) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 403 });
    }

    await deactivateLicenseActivation({ licenseId, machineId });
    await recordLicenseEvent(licenseId, 'deactivated', { machineId });

    return NextResponse.json({ ok: true });
  } catch (error) {
    console.error('License deactivation failed:', error);
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }
    return NextResponse.json({ error: 'Unable to deactivate license' }, { status: 500 });
  }
}
