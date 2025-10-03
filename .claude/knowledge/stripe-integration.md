# Stripe Integration Patterns for VoiceLite

## Webhook Security

### Signature Verification (CRITICAL)
```typescript
// ✅ CORRECT - Always verify webhook signature
import Stripe from 'stripe';

export async function POST(req: Request) {
  const body = await req.text();
  const sig = req.headers.get('stripe-signature');

  if (!sig) {
    return new Response('No signature', { status: 400 });
  }

  let event: Stripe.Event;
  try {
    event = stripe.webhooks.constructEvent(
      body,
      sig,
      process.env.STRIPE_WEBHOOK_SECRET!
    );
  } catch (err) {
    console.error('Webhook signature verification failed:', err);
    return new Response('Invalid signature', { status: 400 });
  }

  // Process event...
  return new Response(JSON.stringify({ received: true }), { status: 200 });
}
```

### Never Skip Verification
```typescript
// ❌ CRITICAL VULNERABILITY
export async function POST(req: Request) {
  const event = await req.json(); // Trusting unverified data!
  // Anyone can fake this...
}
```

### Webhook Best Practices
1. **Always return 200 OK**: Stripe retries on non-200 responses
2. **Process idempotently**: Same event may be sent multiple times
3. **Log event IDs**: Track processing status
4. **Handle errors gracefully**: Don't expose internal errors to Stripe
5. **Use database transactions**: Ensure atomic updates

## Idempotent Event Handling

### Database-Level Idempotency
```typescript
// ✅ GOOD - Idempotent using database constraints
case 'customer.subscription.created': {
  const subscription = event.data.object as Stripe.Subscription;

  try {
    await db.licenses.create({
      data: {
        stripeSubscriptionId: subscription.id, // UNIQUE constraint
        customerId: subscription.customer as string,
        status: 'active',
        // ...
      }
    });
  } catch (err) {
    if (err.code === 'P2002') { // Prisma unique constraint violation
      // Already processed this event
      console.log(`Subscription ${subscription.id} already exists`);
      return new Response(JSON.stringify({ received: true }), { status: 200 });
    }
    throw err; // Rethrow unexpected errors
  }
  break;
}
```

### Event ID Tracking (Alternative)
```typescript
// Track processed events
const processed = await db.processedEvents.findUnique({
  where: { eventId: event.id }
});

if (processed) {
  console.log(`Event ${event.id} already processed`);
  return new Response(JSON.stringify({ received: true }), { status: 200 });
}

// Process event...

// Mark as processed
await db.processedEvents.create({
  data: { eventId: event.id, processedAt: new Date() }
});
```

## Checkout Session Creation

### Secure Checkout Pattern
```typescript
import Stripe from 'stripe';

const stripe = new Stripe(process.env.STRIPE_SECRET_KEY!, {
  apiVersion: '2023-10-16',
});

export async function POST(req: Request) {
  // 1. Validate input
  const body = await req.json();
  const { email, plan } = CheckoutSchema.parse(body); // Zod validation

  // 2. Validate origin (CSRF protection)
  const origin = req.headers.get('origin');
  if (!ALLOWED_ORIGINS.includes(origin)) {
    return new Response('Forbidden', { status: 403 });
  }

  // 3. Create checkout session
  const session = await stripe.checkout.sessions.create({
    customer_email: email,
    mode: plan === 'lifetime' ? 'payment' : 'subscription',
    payment_method_types: ['card'],
    line_items: [
      {
        price: plan === 'lifetime'
          ? process.env.STRIPE_LIFETIME_PRICE_ID
          : process.env.STRIPE_SUBSCRIPTION_PRICE_ID,
        quantity: 1,
      },
    ],
    success_url: `${process.env.NEXT_PUBLIC_BASE_URL}/success?session_id={CHECKOUT_SESSION_ID}`,
    cancel_url: `${process.env.NEXT_PUBLIC_BASE_URL}/`,
    metadata: {
      plan,
      // Add any custom data you need
    },
  });

  // 4. Return checkout URL
  return new Response(JSON.stringify({ url: session.url }), { status: 200 });
}
```

### Common Checkout Mistakes
```typescript
// ❌ WRONG - Hardcoded URLs
success_url: 'http://localhost:3000/success',

// ✅ CORRECT - Environment variable
success_url: `${process.env.NEXT_PUBLIC_BASE_URL}/success`,

// ❌ WRONG - Using secret key in client
const stripe = Stripe('sk_live_...'); // Exposed to browser!

// ✅ CORRECT - Use publishable key in client
const stripe = Stripe(process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY!);
```

## Subscription Lifecycle Events

