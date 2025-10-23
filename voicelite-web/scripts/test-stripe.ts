#!/usr/bin/env tsx
/**
 * Stripe Setup Verification Script
 *
 * Tests that Stripe is properly configured and working.
 * Run this before deploying to production.
 *
 * Usage:
 *   npm run test-stripe
 */

import Stripe from 'stripe';
import * as dotenv from 'dotenv';
import * as path from 'path';

// Load environment variables
dotenv.config({ path: path.join(__dirname, '..', '.env.local') });

interface TestResult {
  name: string;
  status: 'PASS' | 'FAIL' | 'WARN';
  message: string;
}

const results: TestResult[] = [];

function logResult(result: TestResult) {
  results.push(result);
  const icon = result.status === 'PASS' ? '✅' : result.status === 'FAIL' ? '❌' : '⚠️';
  console.log(`${icon} ${result.name}: ${result.message}`);
}

async function main() {
  console.log('🔍 VoiceLite Stripe Configuration Test\n');

  // Test 1: Check environment variables
  console.log('Testing environment variables...');

  const secretKey = process.env.STRIPE_SECRET_KEY;
  const webhookSecret = process.env.STRIPE_WEBHOOK_SECRET;
  const publishableKey = process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY;

  if (!secretKey) {
    logResult({
      name: 'STRIPE_SECRET_KEY',
      status: 'FAIL',
      message: 'Missing - add to .env.local'
    });
  } else if (!secretKey.startsWith('sk_test_') && !secretKey.startsWith('sk_live_')) {
    logResult({
      name: 'STRIPE_SECRET_KEY',
      status: 'FAIL',
      message: 'Invalid format - should start with sk_test_ or sk_live_'
    });
  } else {
    const mode = secretKey.startsWith('sk_test_') ? 'TEST' : 'LIVE';
    logResult({
      name: 'STRIPE_SECRET_KEY',
      status: 'PASS',
      message: `Valid ${mode} mode key`
    });
  }

  if (!publishableKey) {
    logResult({
      name: 'NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY',
      status: 'FAIL',
      message: 'Missing - add to .env.local'
    });
  } else if (!publishableKey.startsWith('pk_test_') && !publishableKey.startsWith('pk_live_')) {
    logResult({
      name: 'NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY',
      status: 'FAIL',
      message: 'Invalid format - should start with pk_test_ or pk_live_'
    });
  } else {
    const mode = publishableKey.startsWith('pk_test_') ? 'TEST' : 'LIVE';
    logResult({
      name: 'NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY',
      status: 'PASS',
      message: `Valid ${mode} mode key`
    });
  }

  if (!webhookSecret) {
    logResult({
      name: 'STRIPE_WEBHOOK_SECRET',
      status: 'WARN',
      message: 'Missing - webhooks will fail (required for production)'
    });
  } else if (!webhookSecret.startsWith('whsec_')) {
    logResult({
      name: 'STRIPE_WEBHOOK_SECRET',
      status: 'FAIL',
      message: 'Invalid format - should start with whsec_'
    });
  } else {
    logResult({
      name: 'STRIPE_WEBHOOK_SECRET',
      status: 'PASS',
      message: 'Valid webhook secret'
    });
  }

  // Test 2: Verify key consistency (test vs live)
  console.log('\nTesting key consistency...');

  const secretIsTest = secretKey?.startsWith('sk_test_');
  const publishableIsTest = publishableKey?.startsWith('pk_test_');

  if (secretKey && publishableKey) {
    if (secretIsTest !== publishableIsTest) {
      logResult({
        name: 'Key Consistency',
        status: 'FAIL',
        message: 'Secret and publishable keys are from different modes (test/live)'
      });
    } else {
      logResult({
        name: 'Key Consistency',
        status: 'PASS',
        message: 'All keys are from the same mode'
      });
    }
  }

  // Test 3: Test Stripe API connection
  if (!secretKey || !secretKey.startsWith('sk_')) {
    console.log('\n⚠️ Skipping API tests - invalid secret key\n');
    printSummary();
    return;
  }

  console.log('\nTesting Stripe API connection...');

  try {
    const stripe = new Stripe(secretKey, {
      apiVersion: '2025-08-27.basil',
    });

    // Test API connection by retrieving account
    const account = await stripe.accounts.retrieve();

    logResult({
      name: 'API Connection',
      status: 'PASS',
      message: `Connected to Stripe account: ${account.email || account.id}`
    });

    logResult({
      name: 'Account Status',
      status: account.charges_enabled ? 'PASS' : 'WARN',
      message: account.charges_enabled
        ? 'Account can accept payments'
        : 'Account cannot accept payments yet - complete verification'
    });

    // Test 4: List products
    console.log('\nChecking products...');

    const products = await stripe.products.list({ limit: 10, active: true });

    if (products.data.length === 0) {
      logResult({
        name: 'Products',
        status: 'WARN',
        message: 'No products found - checkout will create product on-the-fly (this is OK)'
      });
    } else {
      const voiceLiteProduct = products.data.find(p =>
        p.name.toLowerCase().includes('voicelite')
      );

      if (voiceLiteProduct) {
        logResult({
          name: 'Products',
          status: 'PASS',
          message: `Found VoiceLite product: ${voiceLiteProduct.name}`
        });

        // List prices for this product
        const prices = await stripe.prices.list({
          product: voiceLiteProduct.id,
          active: true
        });

        if (prices.data.length > 0) {
          const price = prices.data[0];
          const amount = price.unit_amount ? price.unit_amount / 100 : 0;
          logResult({
            name: 'Product Price',
            status: 'PASS',
            message: `$${amount.toFixed(2)} ${price.currency.toUpperCase()}`
          });
        }
      } else {
        logResult({
          name: 'Products',
          status: 'WARN',
          message: `${products.data.length} products found, but no VoiceLite product (checkout will create on-the-fly)`
        });
      }
    }

    // Test 5: List webhooks
    console.log('\nChecking webhooks...');

    const webhooks = await stripe.webhookEndpoints.list({ limit: 10 });

    if (webhooks.data.length === 0) {
      logResult({
        name: 'Webhooks',
        status: 'WARN',
        message: 'No webhooks configured - license delivery will fail in production'
      });
      console.log('\n💡 To configure webhooks:');
      console.log('   1. For local dev: Run `stripe listen --forward-to localhost:3000/api/webhook`');
      console.log('   2. For production: Go to https://dashboard.stripe.com/webhooks');
    } else {
      const appWebhook = webhooks.data.find(w =>
        w.url.includes('voicelite.app') || w.url.includes('localhost:3000')
      );

      if (appWebhook) {
        const hasCheckout = appWebhook.enabled_events.includes('checkout.session.completed');
        const hasRefund = appWebhook.enabled_events.includes('charge.refunded');

        logResult({
          name: 'Webhook Endpoint',
          status: 'PASS',
          message: `Configured: ${appWebhook.url}`
        });

        logResult({
          name: 'Webhook Events',
          status: hasCheckout && hasRefund ? 'PASS' : 'WARN',
          message: hasCheckout && hasRefund
            ? 'All required events configured'
            : 'Missing required events: checkout.session.completed, charge.refunded'
        });
      } else {
        logResult({
          name: 'Webhooks',
          status: 'WARN',
          message: `${webhooks.data.length} webhooks found, but none for voicelite.app`
        });
      }
    }

    // Test 6: Create test checkout session
    console.log('\nTesting checkout session creation...');

    try {
      const session = await stripe.checkout.sessions.create({
        payment_method_types: ['card'],
        mode: 'payment',
        line_items: [
          {
            price_data: {
              currency: 'usd',
              product_data: {
                name: 'VoiceLite Pro',
                description: 'One-time purchase - Lifetime access to VoiceLite Pro features',
              },
              unit_amount: 2000, // $20.00
            },
            quantity: 1,
          },
        ],
        success_url: 'https://voicelite.app/checkout/success',
        cancel_url: 'https://voicelite.app/checkout/cancel',
        billing_address_collection: 'required',
      });

      logResult({
        name: 'Checkout Session',
        status: 'PASS',
        message: `Test session created successfully (${session.id})`
      });

      // Expire the test session to avoid clutter
      await stripe.checkout.sessions.expire(session.id);

    } catch (error: any) {
      logResult({
        name: 'Checkout Session',
        status: 'FAIL',
        message: `Failed to create session: ${error.message}`
      });
    }

  } catch (error: any) {
    logResult({
      name: 'API Connection',
      status: 'FAIL',
      message: `Failed to connect: ${error.message}`
    });
  }

  console.log('\n');
  printSummary();
}

function printSummary() {
  const passed = results.filter(r => r.status === 'PASS').length;
  const failed = results.filter(r => r.status === 'FAIL').length;
  const warnings = results.filter(r => r.status === 'WARN').length;

  console.log('━'.repeat(60));
  console.log('📊 Summary:');
  console.log(`   ✅ Passed: ${passed}`);
  console.log(`   ❌ Failed: ${failed}`);
  console.log(`   ⚠️  Warnings: ${warnings}`);
  console.log('━'.repeat(60));

  if (failed > 0) {
    console.log('\n❌ Stripe configuration has errors. Please fix before deploying.');
    console.log('📖 See STRIPE_SETUP_GUIDE.md for setup instructions.\n');
    process.exit(1);
  } else if (warnings > 0) {
    console.log('\n⚠️  Stripe configuration has warnings. Review before production.');
    console.log('📖 See STRIPE_SETUP_GUIDE.md for setup instructions.\n');
    process.exit(0);
  } else {
    console.log('\n✅ Stripe is properly configured and ready to use!\n');
    process.exit(0);
  }
}

// Run the tests
main().catch(error => {
  console.error('\n❌ Test script failed:', error);
  process.exit(1);
});
