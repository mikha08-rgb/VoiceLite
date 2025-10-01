import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { getSessionTokenFromRequest, getSessionFromToken, rotateSession, setSessionCookie } from '@/lib/auth/session';
import { checkRateLimit, profileRateLimit } from '@/lib/ratelimit';

export async function GET(request: NextRequest) {
  const sessionToken = getSessionTokenFromRequest(request);
  if (!sessionToken) {
    return NextResponse.json({ user: null, licenses: [] });
  }

  const session = await getSessionFromToken(sessionToken);
  if (!session) {
    return NextResponse.json({ user: null, licenses: [] });
  }

  // Rate limit: 100 requests per hour per user
  const rateLimit = await checkRateLimit(session.userId, profileRateLimit);
  if (!rateLimit.allowed) {
    return NextResponse.json(
      { error: `Rate limit exceeded. Try again after ${rateLimit.reset.toLocaleTimeString()}.` },
      { status: 429, headers: { 'Retry-After': String(Math.ceil((rateLimit.reset.getTime() - Date.now()) / 1000)) } }
    );
  }

  // Only rotate session if older than 7 days to reduce DB writes
  const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000;
  const sessionAge = Date.now() - session.createdAt.getTime();
  const shouldRotate = sessionAge > SEVEN_DAYS_MS;

  let newToken = sessionToken;
  let expiresAt = session.expiresAt;

  if (shouldRotate) {
    const rotated = await rotateSession(session.id);
    newToken = rotated.token;
    expiresAt = rotated.expiresAt;
  }

  const user = await prisma.user.findUnique({
    where: { id: session.userId },
    include: {
      licenses: {
        include: { activations: true },
      },
    },
  });

  if (!user) {
    return NextResponse.json({ user: null, licenses: [] });
  }

  const response = NextResponse.json({
    user: {
      id: user.id,
      email: user.email,
    },
    licenses: user.licenses.map((license) => ({
      id: license.id,
      licenseKey: license.licenseKey,
      type: license.type,
      status: license.status,
      expiresAt: license.expiresAt,
    })),
  });
  setSessionCookie(response, newToken, expiresAt);
  return response;
}