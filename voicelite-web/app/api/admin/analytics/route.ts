import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

// Admin authentication helper
async function verifyAdmin(req: NextRequest): Promise<{ isAdmin: boolean; userId?: string }> {
  const sessionCookie = req.cookies.get('session');

  if (!sessionCookie) {
    return { isAdmin: false };
  }

  try {
    const session = await prisma.session.findUnique({
      where: { sessionHash: sessionCookie.value },
      select: { userId: true, expiresAt: true, revokedAt: true, user: { select: { email: true } } },
    });

    if (!session || session.expiresAt < new Date() || session.revokedAt) {
      return { isAdmin: false };
    }

    // Check if user is admin
    const adminEmails = (process.env.ADMIN_EMAILS || '').split(',').map(e => e.trim());
    const isAdmin = adminEmails.includes(session.user.email);

    return { isAdmin, userId: session.userId };
  } catch (error) {
    console.error('Admin verification error:', error);
    return { isAdmin: false };
  }
}

export async function GET(req: NextRequest) {
  try {
    // Verify admin access
    const { isAdmin } = await verifyAdmin(req);

    if (!isAdmin) {
      return NextResponse.json(
        { error: 'Unauthorized. Admin access required.' },
        { status: 401 }
      );
    }

    // Parse query parameters for date range
    const { searchParams } = new URL(req.url);
    const daysParam = searchParams.get('days') || '30';
    const days = Math.min(Math.max(parseInt(daysParam, 10) || 30, 1), 365);

    // Calculate date ranges
    const now = new Date();
    const startDate = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
    const last7Days = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

    // Run analytics queries in parallel
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
    ] = await Promise.all([
      // Total events
      prisma.analyticsEvent.count({
        where: {
          createdAt: {
            gte: startDate,
          },
        },
      }),

      // Daily Active Users (last 7 days)
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

      // Monthly Active Users (last 30 days)
      prisma.analyticsEvent.groupBy({
        by: ['anonymousUserId'],
        where: {
          createdAt: {
            gte: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000),
          },
        },
        _count: {
          anonymousUserId: true,
        },
      }),

      // Events by type
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

      // Tier distribution (Free vs Pro)
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

      // Version distribution
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

      // Model usage distribution
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

      // OS distribution
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

      // Daily time series (for charts)
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

    // Calculate unique users for DAU/MAU
    const dau = dailyActiveUsers.length;
    const mau = monthlyActiveUsers.length;

    // Format response
    return NextResponse.json({
      overview: {
        totalEvents,
        dailyActiveUsers: dau,
        monthlyActiveUsers: mau,
        dau_mau_ratio: mau > 0 ? (dau / mau).toFixed(2) : '0.00',
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
      generatedAt: new Date().toISOString(),
      dateRange: {
        start: startDate.toISOString(),
        end: now.toISOString(),
        days,
      },
    });
  } catch (error) {
    console.error('Admin analytics error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch analytics' },
      { status: 500 }
    );
  }
}
