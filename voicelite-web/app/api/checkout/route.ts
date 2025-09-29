import { NextResponse } from 'next/server';
import Stripe from 'stripe';

export async function POST() {
  const stripe = new Stripe(process.env.STRIPE_SECRET_KEY || 'sk_test_placeholder', {
    apiVersion: '2025-08-27.basil',
  });
  try {
    // Create Stripe checkout session
    const session = await stripe.checkout.sessions.create({
      mode: 'subscription',
      payment_method_types: ['card'],
      line_items: [
        {
          price: process.env.STRIPE_PRICE_ID,
          quantity: 1,
        },
      ],
      success_url: `${process.env.NEXT_PUBLIC_URL || 'http://localhost:3000'}/success.html`,
      cancel_url: `${process.env.NEXT_PUBLIC_URL || 'http://localhost:3000'}`,
      subscription_data: {
        trial_period_days: 7,
        metadata: {
          product: 'voicelite_pro',
        },
      },
      customer_email: undefined, // Will be filled by customer
      metadata: {
        product: 'voicelite_pro',
      },
    });

    return NextResponse.json({ url: session.url });
  } catch (error) {
    console.error('Stripe checkout error:', error);
    return NextResponse.json(
      { error: 'Failed to create checkout session' },
      { status: 500 }
    );
  }
}