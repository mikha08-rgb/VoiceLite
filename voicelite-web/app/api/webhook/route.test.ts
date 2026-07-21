import assert from 'node:assert/strict';
import test from 'node:test';
import { NextRequest } from 'next/server';
import Stripe from 'stripe';
import { prisma } from '@/lib/prisma';
import { POST } from './route';

const WEBHOOK_SECRET = 'whsec_route_test';

type WebhookRow = {
  eventId: string;
  seenAt: Date;
  processedAt: Date;
};

type MockState = {
  webhookRows: Map<string, WebhookRow>;
  licenseUpserts: number;
  licenseUpdates: Array<Record<string, unknown>>;
  licenseEvents: Array<{ type: string; licenseId: string }>;
};

function makeEvent(type: string, object: Record<string, unknown>, id: string): Stripe.Event {
  return {
    id,
    object: 'event',
    api_version: '2025-09-30.clover',
    created: Math.floor(Date.now() / 1000),
    data: { object } as Stripe.Event.Data,
    livemode: false,
    pending_webhooks: 1,
    request: null,
    type,
  } as Stripe.Event;
}

function makeRequest(event: Stripe.Event): NextRequest {
  const payload = JSON.stringify(event);
  const signature = Stripe.webhooks.generateTestHeaderString({
    payload,
    secret: WEBHOOK_SECRET,
  });

  return new NextRequest('http://localhost/api/webhook', {
    method: 'POST',
    body: payload,
    headers: {
      'content-type': 'application/json',
      'stripe-signature': signature,
    },
  });
}

function installPrismaMocks(): MockState {
  const db = prisma as any;
  const state: MockState = {
    webhookRows: new Map(),
    licenseUpserts: 0,
    licenseUpdates: [],
    licenseEvents: [],
  };

  db.webhookEvent.create = async ({ data }: any) => {
    if (state.webhookRows.has(data.eventId)) {
      throw Object.assign(new Error('duplicate event'), { code: 'P2002' });
    }
    state.webhookRows.set(data.eventId, { ...data });
    return data;
  };
  db.webhookEvent.findUnique = async ({ where }: any) => state.webhookRows.get(where.eventId) ?? null;
  db.webhookEvent.updateMany = async () => ({ count: 0 });
  db.webhookEvent.delete = async ({ where }: any) => {
    state.webhookRows.delete(where.eventId);
    return {};
  };
  db.webhookEvent.update = async ({ where, data }: any) => {
    const existing = state.webhookRows.get(where.eventId);
    if (!existing) throw new Error('missing webhook row');
    const updated = { ...existing, ...data };
    state.webhookRows.set(where.eventId, updated);
    return updated;
  };

  // The default makes checkout tests skip actual email delivery while still
  // exercising the fulfillment path and its audit event.
  db.licenseEvent.findFirst = async () => ({ createdAt: new Date() });
  db.licenseEvent.create = async ({ data }: any) => {
    state.licenseEvents.push({ type: data.type, licenseId: data.licenseId });
    return data;
  };
  db.license.findFirst = async () => ({ id: 'license_refund' });
  db.license.update = async ({ where, data }: any) => {
    state.licenseUpdates.push({ where, data });
    return { id: where.id, ...data };
  };
  db.$transaction = async (work: (tx: any) => Promise<unknown>) => {
    state.licenseUpserts += 1;
    return work({
      user: {
        upsert: async () => ({ id: 'user_1' }),
      },
      license: {
        upsert: async () => ({ id: 'license_checkout', licenseKey: 'VL-TESTXX-TESTXX-TESTXX' }),
      },
    });
  };

  return state;
}

function lifetimeCheckout(id: string): Stripe.Event {
  return makeEvent('checkout.session.completed', {
    id: `cs_${id}`,
    object: 'checkout.session',
    customer: 'cus_test',
    customer_email: 'buyer@example.com',
    metadata: { plan: 'lifetime' },
    mode: 'payment',
    payment_intent: `pi_${id}`,
    amount_total: 2000,
  }, id);
}

test.before(() => {
  process.env.STRIPE_SECRET_KEY = 'sk_test_route';
  process.env.STRIPE_WEBHOOK_SECRET = WEBHOOK_SECRET;
});

test('duplicate delivery fulfills once and returns the cached result', async () => {
  const state = installPrismaMocks();
  const event = lifetimeCheckout('evt_duplicate');

  const first = await POST(makeRequest(event));
  const second = await POST(makeRequest(event));

  assert.equal(first.status, 200);
  assert.equal(second.status, 200);
  assert.equal((await second.json()).cached, true);
  assert.equal(state.licenseUpserts, 1);
});

test('partial refund keeps the license while a full refund revokes it', async () => {
  const state = installPrismaMocks();
  const partial = makeEvent('charge.refunded', {
    id: 'ch_partial',
    object: 'charge',
    payment_intent: 'pi_partial',
    amount: 2000,
    amount_refunded: 500,
  }, 'evt_partial_refund');
  const full = makeEvent('charge.refunded', {
    id: 'ch_full',
    object: 'charge',
    payment_intent: 'pi_full',
    amount: 2000,
    amount_refunded: 2000,
  }, 'evt_full_refund');

  assert.equal((await POST(makeRequest(partial))).status, 200);
  assert.equal(state.licenseUpdates.length, 0);
  assert.deepEqual(state.licenseEvents.map(event => event.type), ['charge_partially_refunded']);

  assert.equal((await POST(makeRequest(full))).status, 200);
  assert.equal(state.licenseUpdates.length, 1);
  assert.deepEqual(state.licenseEvents.map(event => event.type), [
    'charge_partially_refunded',
    'revoked',
  ]);
});

test('DB failure before license creation returns 500 and releases the claim', async () => {
  const state = installPrismaMocks();
  (prisma as any).$transaction = async () => {
    state.licenseUpserts += 1;
    throw new Error('database unavailable before license create');
  };
  const event = lifetimeCheckout('evt_db_before_create');

  const response = await POST(makeRequest(event));

  assert.equal(response.status, 500);
  assert.equal(state.licenseUpserts, 1);
  assert.equal(state.webhookRows.has(event.id), false);
});

test('DB failure after license creation but before email returns 500 and releases the claim', async () => {
  const state = installPrismaMocks();
  (prisma as any).licenseEvent.findFirst = async () => {
    throw new Error('database unavailable during email rate-limit check');
  };
  const event = lifetimeCheckout('evt_db_before_email');

  const response = await POST(makeRequest(event));

  assert.equal(response.status, 500);
  assert.equal(state.licenseUpserts, 1);
  assert.equal(state.webhookRows.has(event.id), false);
});

test('DB failure while recording an email failure returns 500 and releases the claim', async () => {
  const state = installPrismaMocks();
  (prisma as any).licenseEvent.findFirst = async () => null;
  (prisma as any).licenseEvent.create = async ({ data }: any) => {
    if (data.type === 'email_failed') {
      throw new Error('database unavailable recording email failure');
    }
    return data;
  };
  delete process.env.RESEND_FROM_EMAIL;
  const event = lifetimeCheckout('evt_db_record_email_failure');

  const response = await POST(makeRequest(event));

  assert.equal(response.status, 500);
  assert.equal(state.licenseUpserts, 1);
  assert.equal(state.webhookRows.has(event.id), false);
});
