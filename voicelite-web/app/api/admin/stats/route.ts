import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { verifyAdmin } from '@/lib/admin-auth';
import { checkRateLimit, profileRateLimit } from '@/lib/ratelimit';

/**
 * Admin Stats API Endpoint
 *
 * GET /api/admin/stats
 *
 * Returns aggregated stats for licenses, users, purchases, and feedback.
 * Requires admin authentication via session cookie.
 */

// Force dynamic rendering (uses cookies for auth)
export const dynamic = 'force-dynamic';

// Cache stats for 5 minutes
export const revalidate = 300;

export async function GET(req: NextRequest) {
  const startTime = Date.now();

  try {
    // 1. Verify admin access
    const { isAdmin, userId, email, error: authError } = await verifyAdmin(req);

    if (!isAdmin) {
      console.warn(`[Stats] Unauthorized access attempt: ${authError}`);
      return NextResponse.json(
        { error: 'Unauthorized. Admin access required.' },
        { status: 401 }
      );
    }

    console.log(`[Stats] Admin access granted: ${email} (${userId})`);

    // 2. Rate limiting (100 requests/hour per admin)
    const rateLimitResult = await checkRateLimit(userId!, profileRateLimit);
    if (!rateLimitResult.allowed) {
      console.warn(`[Stats] Rate limit exceeded for admin ${email}`);
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

    // Calculate date ranges
    const now = new Date();
    const last7Days = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    const last30Days = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);

    // Run all queries in parallel for performance
    const [
      totalUsers,
      newUsers7d,
      newUsers30d,
      totalLicenses,
      activeLicenses,
      licensesByType,
      totalPurchases,
      recentActivity,
      feedbackCounts,
      activeUsers30d,
    ] = await Promise.all([
      // Total users
      prisma.user.count(),

      // New users (last 7 days)
      prisma.user.count({
        where: {
          createdAt: {
            gte: last7Days,
          },
        },
      }),

      // New users (last 30 days)
      prisma.user.count({
        where: {
          createdAt: {
            gte: last30Days,
          },
        },
      }),

      // Total licenses
      prisma.license.count(),

      // Active licenses
      prisma.license.count({
        where: {
          status: 'ACTIVE',
        },
      }),

      // Licenses by type
      prisma.license.groupBy({
        by: ['type'],
        _count: {
          type: true,
        },
      }),

      // Total purchases
      prisma.purchase.count(),

      // Recent activity (last 50 events)
      prisma.userActivity.findMany({
        take: 50,
        orderBy: {
          createdAt: 'desc',
        },
        include: {
          user: {
            select: {
              email: true,
            },
          },
        },
      }),

      // Feedback counts by status
      prisma.feedback.groupBy({
        by: ['status'],
        _count: {
          status: true,
        },
      }),

      // Active users (had activity in last 30 days)
      prisma.userActivity.groupBy({
        by: ['userId'],
        where: {
          createdAt: {
            gte: last30Days,
          },
        },
        _count: {
          userId: true,
        },
      }),
    ]);

    // Calculate license activation metrics
    const [totalActivations, activeDevices] = await Promise.all([
      prisma.licenseActivation.count(),
      prisma.licenseActivation.count({
        where: {
          status: 'ACTIVE',
        },
      }),
    ]);

    // User growth (last 30 days, grouped by day)
    const userGrowth = await prisma.$queryRaw<Array<{ date: string; count: bigint }>>`
      SELECT
        DATE(created_at) as date,
        COUNT(*) as count
      FROM "User"
      WHERE created_at >= ${last30Days}
      GROUP BY DATE(created_at)
      ORDER BY date ASC
    `;

    // Activity breakdown (last 30 days)
    const activityBreakdown = await prisma.userActivity.groupBy({
      by: ['activityType'],
      where: {
        createdAt: {
          gte: last30Days,
        },
      },
      _count: {
        activityType: true,
      },
    });

    // Format response
    const response = {
      users: {
        total: totalUsers,
        new7d: newUsers7d,
        new30d: newUsers30d,
        active30d: activeUsers30d.length,
        growth: userGrowth.map(g => ({
          date: g.date,
          count: Number(g.count),
        })),
      },
      licenses: {
        total: totalLicenses,
        active: activeLicenses,
        byType: licensesByType.reduce(
          (acc, l) => ({ ...acc, [l.type]: l._count.type }),
          {} as Record<string, number>
        ),
        activations: {
          total: totalActivations,
          active: activeDevices,
        },
      },
      purchases: {
        total: totalPurchases,
      },
      feedback: {
        byStatus: feedbackCounts.reduce(
          (acc, f) => ({ ...acc, [f.status]: f._count.status }),
          {} as Record<string, number>
        ),
        total: feedbackCounts.reduce((sum, f) => sum + f._count.status, 0),
      },
      activity: {
        recent: recentActivity.map(a => ({
          ...a,
          metadata: a.metadata ? JSON.parse(a.metadata) : null,
        })),
        breakdown: activityBreakdown.reduce(
          (acc, a) => ({ ...acc, [a.activityType]: a._count.activityType }),
          {} as Record<string, number>
        ),
      },
      generatedAt: new Date().toISOString(),
      queryTimeMs: Date.now() - startTime,
    };

    console.log(`[Stats] Query completed in ${Date.now() - startTime}ms`);

    return NextResponse.json(response, {
      headers: {
        'Cache-Control': 'private, max-age=300', // 5 minutes
        'X-Query-Time': String(Date.now() - startTime),
      },
    });
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    const queryTime = Date.now() - startTime;

    console.error(`[Stats] Error after ${queryTime}ms:`, error);

    // Different error responses based on error type
    if (errorMessage.includes('database') || errorMessage.includes('Prisma')) {
      return NextResponse.json(
        {
          error: 'Database error. Please try again later.',
          retryAfter: 60,
        },
        { status: 503 } // Service Unavailable
      );
    }

    // Generic error response (don't expose internal details)
    return NextResponse.json(
      {
        error: 'Failed to fetch stats. Please try again later.',
        errorId: `stats_${Date.now()}`,
      },
      { status: 500 }
    );
  }
}
