import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { prisma } from '@/lib/prisma';

const stripe = new Stripe(process.env.STRIPE_SECRET_KEY || 'sk_test_placeholder', {
  apiVersion: '2025-08-27.basil',
});

/**
 * POST /api/billing/portal
 * Create a Stripe customer portal session for managing subscriptions.
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

    // Find user's most recent license with Stripe customer ID
    const license = await prisma.license.findFirst({
      where: {
        userId: session.userId,
        stripeCustomerId: { not: null },
      },
      orderBy: { createdAt: 'desc' },
    });

    if (!license || !license.stripeCustomerId) {
      return NextResponse.json(
        { error: 'No active subscription found' },
        { status: 404 }
      );
    }

    const baseUrl = process.env.NEXT_PUBLIC_APP_URL;
    if (!baseUrl) {
      return NextResponse.json(
        { error: 'Server configuration error: NEXT_PUBLIC_APP_URL is required' },
        { status: 500 }
      );
    }

    const portalSession = await stripe.billingPortal.sessions.create({
      customer: license.stripeCustomerId,
      return_url: `${baseUrl}/settings`,
    });

    return NextResponse.json({ url: portalSession.url });
  } catch (error) {
    console.error('Billing portal error:', error);
    return NextResponse.json({ error: 'Unable to create portal session' }, { status: 500 });
  }
}
