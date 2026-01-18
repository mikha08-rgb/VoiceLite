import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { sendLicenseEmail } from '@/lib/emails/license-email';
import { isAdminAuthenticated } from '@/lib/admin-auth';

export const dynamic = 'force-dynamic';

export async function GET(request: NextRequest) {
  // CRIT-1 FIX: Require admin authentication
  if (!isAdminAuthenticated(request)) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }
  const results: any = {
    timestamp: new Date().toISOString(),
    checks: {},
  };

  // Check 1: Environment variables
  results.checks.envVars = {
    DATABASE_URL: process.env.DATABASE_URL ? '✅ Set' : '❌ Missing',
    RESEND_API_KEY: process.env.RESEND_API_KEY ? '✅ Set' : '❌ Missing',
    RESEND_FROM_EMAIL: process.env.RESEND_FROM_EMAIL || 'Not set (will use default)',
    STRIPE_SECRET_KEY: process.env.STRIPE_SECRET_KEY ? '✅ Set' : '❌ Missing',
    STRIPE_WEBHOOK_SECRET: process.env.STRIPE_WEBHOOK_SECRET ? '✅ Set' : '❌ Missing',
  };

  // Check 2: Database connection
  try {
    await prisma.$connect();
    const count = await prisma.license.count();
    results.checks.database = {
      status: '✅ Connected',
      licenseCount: count,
    };
  } catch (error: any) {
    results.checks.database = {
      status: '❌ Failed',
      error: error.message,
    };
  }

  // Check 3: Test email sending (optional, only if ?testEmail=true)
  const testEmail = request.nextUrl.searchParams.get('testEmail');
  if (testEmail === 'true') {
    // HIGH-4 FIX: Use env variable instead of hardcoded email
    const diagnosticEmail = process.env.DIAGNOSTIC_TEST_EMAIL || process.env.RESEND_FROM_EMAIL;
    if (!diagnosticEmail) {
      results.checks.email = {
        status: '❌ Skipped',
        error: 'DIAGNOSTIC_TEST_EMAIL or RESEND_FROM_EMAIL not configured',
      };
    } else {
      try {
        const emailResult = await sendLicenseEmail({
          email: diagnosticEmail,
          licenseKey: 'DIAGNOSTIC-TEST-KEY',
        });

        results.checks.email = {
          status: emailResult.success ? '✅ Success' : '❌ Failed',
          messageId: emailResult.messageId,
          error: emailResult.error,
        };
      } catch (error: any) {
        results.checks.email = {
          status: '❌ Failed',
          error: error.message,
        };
      }
    }
  }

  // Check 4: Recent licenses
  // CRIT-5 FIX: Filter sensitive fields (licenseKey, full email) from diagnostic response
  try {
    const recentLicenses = await prisma.license.findMany({
      take: 5,
      orderBy: { createdAt: 'desc' },
      select: {
        id: true,
        email: true,
        type: true,
        status: true,
        createdAt: true,
      },
    });

    // CRIT-5 FIX: Mask email addresses (show only domain) and remove license keys
    const maskedLicenses = recentLicenses.map((license) => ({
      id: license.id,
      email: license.email ? `***@${license.email.split('@')[1] || 'unknown'}` : 'unknown',
      type: license.type,
      status: license.status,
      createdAt: license.createdAt,
    }));

    results.checks.recentLicenses = {
      status: '✅ Retrieved',
      count: recentLicenses.length,
      licenses: maskedLicenses,
    };
  } catch (error: any) {
    results.checks.recentLicenses = {
      status: '❌ Failed',
      error: error.message,
    };
  }

  return NextResponse.json(results, { status: 200 });
}