### Key Events to Handle
```typescript
switch (event.type) {
  case 'customer.subscription.created':
    // New subscription - create license
    break;

  case 'customer.subscription.updated':
    // Subscription changed - update license tier or status
    break;

  case 'customer.subscription.deleted':
    // Subscription canceled - expire license
    break;

  case 'invoice.payment_succeeded':
    // Payment successful - extend license validity
    break;

  case 'invoice.payment_failed':
    // Payment failed - put license in grace period
    break;

  case 'customer.subscription.trial_will_end':
    // Trial ending soon - send notification
    break;
}
```

### Full Subscription Flow Example
```typescript
case 'customer.subscription.created': {
  const subscription = event.data.object as Stripe.Subscription;

  await db.licenses.create({
    data: {
      stripeSubscriptionId: subscription.id,
      stripeCustomerId: subscription.customer as string,
      status: 'active',
      tier: 'pro',
      currentPeriodEnd: new Date(subscription.current_period_end * 1000),
    },
  });

  // Send welcome email
  await sendWelcomeEmail(subscription.customer_email);
  break;
}

case 'invoice.payment_failed': {
  const invoice = event.data.object as Stripe.Invoice;
  const attemptCount = invoice.attempt_count;

  if (attemptCount === 1) {
    // First failure - friendly reminder
    await db.licenses.update({
      where: { stripeCustomerId: invoice.customer as string },
      data: {
        status: 'grace_period',
        gracePeriodEndsAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000), // 7 days
      },
    });

    await sendPaymentFailedEmail(invoice.customer_email, {
      reason: invoice.last_payment_error?.message,
      updateUrl: `${process.env.NEXT_PUBLIC_BASE_URL}/billing`,
    });
  } else if (attemptCount >= 3) {
    // Final warning before cancellation
    await sendFinalWarningEmail(invoice.customer_email);
  }
  break;
}

case 'customer.subscription.deleted': {
  const subscription = event.data.object as Stripe.Subscription;

  await db.licenses.update({
    where: { stripeSubscriptionId: subscription.id },
    data: {
      status: 'expired',
      expiresAt: new Date(),
    },
  });

  await sendCancellationEmail(subscription.customer_email);
  break;
}
```

## Customer Portal

### Setup Customer Portal Session
```typescript
export async function POST(req: Request) {
  const { customerId } = await req.json();

  const session = await stripe.billingPortal.sessions.create({
    customer: customerId,
    return_url: `${process.env.NEXT_PUBLIC_BASE_URL}/account`,
  });

  return new Response(JSON.stringify({ url: session.url }), { status: 200 });
}
```

### Portal Features
- Update payment method
- Cancel subscription
- View invoices
- Download receipts
- Update billing address

## Testing with Stripe CLI

### Local Webhook Testing
```bash
# 1. Install Stripe CLI
scoop install stripe  # Windows
brew install stripe/stripe-cli/stripe  # Mac

# 2. Login
stripe login

# 3. Forward webhooks to local server
stripe listen --forward-to localhost:3000/api/webhook

# Output: Your webhook signing secret is whsec_...
# Copy this to .env.local as STRIPE_WEBHOOK_SECRET
```

### Trigger Test Events
```bash
# Test subscription created
stripe trigger customer.subscription.created

# Test payment failed
stripe trigger invoice.payment_failed

# Test subscription canceled
stripe trigger customer.subscription.deleted

# Test payment succeeded
stripe trigger payment_intent.succeeded
```

### Test Card Numbers
```
Success: 4242 4242 4242 4242
Decline: 4000 0000 0000 0002
Insufficient funds: 4000 0000 0000 9995
Expired card: 4000 0000 0000 0069
Processing error: 4000 0000 0000 0119
```

## Error Handling

### Stripe API Error Types
```typescript
try {
  const session = await stripe.checkout.sessions.create({...});
} catch (err) {
  if (err instanceof Stripe.errors.StripeCardError) {
    // Card declined
    return new Response(JSON.stringify({
      error: 'Your card was declined.'
    }), { status: 400 });
  } else if (err instanceof Stripe.errors.StripeInvalidRequestError) {
    // Invalid parameters
    console.error('Invalid request:', err.message);
    return new Response(JSON.stringify({
      error: 'Invalid request.'
    }), { status: 400 });
  } else if (err instanceof Stripe.errors.StripeAPIError) {
    // Stripe API issue
    console.error('Stripe API error:', err.message);
    return new Response(JSON.stringify({
      error: 'Payment service unavailable.'
    }), { status: 503 });
  } else if (err instanceof Stripe.errors.StripeConnectionError) {
    // Network issue
    console.error('Stripe connection error:', err.message);
    return new Response(JSON.stringify({
      error: 'Network error. Please try again.'
    }), { status: 503 });
  } else {
    // Unknown error
    console.error('Unknown error:', err);
    return new Response(JSON.stringify({
      error: 'An unexpected error occurred.'
    }), { status: 500 });
  }
}
```

