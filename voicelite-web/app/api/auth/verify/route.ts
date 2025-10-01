import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { hashToken } from '@/lib/crypto';
import { createSession, setSessionCookie } from '@/lib/auth/session';

const DEFAULT_REDIRECT = process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3000';

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const token = searchParams.get('token');
  const redirectParam = searchParams.get('redirect');

  if (!token) {
    return NextResponse.json({ error: 'Missing token' }, { status: 400 });
  }

  const tokenHash = hashToken(token);
  const magicToken = await prisma.magicLinkToken.findUnique({
    where: { tokenHash },
  });

  if (!magicToken || magicToken.consumedAt || magicToken.expiresAt < new Date()) {
    return NextResponse.redirect(new URL('/?login=expired', DEFAULT_REDIRECT));
  }

  await prisma.magicLinkToken.update({
    where: { tokenHash },
    data: { consumedAt: new Date() },
  });

  const { token: sessionToken, expiresAt } = await createSession(magicToken.userId, request);

  const destination = redirectParam ?? magicToken.redirectUri ?? `${DEFAULT_REDIRECT}/?login=success`;

  const response = NextResponse.redirect(destination);
  setSessionCookie(response, sessionToken, expiresAt);
  return response;
}