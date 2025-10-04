import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { verifyAdmin } from '@/lib/admin-auth';
import { checkRateLimit, profileRateLimit } from '@/lib/ratelimit';

/**
 * Admin Analytics API Endpoint
 *
 * GET /api/admin/analytics?days=30
 *
 * Returns aggregated usage analytics for VoiceLite app.
 * Requires admin authentication via session cookie.
 *
 * Security features:
 * - Session-based admin authentication
 * - Rate limiting (100 requests/hour per admin)
 * - Input validation and sanitization
 * - SQL injection prevention (parameterized queries)
 * - Comprehensive error handling
 * - Privacy-first (only aggregated data, no PII)
 *
 * Performance optimizations:
 * - 5-minute server-side caching
 * - Parallel query execution (Promise.all)
 * - Database-level aggregations
 * - Indexed queries
 */

// Force dynamic rendering (uses cookies for auth)
export const dynamic = 'force-dynamic';

// Cache analytics data for 5 minutes (reduces DB load)
export const revalidate = 300; // 5 minutes

// Maximum allowed date range (prevent DB overload)
const MAX_DAYS = 365;
const MIN_DAYS = 1;
const DEFAULT_DAYS = 30;

// Query timeout (prevent long-running queries)
const QUERY_TIMEOUT_MS = 30000; // 30 seconds

