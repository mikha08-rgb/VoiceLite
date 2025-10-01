import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { hashOtp } from '@/lib/crypto';
import { createSession, setSessionCookie } from '@/lib/auth/session';
import { checkRateLimit, otpRateLimit } from '@/lib/ratelimit';
import { validateOrigin, getCsrfErrorResponse } from '@/lib/csrf';

const bodySchema = z.object({
  email: z.string().email(),
  otp: z.string().length(8), // Updated to 8 digits for enhanced security
});

export async function POST(request: NextRequest) {
  // CSRF protection
  if (!validateOrigin(request)) {
    return NextResponse.json(getCsrfErrorResponse(), { status: 403 });
  }

  try {
    const body = await request.json();
    const { email, otp } = bodySchema.parse(body);
    const normalizedEmail = email.toLowerCase();

    // Rate limit: 10 OTP attempts per hour per email
    const rateLimit = await checkRateLimit(normalizedEmail, otpRateLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json(
        { error: 'Too many attempts. Please try again later.' },
        { status: 429 }
      );
    }

    const user = await prisma.user.findUnique({ where: { email: normalizedEmail } });
    if (!user) {
      return NextResponse.json({ error: 'Invalid code' }, { status: 400 });
    }

    const tokens = await prisma.magicLinkToken.findMany({
      where: {
        userId: user.id,
        consumedAt: null,
        expiresAt: { gt: new Date() },
      },
      orderBy: { createdAt: 'desc' },
      take: 5,
    });

    const otpHash = hashOtp(otp);

    // Use constant-time comparison to prevent timing attacks
    const match = tokens.find((token) => {
      try {
        // timingSafeEqual requires equal-length buffers
        if (token.otpHash.length !== otpHash.length) {
          return false;
        }
        const crypto = require('crypto');
        return crypto.timingSafeEqual(
          Buffer.from(token.otpHash, 'utf8'),
          Buffer.from(otpHash, 'utf8')
        );
      } catch {
        return false;
      }
    });

    if (!match) {
      return NextResponse.json({ error: 'Invalid code' }, { status: 400 });
    }

    await prisma.magicLinkToken.update({
      where: { id: match.id },
      data: { consumedAt: new Date() },
    });

    const { token: sessionToken, expiresAt } = await createSession(user.id, request);
    const response = NextResponse.json({ ok: true });
    setSessionCookie(response, sessionToken, expiresAt);
    return response;
  } catch (error) {
    console.error('OTP verification failed', error);
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }
    return NextResponse.json({ error: 'Unable to verify code' }, { status: 500 });
  }
}