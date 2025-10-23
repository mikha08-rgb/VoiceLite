import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { getLicenseByKey, recordLicenseActivation } from '@/lib/licensing';
import { checkRateLimit, licenseRateLimit } from '@/lib/ratelimit';
import { validateOrigin, getCsrfErrorResponse } from '@/lib/csrf';

const bodySchema = z.object({
  licenseKey: z.string().min(10),
  machineId: z.string().min(6),
  machineLabel: z.string().optional(),
  machineHash: z.string().optional(),
});

const MAX_ACTIVATIONS = 3;

export async function POST(request: NextRequest) {
  // CSRF protection
  if (!validateOrigin(request)) {
    return NextResponse.json(getCsrfErrorResponse(), { status: 403 });
  }

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
    const { licenseKey, machineId, machineLabel, machineHash } = bodySchema.parse(body);

    const license = await getLicenseByKey(licenseKey);
    if (!license) {
      return NextResponse.json({ error: 'License not found' }, { status: 404 });
    }

    if (license.userId !== session.userId) {
      return NextResponse.json({ error: 'License does not belong to this account' }, { status: 403 });
    }

    // Count ALL activations (ACTIVE + BLOCKED) to prevent limit bypass
    // Users were previously able to block 3 machines, then activate 3 more
    const allActivations = license.activations;
    const existing = allActivations.find((activation) => activation.machineId === machineId);

    if (!existing && allActivations.length >= MAX_ACTIVATIONS) {
      return NextResponse.json({ error: 'Activation limit reached' }, { status: 409 });
    }

    const activation = await recordLicenseActivation({
      licenseId: license.id,
      machineId,
      machineLabel,
      machineHash,
    });

    return NextResponse.json({
      ok: true,
      activation: {
        id: activation.id,
        status: activation.status,
      },
    });
  } catch (error) {
    console.error('License activation failed', error);
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }
    return NextResponse.json({ error: 'Unable to activate license' }, { status: 500 });
  }
}