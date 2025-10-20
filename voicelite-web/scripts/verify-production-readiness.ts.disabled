/**
 * VoiceLite Production Readiness Verification Script
 * Checks all services and configurations before deployment
 */

import { PrismaClient } from '@prisma/client';

interface CheckResult {
  name: string;
  status: 'PASS' | 'FAIL' | 'WARN' | 'SKIP';
  message: string;
  details?: string;
}

const results: CheckResult[] = [];

function check(result: CheckResult) {
  results.push(result);
  const icon = {
    PASS: '✅',
    FAIL: '❌',
    WARN: '⚠️',
    SKIP: '⏭️'
  }[result.status];
  console.log(`${icon} ${result.name}: ${result.message}`);
  if (result.details) {
    console.log(`   ${result.details}`);
  }
}

async function verifyEnvironmentVariables() {
  console.log('\n📋 Checking Environment Variables...\n');

  const requiredVars = [
    'DATABASE_URL',
    'LICENSE_SIGNING_PRIVATE',
    'LICENSE_SIGNING_PUBLIC',
    'CRL_SIGNING_PRIVATE',
    'CRL_SIGNING_PUBLIC',
    'STRIPE_SECRET_KEY',
    'NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY',
    'RESEND_API_KEY',
    'RESEND_FROM_EMAIL',
  ];

  const optionalVars = [
    'UPSTASH_REDIS_REST_URL',
    'UPSTASH_REDIS_REST_TOKEN',
    'STRIPE_WEBHOOK_SECRET',
    'STRIPE_QUARTERLY_PRICE_ID',
    'STRIPE_LIFETIME_PRICE_ID',
  ];

  for (const varName of requiredVars) {
    if (process.env[varName]) {
      check({
        name: varName,
        status: 'PASS',
        message: 'Configured',
        details: process.env[varName]?.substring(0, 20) + '...',
      });
    } else {
      check({
        name: varName,
        status: 'FAIL',
        message: 'MISSING - Required for production',
      });
    }
  }

  for (const varName of optionalVars) {
    if (process.env[varName]) {
      check({
        name: varName,
        status: 'PASS',
        message: 'Configured',
      });
    } else {
      check({
        name: varName,
        status: 'WARN',
        message: 'Not set (optional but recommended)',
      });
    }
  }

  // Check if using test vs production keys
  const stripeKey = process.env.STRIPE_SECRET_KEY || '';
  if (stripeKey.startsWith('sk_test_')) {
    check({
      name: 'Stripe Mode',
      status: 'WARN',
      message: 'Using TEST mode keys',
      details: 'Switch to sk_live_* for production',
    });
  } else if (stripeKey.startsWith('sk_live_')) {
    check({
      name: 'Stripe Mode',
      status: 'PASS',
      message: 'Using LIVE mode keys',
    });
  }
}

async function verifyDatabase() {
  console.log('\n🗄️  Checking Database Connection...\n');

  try {
    const prisma = new PrismaClient();
    await prisma.$connect();

    check({
      name: 'Database Connection',
      status: 'PASS',
      message: 'Connected successfully',
    });

    // Check if migrations are applied
    const tables = await prisma.$queryRaw<any[]>`
      SELECT table_name
      FROM information_schema.tables
      WHERE table_schema = 'public'
    `;

    const expectedTables = [
      'User',
      'Session',
      'MagicLinkToken',
      'License',
      'WebhookEvent',
    ];

    const tableNames = tables.map((t) => t.table_name);
    const missingTables = expectedTables.filter(
      (t) => !tableNames.includes(t)
    );

    if (missingTables.length === 0) {
      check({
        name: 'Database Schema',
        status: 'PASS',
        message: `All ${expectedTables.length} required tables exist`,
        details: tableNames.join(', '),
      });
    } else {
      check({
        name: 'Database Schema',
        status: 'FAIL',
        message: `Missing tables: ${missingTables.join(', ')}`,
        details: 'Run: npx prisma migrate deploy',
      });
    }

    // Check for test data
    const userCount = await prisma.user.count();
    check({
      name: 'Database Users',
      status: 'PASS',
      message: `${userCount} users in database`,
    });

    await prisma.$disconnect();
  } catch (error: any) {
    check({
      name: 'Database Connection',
      status: 'FAIL',
      message: error.message,
      details: 'Check DATABASE_URL and network connectivity',
    });
  }
}

async function verifyStripe() {
  console.log('\n💳 Checking Stripe Configuration...\n');

  const stripeKey = process.env.STRIPE_SECRET_KEY;
  if (!stripeKey) {
    check({
      name: 'Stripe',
      status: 'SKIP',
      message: 'No API key configured',
    });
    return;
  }

  try {
    const response = await fetch('https://api.stripe.com/v1/products?limit=5', {
      headers: {
        Authorization: `Bearer ${stripeKey}`,
      },
    });

    if (response.ok) {
      const data = await response.json();
      check({
        name: 'Stripe Connection',
        status: 'PASS',
        message: `Connected (${data.data.length} products found)`,
      });

      // Check if price IDs are configured
      const quarterlyId = process.env.STRIPE_QUARTERLY_PRICE_ID;
      const lifetimeId = process.env.STRIPE_LIFETIME_PRICE_ID;

      if (quarterlyId && lifetimeId) {
        check({
          name: 'Stripe Products',
          status: 'PASS',
          message: 'Quarterly and Lifetime price IDs configured',
        });
      } else {
        check({
          name: 'Stripe Products',
          status: 'WARN',
          message: 'Price IDs not configured',
          details: 'Set STRIPE_QUARTERLY_PRICE_ID and STRIPE_LIFETIME_PRICE_ID',
        });
      }
    } else {
      check({
        name: 'Stripe Connection',
        status: 'FAIL',
        message: `HTTP ${response.status}: ${response.statusText}`,
      });
    }
  } catch (error: any) {
    check({
      name: 'Stripe Connection',
      status: 'FAIL',
      message: error.message,
    });
  }
}

