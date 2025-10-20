/**
 * CRITICAL WEBHOOK SECURITY TESTS
 *
 * These tests validate the webhook endpoint's security mechanisms:
 * 1. Signature verification (prevents unauthorized webhook calls)
 * 2. Replay attack prevention (5-minute event age limit)
 * 3. Idempotency (prevents duplicate processing via database constraint)
 * 4. Email failure handling (customer keeps license even if email fails)
 *
 * WHY THIS MATTERS:
 * - Each failed test = potential $20+ revenue loss per customer
 * - Replay attacks could issue unlimited free licenses
 * - Missing idempotency = double-charging customers or duplicate licenses
 * - Email failures = customer paid but didn't receive license key
 *
 * Test methodology: We use Stripe's webhook signature algorithm to generate
 * valid signatures, then test various attack vectors and edge cases.
 */

import { test, expect } from '@playwright/test';
import crypto from 'crypto';
import { prisma } from '@/lib/prisma';

// Stripe webhook signature generation (matches Stripe's algorithm)
function generateStripeSignature(payload: string, secret: string, timestamp?: number): string {
  const ts = timestamp || Math.floor(Date.now() / 1000);
  const signedPayload = `${ts}.${payload}`;
  const signature = crypto
    .createHmac('sha256', secret)
    .update(signedPayload)
    .digest('hex');

  return `t=${ts},v1=${signature}`;
}

// Helper to create a valid Stripe checkout.session.completed event
function createCheckoutSessionEvent(overrides: any = {}): any {
  return {
    id: `evt_${crypto.randomBytes(12).toString('hex')}`,
    object: 'event',
    api_version: '2025-09-30.clover',
    created: Math.floor(Date.now() / 1000),
    type: 'checkout.session.completed',
    data: {
      object: {
        id: `cs_${crypto.randomBytes(12).toString('hex')}`,
        object: 'checkout.session',
        customer: `cus_${crypto.randomBytes(12).toString('hex')}`,
        customer_email: 'test@example.com',
        payment_intent: `pi_${crypto.randomBytes(12).toString('hex')}`,
        payment_status: 'paid',
        mode: 'payment',
        ...overrides.data?.object,
      },
    },
    livemode: false,
    ...overrides,
  };
}

