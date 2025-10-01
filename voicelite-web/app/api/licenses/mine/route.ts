import { NextRequest, NextResponse } from 'next/server';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { prisma } from '@/lib/prisma';

/**
 * GET /api/licenses/mine
 * Get all licenses belonging to the authenticated user with activation details.
 */
export async function GET(request: NextRequest) {
  try {
    const sessionToken = getSessionTokenFromRequest(request);
    if (!sessionToken) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    const session = await getSessionFromToken(sessionToken);
    if (!session) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    const licenses = await prisma.license.findMany({
      where: { userId: session.userId },
      include: {
        activations: {
          orderBy: { activatedAt: 'desc' },
        },
      },
      orderBy: { createdAt: 'desc' },
    });

    return NextResponse.json({
      licenses: licenses.map((license) => ({
        id: license.id,
        licenseKey: license.licenseKey,
        type: license.type,
        status: license.status,
        activatedAt: license.activatedAt,
        expiresAt: license.expiresAt,
        activations: license.activations.map((activation) => ({
          id: activation.id,
          machineId: activation.machineId,
          machineLabel: activation.machineLabel,
          activatedAt: activation.activatedAt,
          lastValidatedAt: activation.lastValidatedAt,
          status: activation.status,
        })),
      })),
    });
  } catch (error) {
    console.error('Fetch licenses failed:', error);
    return NextResponse.json({ error: 'Unable to fetch licenses' }, { status: 500 });
  }
}