async function verifyResend() {
  console.log('\n📧 Checking Resend Email Service...\n');

  const apiKey = process.env.RESEND_API_KEY;
  if (!apiKey) {
    check({
      name: 'Resend',
      status: 'SKIP',
      message: 'No API key configured',
    });
    return;
  }

  try {
    const response = await fetch('https://api.resend.com/domains', {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
    });

    if (response.ok) {
      const data = await response.json();
      const domains = data.data || [];
      const verifiedDomains = domains.filter((d: any) => d.status === 'verified');

      if (verifiedDomains.length > 0) {
        check({
          name: 'Resend Connection',
          status: 'PASS',
          message: `Connected (${verifiedDomains.length} verified domains)`,
          details: verifiedDomains.map((d: any) => d.name).join(', '),
        });
      } else {
        check({
          name: 'Resend Connection',
          status: 'WARN',
          message: 'No verified domains',
          details: 'Verify your domain in Resend dashboard',
        });
      }
    } else {
      check({
        name: 'Resend Connection',
        status: 'FAIL',
        message: `HTTP ${response.status}`,
      });
    }
  } catch (error: any) {
    check({
      name: 'Resend Connection',
      status: 'FAIL',
      message: error.message,
    });
  }
}

async function verifyRedis() {
  console.log('\n🔴 Checking Upstash Redis...\n');

  const url = process.env.UPSTASH_REDIS_REST_URL;
  const token = process.env.UPSTASH_REDIS_REST_TOKEN;

  if (!url || !token) {
    check({
      name: 'Upstash Redis',
      status: 'WARN',
      message: 'Not configured (rate limiting will use in-memory fallback)',
    });
    return;
  }

  try {
    const response = await fetch(`${url}/ping`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (response.ok) {
      const data = await response.json();
      if (data.result === 'PONG') {
        check({
          name: 'Upstash Redis',
          status: 'PASS',
          message: 'Connected successfully',
        });
      }
    } else {
      check({
        name: 'Upstash Redis',
        status: 'FAIL',
        message: `HTTP ${response.status}`,
      });
    }
  } catch (error: any) {
    check({
      name: 'Upstash Redis',
      status: 'FAIL',
      message: error.message,
    });
  }
}

async function verifyDesktopAppConfig() {
  console.log('\n🖥️  Checking Desktop App Configuration...\n');

  // Check if public keys in .env match what the desktop app expects
  const licensePublicKey = process.env.LICENSE_SIGNING_PUBLIC;

  if (licensePublicKey) {
    check({
      name: 'License Public Key',
      status: 'PASS',
      message: 'Configured in environment',
      details: 'Verify this matches VoiceLite/Services/LicenseValidator.cs',
    });
  } else {
    check({
      name: 'License Public Key',
      status: 'FAIL',
      message: 'Not configured',
    });
  }
}

async function generateSummary() {
  console.log('\n' + '='.repeat(80));
  console.log('📊 PRODUCTION READINESS SUMMARY');
  console.log('='.repeat(80) + '\n');

  const passed = results.filter((r) => r.status === 'PASS').length;
  const failed = results.filter((r) => r.status === 'FAIL').length;
  const warned = results.filter((r) => r.status === 'WARN').length;
  const skipped = results.filter((r) => r.status === 'SKIP').length;

  console.log(`✅ Passed:  ${passed}`);
  console.log(`❌ Failed:  ${failed}`);
  console.log(`⚠️  Warnings: ${warned}`);
  console.log(`⏭️  Skipped: ${skipped}`);
  console.log(`📈 Total:   ${results.length}\n`);

  if (failed === 0 && warned === 0) {
    console.log('🎉 All checks passed! You are ready for production deployment.\n');
    console.log('Next steps:');
    console.log('1. Deploy to Vercel: cd voicelite-web && vercel --prod');
    console.log('2. Build desktop installer: dotnet publish -c Release');
    console.log('3. Test end-to-end user flow\n');
  } else if (failed > 0) {
    console.log('🚫 BLOCKED: Critical issues found. Fix failed checks before deploying.\n');
    console.log('Failed checks:');
    results
      .filter((r) => r.status === 'FAIL')
      .forEach((r) => {
        console.log(`  • ${r.name}: ${r.message}`);
        if (r.details) console.log(`    ${r.details}`);
      });
    console.log();
    process.exit(1);
  } else if (warned > 0) {
    console.log('⚠️  WARNINGS: Some optional features are not configured.\n');
    console.log('Warnings:');
    results
      .filter((r) => r.status === 'WARN')
      .forEach((r) => {
        console.log(`  • ${r.name}: ${r.message}`);
      });
    console.log('\nYou can proceed with deployment, but consider addressing these warnings.\n');
  }
}

async function main() {
  console.log('🚀 VoiceLite Production Readiness Check\n');
  console.log('Environment:', process.env.NODE_ENV || 'development');
  console.log('Time:', new Date().toISOString());
  console.log();

  await verifyEnvironmentVariables();
  await verifyDatabase();
  await verifyStripe();
  await verifyResend();
  await verifyRedis();
  await verifyDesktopAppConfig();
  await generateSummary();
}

main().catch(console.error);
