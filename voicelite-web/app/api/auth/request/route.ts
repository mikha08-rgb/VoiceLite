import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { generateOtp, generateToken, hashOtp, hashToken } from '@/lib/crypto';
import { sendMagicLinkEmail } from '@/lib/email';
import { checkRateLimit, emailRateLimit } from '@/lib/ratelimit';
import { validateOrigin, getCsrfErrorResponse } from '@/lib/csrf';

const requestSchema = z.object({
  email: z.string().email(),
  redirectUri: z.string().url().optional(),
});

const MAGIC_LINK_EXPIRY_MINUTES = 15;

export async function POST(request: NextRequest) {
  // CSRF protection: Validate request origin
  if (!validateOrigin(request)) {
    return NextResponse.json(getCsrfErrorResponse(), { status: 403 });
  }

  try {
    const body = await request.json();
    const { email, redirectUri } = requestSchema.parse(body);
    const normalizedEmail = email.toLowerCase();

    // Rate limit: 5 magic link requests per hour per email
    const rateLimit = await checkRateLimit(normalizedEmail, emailRateLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json(
        {
          error: `Too many requests. Please try again after ${rateLimit.reset.toLocaleTimeString()}.`,
          retryAfter: Math.ceil((rateLimit.reset.getTime() - Date.now()) / 1000),
        },
        {
          status: 429,
          headers: {
            'Retry-After': String(Math.ceil((rateLimit.reset.getTime() - Date.now()) / 1000)),
            'X-RateLimit-Limit': String(rateLimit.limit),
            'X-RateLimit-Remaining': String(rateLimit.remaining),
            'X-RateLimit-Reset': rateLimit.reset.toISOString(),
          },
        }
      );
    }

    const user = await prisma.user.upsert({
      where: { email: normalizedEmail },
      create: { email: normalizedEmail },
      update: {},
    });

    const token = generateToken(32);
    const tokenHash = hashToken(token);
    const otpCode = generateOtp();
    const otpHash = hashOtp(otpCode);
    const expiresAt = new Date(Date.now() + MAGIC_LINK_EXPIRY_MINUTES * 60 * 1000);

    await prisma.magicLinkToken.create({
      data: {
        userId: user.id,
        tokenHash,
        otpHash,
        expiresAt,
        redirectUri,
      },
    });

    const baseUrl = process.env.NEXT_PUBLIC_APP_URL;
    if (!baseUrl) {
      return NextResponse.json(
        { error: 'Server configuration error: NEXT_PUBLIC_APP_URL is required' },
        { status: 500 }
      );
    }
    const deepLinkBase = process.env.MAGIC_LINK_APP_DEEP_LINK ?? 'voicelite://auth/callback';

    const magicLinkUrl = new URL('/api/auth/verify', baseUrl);
    magicLinkUrl.searchParams.set('token', token);
    if (redirectUri) {
      magicLinkUrl.searchParams.set('redirect', redirectUri);
    }

    const deepLinkUrl = `${deepLinkBase}?token=${token}`;

    // Send email but don't expose failures to prevent user enumeration
    try {
      await sendMagicLinkEmail({
        email: normalizedEmail,
        magicLinkUrl: magicLinkUrl.toString(),
        deepLinkUrl,
        otpCode,
        expiresInMinutes: MAGIC_LINK_EXPIRY_MINUTES,
      });
    } catch (emailError) {
      // Log error but still return success to prevent enumeration
      console.error('Failed to send magic link email', emailError);
    }

    // Always return success response to prevent account enumeration
    return NextResponse.json({ ok: true });
  } catch (error) {
    console.error('Magic link request failed', error);
    if (error instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }
    // Generic success message that doesn't leak information
    return NextResponse.json({ ok: true }, { status: 200 });
  }
}