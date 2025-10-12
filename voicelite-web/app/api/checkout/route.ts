import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { validateOrigin, getCsrfErrorResponse } from '@/lib/csrf';

// Lazy initialization of Stripe client
// During build time, env vars may not be set, so we initialize on first use
function getStripeClient() {
  if (!process.env.STRIPE_SECRET_KEY) {
    throw new Error('STRIPE_SECRET_KEY environment variable is required but not set');
  }
  return new Stripe(process.env.STRIPE_SECRET_KEY, {
    apiVersion: '2025-08-27.basil',
  });
}

const bodySchema = z.object({
  successUrl: z.string().url().optional(),
  cancelUrl: z.string().url().optional(),
});

export async function POST(request: NextRequest) {
  // CSRF protection
  if (!validateOrigin(request)) {
    return NextResponse.json(getCsrfErrorResponse(), { status: 403 });
  }

  try {
    const body = await request.json();
    const { successUrl, cancelUrl } = bodySchema.parse(body);

    const baseUrl = process.env.NEXT_PUBLIC_APP_URL;
    if (!baseUrl) {
      return NextResponse.json(
        { error: 'Server configuration error: NEXT_PUBLIC_APP_URL is required' },
        { status: 500 }
      );
    }
    const success = successUrl ?? `${baseUrl}/checkout/success`;
    const cancel = cancelUrl ?? `${baseUrl}/checkout/cancel`;

    const sessionToken = getSessionTokenFromRequest(request);
    let customerEmail: string | undefined;
    if (sessionToken) {
      const session = await getSessionFromToken(sessionToken);
      if (session) {
        const user = await prisma.user.findUnique({ where: { id: session.userId } });
        customerEmail = user?.email;
      }
    }

    if (!customerEmail) {
      return NextResponse.json({ error: 'Authentication required to start checkout' }, { status: 401 });
    }

    // Simple one-time $20 payment - no subscription, no tiers
    const sessionPayload: Stripe.Checkout.SessionCreateParams = {
      payment_method_types: ['card'],
      mode: 'payment', // One-time payment only
      line_items: [
        {
          price_data: {
            currency: 'usd',
            product_data: {
              name: 'VoiceLite',
              description: 'One-time purchase - Lifetime access to VoiceLite',
            },
            unit_amount: 2000, // $20.00
          },
          quantity: 1,
        },
      ],
      success_url: success,
      cancel_url: cancel,
      customer_email: customerEmail,
      client_reference_id: customerEmail,
    }

    const stripe = getStripeClient();
    const session = await stripe.checkout.sessions.create(sessionPayload);

    return NextResponse.json({ url: session.url });
  } catch (error) {
    // Enhanced error handling with specific messages for different error types
    if (error instanceof z.ZodError) {
      console.error('Checkout validation error:', error.issues);
      return NextResponse.json({ error: 'Invalid request parameters' }, { status: 400 });
    }

    // Handle Stripe-specific errors with actionable messages
    if (error && typeof error === 'object' && 'type' in error) {
      const stripeError = error as any; // Stripe error types vary by version

      console.error('Stripe API error:', {
        type: stripeError.type,
        code: stripeError.code,
        message: stripeError.message,
        // Don't log full error object to avoid leaking sensitive details
      });

      switch (stripeError.type) {
        case 'StripeCardError':
          return NextResponse.json(
            { error: 'Payment method declined. Please try a different card.' },
            { status: 400 }
          );

        case 'StripeRateLimitError':
          return NextResponse.json(
            { error: 'Too many requests. Please try again in a moment.' },
            { status: 429 }
          );

        case 'StripeInvalidRequestError':
          // Configuration error - don't expose details to user
          console.error('Stripe configuration error - check price IDs and API keys');
          return NextResponse.json(
            { error: 'Payment system configuration error. Please contact support.' },
            { status: 500 }
          );

        case 'StripeAPIError':
        case 'StripeConnectionError':
          return NextResponse.json(
            { error: 'Payment service temporarily unavailable. Please try again.' },
            { status: 503 }
          );

        case 'StripeAuthenticationError':
          console.error('Stripe authentication failed - check API keys');
          return NextResponse.json(
            { error: 'Payment system authentication error. Please contact support.' },
            { status: 500 }
          );

        default:
          console.error('Unknown Stripe error type:', stripeError.type);
          return NextResponse.json(
            { error: 'An error occurred during checkout. Please try again.' },
            { status: 500 }
          );
      }
    }

    // Generic error fallback (network errors, etc.)
    console.error('Unexpected checkout error:', error instanceof Error ? error.message : 'Unknown error');
    return NextResponse.json(
      { error: 'Failed to create checkout session. Please try again.' },
      { status: 500 }
    );
  }
}