import { cookies } from 'next/headers';
import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { generateToken, hashToken } from '@/lib/crypto';

const SESSION_COOKIE_NAME = process.env.SESSION_COOKIE_NAME ?? 'voicelite_session';
const SESSION_TTL_MS = 30 * 24 * 60 * 60 * 1000; // 30 days

export async function createSession(userId: string, request?: NextRequest) {
  const token = generateToken(48);
  const tokenHash = hashToken(token);
  const jwtId = generateToken(32);
  const expiresAt = new Date(Date.now() + SESSION_TTL_MS);

  await prisma.session.create({
    data: {
      userId,
      sessionHash: tokenHash,
      jwtId,
      expiresAt,
      userAgent: request?.headers.get('user-agent') ?? undefined,
      ipAddress:
        request?.headers.get('x-forwarded-for')?.split(',')[0]?.trim() ??
        undefined,
    },
  });

  return { token, expiresAt, jwtId };
}

export function setSessionCookie(response: NextResponse, token: string, expiresAt: Date) {
  const isProduction = process.env.NODE_ENV === 'production';

  response.cookies.set({
    name: SESSION_COOKIE_NAME,
    value: token,
    httpOnly: true,
    secure: isProduction, // HTTPS only in production
    sameSite: 'strict', // CSRF protection
    expires: expiresAt,
    path: '/',
  });
}

export async function clearSessionCookie(response: NextResponse, request?: NextRequest) {
  const sessionToken = request ? getSessionTokenFromRequest(request) : await getSessionTokenFromCookies();
  if (sessionToken) {
    try {
      await prisma.session.update({
        where: { sessionHash: hashToken(sessionToken) },
        data: { revokedAt: new Date() },
      });
    } catch (error) {
      // Session already cleared
    }
  }

  const isProduction = process.env.NODE_ENV === 'production';

  response.cookies.set({
    name: SESSION_COOKIE_NAME,
    value: '',
    httpOnly: true,
    secure: isProduction,
    sameSite: 'strict',
    expires: new Date(0),
    path: '/',
  });
}

export function getSessionTokenFromRequest(request: NextRequest) {
  return request.cookies.get(SESSION_COOKIE_NAME)?.value ?? null;
}

export async function getSessionTokenFromCookies() {
  const cookieStore = await cookies();
  return cookieStore.get(SESSION_COOKIE_NAME)?.value ?? null;
}

export async function getSessionFromToken(token: string) {
  const tokenHash = hashToken(token);
  const session = await prisma.session.findUnique({
    where: { sessionHash: tokenHash },
    include: { user: true },
  });

  if (!session) return null;
  if (session.revokedAt) return null;
  if (session.expiresAt < new Date()) return null;

  return session;
}

export async function rotateSession(sessionId: string) {
  const newToken = generateToken(48);
  const tokenHash = hashToken(newToken);
  const newJwtId = generateToken(32);
  const expiresAt = new Date(Date.now() + SESSION_TTL_MS);

  // Use transaction to prevent race conditions during concurrent rotation
  const session = await prisma.$transaction(async (tx) => {
    // Check if session is still valid before rotating
    const existing = await tx.session.findUnique({
      where: { id: sessionId },
    });

    if (!existing) {
      throw new Error('Session not found');
    }

    if (existing.revokedAt) {
      throw new Error('Session already revoked');
    }

    if (existing.expiresAt < new Date()) {
      throw new Error('Session expired');
    }

    // Update session with new credentials
    return tx.session.update({
      where: { id: sessionId },
      data: {
        sessionHash: tokenHash,
        jwtId: newJwtId,
        expiresAt,
        revokedAt: null,
      },
    });
  });

  return { session, token: newToken, expiresAt };
}