### User-Friendly Error Messages
```typescript
const errorMessages: Record<string, string> = {
  card_declined: 'Your card was declined. Please use a different payment method.',
  expired_card: 'Your card has expired. Please update your payment information.',
  insufficient_funds: 'Insufficient funds. Please use a different payment method.',
  incorrect_cvc: 'The security code is incorrect.',
  processing_error: 'An error occurred while processing your card. Please try again.',
};

const userMessage = errorMessages[err.code] || 'An unexpected error occurred.';
```

## Rate Limiting

### Protect Checkout Endpoint
```typescript
import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

const ratelimit = new Ratelimit({
  redis: Redis.fromEnv(),
  limiter: Ratelimit.slidingWindow(10, '1 m'), // 10 requests per minute
});

export async function POST(req: Request) {
  const ip = req.headers.get('x-forwarded-for') ?? 'anonymous';
  const { success } = await ratelimit.limit(ip);

  if (!success) {
    return new Response('Too many requests', { status: 429 });
  }

  // Process checkout...
}
```

### Webhook Rate Limiting
```typescript
// Don't rate-limit webhooks by IP (Stripe uses different IPs)
// Instead, rate-limit by customer ID if needed
const customerId = event.data.object.customer as string;
const { success } = await ratelimit.limit(customerId);
```

## Pricing Consistency

### Single Source of Truth
```typescript
// config/pricing.ts
export const PRICING = {
  subscription: {
    priceId: process.env.STRIPE_SUBSCRIPTION_PRICE_ID!,
    amount: 700, // $7.00 in cents
    interval: 'month',
    currency: 'usd',
  },
  lifetime: {
    priceId: process.env.STRIPE_LIFETIME_PRICE_ID!,
    amount: 9900, // $99.00 in cents
    currency: 'usd',
  },
} as const;
```

### Verify Prices in Tests
```typescript
// tests/pricing.test.ts
test('Stripe prices match documented pricing', async () => {
  const subscriptionPrice = await stripe.prices.retrieve(
    PRICING.subscription.priceId
  );

  expect(subscriptionPrice.unit_amount).toBe(700);
  expect(subscriptionPrice.recurring?.interval).toBe('month');
});
```

## Security Checklist

### Before Deploying Stripe Integration
- [ ] Webhook signature verification enabled
- [ ] No hardcoded secret keys in code
- [ ] All secrets in environment variables
- [ ] CSRF protection (origin validation)
- [ ] Rate limiting on checkout endpoint
- [ ] Input validation with Zod/Joi
- [ ] HTTPS enforced (no HTTP URLs)
- [ ] Customer portal using test configuration in development
- [ ] Webhook endpoint returns 200 OK for all events
- [ ] Idempotent event handling (database constraints or tracking)
- [ ] Error handling doesn't expose internal details
- [ ] Test mode keys used in development
- [ ] Production keys stored securely (e.g., Vercel secrets)

## Common Issues & Fixes

### Issue: Webhook Not Receiving Events
**Diagnosis**:
```bash
# Check webhook endpoint in Stripe Dashboard
# Developers > Webhooks > [Your endpoint] > Events
```

**Fixes**:
1. Ensure endpoint URL is correct (e.g., `https://voicelite.app/api/webhook`)
2. Check webhook events are selected (customer.subscription.*, invoice.*)
3. Verify webhook signature secret matches `.env` variable
4. Check server logs for errors

### Issue: Signature Verification Fails
**Causes**:
- Wrong webhook secret
- Request body modified before verification
- Using `req.json()` instead of `req.text()`

**Fix**:
```typescript
// ✅ CORRECT - Use raw body
const body = await req.text();
stripe.webhooks.constructEvent(body, sig, secret);

// ❌ WRONG - Parsed body
const body = await req.json();
stripe.webhooks.constructEvent(JSON.stringify(body), sig, secret); // Will fail!
```

### Issue: Duplicate License Creation
**Cause**: Event sent multiple times, no idempotency handling

**Fix**: Add unique constraint on `stripeSubscriptionId` in database schema
```prisma
model License {
  id                   String   @id @default(cuid())
  stripeSubscriptionId String   @unique // Prevents duplicates
  // ...
}
```

## References
- Stripe API Documentation: https://stripe.com/docs/api
- Webhooks Guide: https://stripe.com/docs/webhooks
- Testing Guide: https://stripe.com/docs/testing
- Checkout Session API: https://stripe.com/docs/api/checkout/sessions
- Customer Portal: https://stripe.com/docs/billing/subscriptions/customer-portal
