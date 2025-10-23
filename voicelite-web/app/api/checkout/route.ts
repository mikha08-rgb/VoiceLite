import { NextRequest, NextResponse } from 'next/server';
import Stripe from 'stripe';

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

export async function POST(request: NextRequest) {
  try {
    const baseUrl = process.env.NEXT_PUBLIC_APP_URL;
    if (!baseUrl) {
      return NextResponse.json(
        { error: 'Server configuration error: NEXT_PUBLIC_APP_URL is required' },
        { status: 500 }
      );
    }

    const priceId = process.env.STRIPE_PRO_PRICE_ID;
    if (!priceId || priceId.includes('placeholder')) {
      return NextResponse.json({ error: 'Stripe price not configured' }, { status: 500 });
    }

    const stripe = getStripeClient();

    // Simple one-time payment session - Stripe collects email
    const session = await stripe.checkout.sessions.create({
      mode: 'payment',
      payment_method_types: ['card'],
      line_items: [
        {
          price: priceId,
          quantity: 1,
        },
      ],
      success_url: `${baseUrl}/checkout/success?session_id={CHECKOUT_SESSION_ID}`,
      cancel_url: `${baseUrl}/checkout/cancel`,
      allow_promotion_codes: true,
      billing_address_collection: 'auto',
    });

    return NextResponse.json({ url: session.url });
  } catch (error) {
    // Handle Stripe-specific errors with actionable messages
    if (error && typeof error === 'object' && 'type' in error) {
      const stripeError = error as any;

      console.error('Stripe API error:', {
        type: stripeError.type,
        code: stripeError.code,
        message: stripeError.message,
      });

      switch (stripeError.type) {
        case 'StripeInvalidRequestError':
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

    // Generic error fallback
    console.error('Unexpected checkout error:', error instanceof Error ? error.message : 'Unknown error');
    return NextResponse.json(
      { error: 'Failed to create checkout session. Please try again.' },
      { status: 500 }
    );
  }
}