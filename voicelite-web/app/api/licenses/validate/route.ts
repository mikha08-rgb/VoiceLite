import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getLicenseByKey } from '@/lib/licensing';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { licenseRateLimit, checkRateLimit } from '@/lib/ratelimit';

const bodySchema = z.object({
  licenseKey: z.string().min(10),
  machineId: z.string().optional(),
});

export async function POST(request: NextRequest) {
  try {
    // Require authentication
    const sessionToken = getSessionTokenFromRequest(request);
    if (!sessionToken) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    const session = await getSessionFromToken(sessionToken);
    if (!session) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    // Rate limiting (30 requests per day per user)
    const rateLimit = await checkRateLimit(session.userId, licenseRateLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json(
        { error: `Rate limit exceeded. Try again after ${rateLimit.reset.toLocaleTimeString()}.` },
        { status: 429, headers: { 'Retry-After': String(Math.ceil((rateLimit.reset.getTime() - Date.now()) / 1000)) } }
      );
    }

    const body = await request.json();
    const { licenseKey, machineId } = bodySchema.parse(body);

    const license = await getLicenseByKey(licenseKey);
    if (!license) {
      return NextResponse.json({ valid: false }, { status: 200 });
    }

    const activation = machineId
      ? license.activations.find((item) => item.machineId === machineId)
      : null;

    return NextResponse.json({
      valid: license.status === 'ACTIVE',
      license: {
        type: license.type,
        status: license.status,
        expiresAt: license.expiresAt,
      },
      activation: activation
        ? {
            status: activation.status,
            lastValidatedAt: activation.lastValidatedAt,
          }
        : null,
    });
  } catch (error) {
    console.error('License validation failed', error);
    return NextResponse.json({ error: 'Unable to validate' }, { status: 500 });
  }
}