export async function GET(req: NextRequest) {
  const startTime = Date.now();

  try {
    // 1. Verify admin access
    const { isAdmin, userId, email, error: authError } = await verifyAdmin(req);

    if (!isAdmin) {
      console.warn(`[Analytics] Unauthorized access attempt: ${authError}`);
      return NextResponse.json(
        { error: 'Unauthorized. Admin access required.' },
        { status: 401 }
      );
    }

    console.log(`[Analytics] Admin access granted: ${email} (${userId})`);

    // 2. Rate limiting (100 requests/hour per admin)
    const rateLimitResult = await checkRateLimit(userId!, profileRateLimit);
    if (!rateLimitResult.allowed) {
      console.warn(`[Analytics] Rate limit exceeded for admin ${email}`);
      return NextResponse.json(
        {
          error: 'Rate limit exceeded. Please try again later.',
          retryAfter: Math.ceil((rateLimitResult.reset.getTime() - Date.now()) / 1000),
        },
        {
          status: 429,
          headers: {
            'Retry-After': String(Math.ceil((rateLimitResult.reset.getTime() - Date.now()) / 1000)),
            'X-RateLimit-Limit': String(rateLimitResult.limit),
            'X-RateLimit-Remaining': String(rateLimitResult.remaining),
            'X-RateLimit-Reset': rateLimitResult.reset.toISOString(),
          },
        }
      );
    }

    // 3. Parse and validate query parameters
    const { searchParams } = new URL(req.url);
    const daysParam = searchParams.get('days');

    // Validate 'days' parameter
    let days = DEFAULT_DAYS;
    if (daysParam) {
      const parsedDays = parseInt(daysParam, 10);

      if (isNaN(parsedDays)) {
        return NextResponse.json(
          { error: 'Invalid "days" parameter. Must be a number.' },
          { status: 400 }
        );
      }

      if (parsedDays < MIN_DAYS || parsedDays > MAX_DAYS) {
        return NextResponse.json(
          { error: `Invalid "days" parameter. Must be between ${MIN_DAYS} and ${MAX_DAYS}.` },
          { status: 400 }
        );
      }

      days = parsedDays;
    }

    console.log(`[Analytics] Fetching analytics for last ${days} days`);

    // 4. Calculate date ranges (UTC for consistency)
    const now = new Date();
    const startDate = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
    const last7Days = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    const last30Days = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);

    // 5. Run analytics queries in parallel with timeout protection
    const analyticsPromise = Promise.all([
      // Total events in date range
      prisma.analyticsEvent.count({
        where: {
          createdAt: {
            gte: startDate,
          },
        },
      }),

      // Daily Active Users (last 7 days) - unique anonymousUserId
      prisma.analyticsEvent.groupBy({
        by: ['anonymousUserId'],
        where: {
          createdAt: {
            gte: last7Days,
          },
        },
        _count: {
          anonymousUserId: true,
        },
      }),

      // Monthly Active Users (last 30 days) - unique anonymousUserId
      prisma.analyticsEvent.groupBy({
        by: ['anonymousUserId'],
        where: {
          createdAt: {
            gte: last30Days,
          },
        },
        _count: {
          anonymousUserId: true,
        },
      }),

      // Events by type (APP_LAUNCHED, TRANSCRIPTION_COMPLETED, etc.)
      prisma.analyticsEvent.groupBy({
        by: ['eventType'],
        where: {
          createdAt: {
            gte: startDate,
          },
        },
        _count: {
          eventType: true,
        },
      }),

      // Tier distribution (FREE vs PRO)
      prisma.analyticsEvent.groupBy({
        by: ['tier'],
        where: {
          createdAt: {
            gte: startDate,
          },
        },
        _count: {
          tier: true,
        },
      }),

      // App version distribution (top 10)
      prisma.analyticsEvent.groupBy({
        by: ['appVersion'],
        where: {
          createdAt: {
            gte: startDate,
          },
          appVersion: {
            not: null,
          },
        },
        _count: {
          appVersion: true,
        },
        orderBy: {
          _count: {
            appVersion: 'desc',
          },
        },
        take: 10,
      }),

      // Model usage distribution (ggml-small.bin, ggml-tiny.bin, etc.)
      prisma.analyticsEvent.groupBy({
        by: ['modelUsed'],
        where: {
          createdAt: {
            gte: startDate,
          },
          modelUsed: {
            not: null,
          },
        },
        _count: {
          modelUsed: true,
        },
        orderBy: {
          _count: {
            modelUsed: 'desc',
          },
        },
      }),

      // OS distribution (Windows 11, Windows 10, etc.) - top 10
      prisma.analyticsEvent.groupBy({
        by: ['osVersion'],
        where: {
          createdAt: {
            gte: startDate,
          },
          osVersion: {
            not: null,
          },
        },
        _count: {
          osVersion: true,
        },
        orderBy: {
          _count: {
            osVersion: 'desc',
          },
        },
        take: 10,
      }),

      // Daily time series (for line chart)
      // Using parameterized query to prevent SQL injection
      prisma.$queryRaw<Array<{ date: string; count: bigint }>>`
        SELECT
          DATE("createdAt") as date,
          COUNT(*) as count
        FROM "AnalyticsEvent"
        WHERE "createdAt" >= ${startDate}
        GROUP BY DATE("createdAt")
        ORDER BY date ASC
      `,
    ]);

    // Add timeout protection
    const timeoutPromise = new Promise<never>((_, reject) =>
      setTimeout(() => reject(new Error('Query timeout')), QUERY_TIMEOUT_MS)
    );

    const [
      totalEvents,
      dailyActiveUsers,
      monthlyActiveUsers,
      eventsByType,
      tierDistribution,
      versionDistribution,
      modelDistribution,
      osDistribution,
      dailyTimeSeries,
    ] = await Promise.race([analyticsPromise, timeoutPromise]);

    // 6. Calculate derived metrics
    const dau = dailyActiveUsers.length;
    const mau = monthlyActiveUsers.length;
    const dauMauRatio = mau > 0 ? (dau / mau).toFixed(2) : '0.00';

    // 7. Format response (sanitize and structure data)
    const response = {
      overview: {
        totalEvents,
        dailyActiveUsers: dau,
        monthlyActiveUsers: mau,
        dau_mau_ratio: dauMauRatio,
      },
      events: {
        byType: eventsByType.reduce(
          (acc, e) => ({ ...acc, [e.eventType]: e._count.eventType }),
          {} as Record<string, number>
        ),
      },
      users: {
        tierDistribution: tierDistribution.reduce(
          (acc, t) => ({ ...acc, [t.tier]: t._count.tier }),
          {} as Record<string, number>
        ),
      },
      versions: {
        distribution: versionDistribution.map(v => ({
          version: v.appVersion,
          count: v._count.appVersion,
        })),
      },
      models: {
        distribution: modelDistribution.map(m => ({
          model: m.modelUsed,
          count: m._count.modelUsed,
        })),
      },
      os: {
        distribution: osDistribution.map(o => ({
          os: o.osVersion,
          count: o._count.osVersion,
        })),
      },
      timeSeries: {
        daily: dailyTimeSeries.map(d => ({
          date: d.date,
          count: Number(d.count),
        })),
      },
      dateRange: {
        start: startDate.toISOString(),
        end: now.toISOString(),
        days,
      },
      generatedAt: new Date().toISOString(),
      queryTimeMs: Date.now() - startTime,
    };

    console.log(`[Analytics] Query completed in ${Date.now() - startTime}ms`);

    return NextResponse.json(response, {
      headers: {
        'Cache-Control': 'private, max-age=300', // 5 minutes
        'X-Query-Time': String(Date.now() - startTime),
      },
    });

  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    const queryTime = Date.now() - startTime;

    console.error(`[Analytics] Error after ${queryTime}ms:`, error);

    // Different error responses based on error type
    if (errorMessage.includes('timeout')) {
      return NextResponse.json(
        {
          error: 'Query timeout. Please try a smaller date range.',
          suggestion: 'Reduce the "days" parameter (e.g., ?days=30)',
        },
        { status: 504 } // Gateway Timeout
      );
    }

    if (errorMessage.includes('database') || errorMessage.includes('Prisma')) {
      return NextResponse.json(
        {
          error: 'Database error. Please try again later.',
          retryAfter: 60, // Suggest retry after 60 seconds
        },
        { status: 503 } // Service Unavailable
      );
    }

    // Generic error response (don't expose internal details)
    return NextResponse.json(
      {
        error: 'Failed to fetch analytics. Please try again later.',
        errorId: `analytics_${Date.now()}`, // For support/debugging
      },
      { status: 500 }
    );
  }
}