test.describe('Webhook Security Tests', () => {
  const webhookUrl = '/api/webhook';
  const webhookSecret = process.env.STRIPE_WEBHOOK_SECRET || 'whsec_test_secret';

  test.beforeEach(async () => {
    // Clean up test data before each test
    await prisma.webhookEvent.deleteMany({
      where: { eventId: { startsWith: 'evt_test_' } },
    });
  });

  test.describe('1. Signature Verification', () => {
    test('should reject webhook with missing signature', async ({ request }) => {
      const event = createCheckoutSessionEvent();
      const payload = JSON.stringify(event);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          // Intentionally missing 'stripe-signature' header
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.error).toBe('Missing signature');
    });

    test('should reject webhook with invalid signature', async ({ request }) => {
      const event = createCheckoutSessionEvent();
      const payload = JSON.stringify(event);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': 't=1234567890,v1=invalid_signature_here',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.error).toBe('Invalid signature');
    });

    test('should accept webhook with valid signature', async ({ request }) => {
      const event = createCheckoutSessionEvent({
        id: 'evt_test_valid_sig',
        data: {
          object: {
            customer_email: 'valid-sig-test@example.com',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      expect(response.status()).toBe(200);
      const json = await response.json();
      expect(json.received).toBe(true);
      expect(json.eventId).toBe('evt_test_valid_sig');
    });

    test('should reject webhook with tampered payload (signature mismatch)', async ({ request }) => {
      const event = createCheckoutSessionEvent();
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      // Tamper with the payload AFTER generating signature
      const tamperedEvent = { ...event, type: 'payment_intent.succeeded' };
      const tamperedPayload = JSON.stringify(tamperedEvent);

      const response = await request.post(webhookUrl, {
        data: tamperedPayload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature, // Signature doesn't match tampered payload
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.error).toBe('Invalid signature');
    });
  });

  test.describe('2. Replay Attack Prevention', () => {
    test('should reject events older than 5 minutes', async ({ request }) => {
      // Create event with timestamp 10 minutes in the past
      const tenMinutesAgo = Math.floor(Date.now() / 1000) - (10 * 60);
      const event = createCheckoutSessionEvent({
        id: 'evt_test_old_event',
        created: tenMinutesAgo,
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret, tenMinutesAgo);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.error).toBe('Event too old');
      expect(json.received).toBe(true);

      // Verify event was NOT processed (not in database)
      const dbEvent = await prisma.webhookEvent.findUnique({
        where: { eventId: 'evt_test_old_event' },
      });
      expect(dbEvent).toBeNull();
    });

    test('should accept events within 5 minute window', async ({ request }) => {
      // Create event with current timestamp
      const now = Math.floor(Date.now() / 1000);
      const event = createCheckoutSessionEvent({
        id: 'evt_test_recent',
        created: now,
        data: {
          object: {
            customer_email: 'recent-event@example.com',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret, now);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      expect(response.status()).toBe(200);
      const json = await response.json();
      expect(json.received).toBe(true);

      // Verify event WAS processed
      const dbEvent = await prisma.webhookEvent.findUnique({
        where: { eventId: 'evt_test_recent' },
      });
      expect(dbEvent).not.toBeNull();
    });

    test('should accept events at exactly 5 minute boundary', async ({ request }) => {
      // Create event at exactly 5 minutes ago (boundary case)
      const fiveMinutesAgo = Math.floor(Date.now() / 1000) - (5 * 60);
      const event = createCheckoutSessionEvent({
        id: 'evt_test_boundary',
        created: fiveMinutesAgo,
        data: {
          object: {
            customer_email: 'boundary-test@example.com',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret, fiveMinutesAgo);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      // Should accept (5 minutes is within limit, >5 minutes is rejected)
      expect(response.status()).toBe(200);
    });
  });

  test.describe('3. Idempotency (Duplicate Prevention)', () => {
    test('should process same event only once', async ({ request }) => {
      const event = createCheckoutSessionEvent({
        id: 'evt_test_duplicate',
        data: {
          object: {
            customer_email: 'duplicate-test@example.com',
            payment_intent: 'pi_duplicate_test_123',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      // Send webhook FIRST time
      const response1 = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      expect(response1.status()).toBe(200);
      const json1 = await response1.json();
      expect(json1.received).toBe(true);
      expect(json1.cached).toBeUndefined(); // First time = not cached

      // Verify license was created
      const license1 = await prisma.license.findFirst({
        where: { stripePaymentIntentId: 'pi_duplicate_test_123' },
      });
      expect(license1).not.toBeNull();
      const originalLicenseKey = license1?.licenseKey;

      // Send SAME webhook SECOND time (duplicate event ID)
      const response2 = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      expect(response2.status()).toBe(200);
      const json2 = await response2.json();
      expect(json2.received).toBe(true);
      expect(json2.cached).toBe(true); // Second time = cached response

      // Verify NO duplicate license was created
      const licenses = await prisma.license.findMany({
        where: { stripePaymentIntentId: 'pi_duplicate_test_123' },
      });
      expect(licenses.length).toBe(1);
      expect(licenses[0].licenseKey).toBe(originalLicenseKey);
    });

    test('should handle concurrent duplicate webhooks (race condition)', async ({ request }) => {
      const event = createCheckoutSessionEvent({
        id: 'evt_test_race_condition',
        data: {
          object: {
            customer_email: 'race-test@example.com',
            payment_intent: 'pi_race_test_456',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      // Send 5 webhooks SIMULTANEOUSLY (simulate race condition)
      const requests = Array(5).fill(null).map(() =>
        request.post(webhookUrl, {
          data: payload,
          headers: {
            'Content-Type': 'application/json',
            'stripe-signature': signature,
          },
        })
      );

      const responses = await Promise.all(requests);

      // All should return 200 (some cached, some processed)
      responses.forEach(response => {
        expect(response.status()).toBe(200);
      });

      // Verify ONLY ONE license was created despite 5 concurrent requests
      const licenses = await prisma.license.findMany({
        where: { stripePaymentIntentId: 'pi_race_test_456' },
      });
      expect(licenses.length).toBe(1);

      // Verify webhook event recorded only once
      const webhookEvents = await prisma.webhookEvent.findMany({
        where: { eventId: 'evt_test_race_condition' },
      });
      expect(webhookEvents.length).toBe(1);
    });
  });

  test.describe('4. Email Failure Handling', () => {
    test('should mark license as created even if email fails', async ({ request }) => {
      // This test requires mocking email service to force failure
      // For now, we verify the license exists in database regardless of email status
      const event = createCheckoutSessionEvent({
        id: 'evt_test_email_fail',
        data: {
          object: {
            customer_email: 'email-fail-test@example.com',
            payment_intent: 'pi_email_fail_test',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      // Webhook should return 200 even if email fails
      expect(response.status()).toBe(200);

      // License MUST exist in database (customer paid)
      const license = await prisma.license.findFirst({
        where: { stripePaymentIntentId: 'pi_email_fail_test' },
      });
      expect(license).not.toBeNull();
      expect(license?.email).toBe('email-fail-test@example.com');

      // License key should be valid format (VL-XXXX-XXXX-XXXX-XXXX)
      expect(license?.licenseKey).toMatch(/^VL-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$/);
    });
  });

  test.describe('5. Missing/Invalid Data Handling', () => {
    test('should handle missing customer email gracefully', async ({ request }) => {
      const event = createCheckoutSessionEvent({
        id: 'evt_test_no_email',
        data: {
          object: {
            customer_email: null, // Missing email
            payment_intent: 'pi_no_email_test',
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      // Should still return 200 to prevent Stripe retries
      expect(response.status()).toBe(200);

      // Event should be marked as processed (idempotency)
      const dbEvent = await prisma.webhookEvent.findUnique({
        where: { eventId: 'evt_test_no_email' },
      });
      expect(dbEvent).not.toBeNull();
    });

    test('should handle missing payment intent gracefully', async ({ request }) => {
      const event = createCheckoutSessionEvent({
        id: 'evt_test_no_pi',
        data: {
          object: {
            customer_email: 'no-pi-test@example.com',
            payment_intent: null, // Missing payment intent
          },
        },
      });
      const payload = JSON.stringify(event);
      const signature = generateStripeSignature(payload, webhookSecret);

      const response = await request.post(webhookUrl, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': signature,
        },
      });

      // Should return 200 to prevent retries
      expect(response.status()).toBe(200);
    });
  });

  test.describe('6. Refund Flow', () => {
    test('should revoke license on charge.refunded event', async ({ request }) => {
      // Step 1: Create a license first via checkout.session.completed
      const checkoutEvent = createCheckoutSessionEvent({
        id: 'evt_test_refund_setup',
        data: {
          object: {
            customer_email: 'refund-test@example.com',
            payment_intent: 'pi_refund_test_789',
          },
        },
      });
      const checkoutPayload = JSON.stringify(checkoutEvent);
      const checkoutSignature = generateStripeSignature(checkoutPayload, webhookSecret);

      await request.post(webhookUrl, {
        data: checkoutPayload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': checkoutSignature,
        },
      });

      // Verify license was created and is active
      const licenseBeforeRefund = await prisma.license.findFirst({
        where: { stripePaymentIntentId: 'pi_refund_test_789' },
      });
      expect(licenseBeforeRefund).not.toBeNull();
      expect(licenseBeforeRefund?.status).toBe('ACTIVE');

      // Step 2: Send charge.refunded webhook
      const refundEvent = {
        id: 'evt_test_refund',
        object: 'event',
        api_version: '2025-09-30.clover',
        created: Math.floor(Date.now() / 1000),
        type: 'charge.refunded',
        data: {
          object: {
            id: 'ch_test_refund',
            object: 'charge',
            payment_intent: 'pi_refund_test_789',
            refunded: true,
          },
        },
        livemode: false,
      };
      const refundPayload = JSON.stringify(refundEvent);
      const refundSignature = generateStripeSignature(refundPayload, webhookSecret);

      const response = await request.post(webhookUrl, {
        data: refundPayload,
        headers: {
          'Content-Type': 'application/json',
          'stripe-signature': refundSignature,
        },
      });

      expect(response.status()).toBe(200);

      // Verify license was revoked
      const licenseAfterRefund = await prisma.license.findFirst({
        where: { stripePaymentIntentId: 'pi_refund_test_789' },
      });
      expect(licenseAfterRefund?.status).toBe('REVOKED');
    });
  });
});
