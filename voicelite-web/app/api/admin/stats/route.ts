import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

// Admin authentication helper (reuse from feedback route)
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
    return NextResponse.json({
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
    });
  } catch (error) {
    console.error('Admin stats error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch stats' },
      { status: 500 }
    );
  }
}
