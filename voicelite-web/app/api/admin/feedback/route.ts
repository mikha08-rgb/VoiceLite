import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { z } from 'zod';

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

    // Check if user is admin (update this with your admin email list)
    const adminEmails = (process.env.ADMIN_EMAILS || '').split(',').map(e => e.trim());
    const isAdmin = adminEmails.includes(session.user.email);

    return { isAdmin, userId: session.userId };
  } catch (error) {
    console.error('Admin verification error:', error);
    return { isAdmin: false };
  }
}

const querySchema = z.object({
  status: z.enum(['OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED', 'ALL']).optional().default('ALL'),
  type: z.enum(['BUG', 'FEATURE_REQUEST', 'GENERAL', 'QUESTION', 'ALL']).optional().default('ALL'),
  page: z.string().optional().default('1'),
  limit: z.string().optional().default('20'),
});

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

    // Parse query parameters
    const searchParams = req.nextUrl.searchParams;
    const params = querySchema.parse({
      status: searchParams.get('status') || 'ALL',
      type: searchParams.get('type') || 'ALL',
      page: searchParams.get('page') || '1',
      limit: searchParams.get('limit') || '20',
    });

    const page = parseInt(params.page);
    const limit = Math.min(parseInt(params.limit), 100); // Max 100 items per page
    const skip = (page - 1) * limit;

    // Build where clause
    const where: any = {};
    if (params.status !== 'ALL') {
      where.status = params.status;
    }
    if (params.type !== 'ALL') {
      where.type = params.type;
    }

    // Fetch feedback with pagination
    const [feedback, totalCount] = await Promise.all([
      prisma.feedback.findMany({
        where,
        include: {
          user: {
            select: {
              id: true,
              email: true,
              createdAt: true,
            },
          },
        },
        orderBy: [
          { priority: 'desc' },
          { createdAt: 'desc' },
        ],
        skip,
        take: limit,
      }),
      prisma.feedback.count({ where }),
    ]);

    // Get status counts for all feedback
    const statusCounts = await prisma.feedback.groupBy({
      by: ['status'],
      _count: {
        status: true,
      },
    });

    const typeCounts = await prisma.feedback.groupBy({
      by: ['type'],
      _count: {
        type: true,
      },
    });

    return NextResponse.json({
      feedback: feedback.map(f => ({
        ...f,
        metadata: f.metadata ? JSON.parse(f.metadata) : null,
      })),
      pagination: {
        page,
        limit,
        totalCount,
        totalPages: Math.ceil(totalCount / limit),
      },
      stats: {
        byStatus: statusCounts.reduce((acc, s) => ({ ...acc, [s.status]: s._count.status }), {}),
        byType: typeCounts.reduce((acc, t) => ({ ...acc, [t.type]: t._count.type }), {}),
      },
    });
  } catch (error) {
    console.error('Admin feedback list error:', error);

    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: 'Invalid query parameters', details: error.errors },
        { status: 400 }
      );
    }

    return NextResponse.json(
      { error: 'Failed to fetch feedback' },
      { status: 500 }
    );
  }
}

// Update feedback status (PATCH)
export async function PATCH(req: NextRequest) {
  try {
    // Verify admin access
    const { isAdmin } = await verifyAdmin(req);

    if (!isAdmin) {
      return NextResponse.json(
        { error: 'Unauthorized. Admin access required.' },
        { status: 401 }
      );
    }

    const body = await req.json();
    const { feedbackId, status, priority } = z.object({
      feedbackId: z.string(),
      status: z.enum(['OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED']).optional(),
      priority: z.enum(['LOW', 'MEDIUM', 'HIGH', 'CRITICAL']).optional(),
    }).parse(body);

    const updateData: any = {};
    if (status) updateData.status = status;
    if (priority) updateData.priority = priority;

    const updatedFeedback = await prisma.feedback.update({
      where: { id: feedbackId },
      data: updateData,
    });

    return NextResponse.json({
      success: true,
      feedback: updatedFeedback,
    });
  } catch (error) {
    console.error('Update feedback error:', error);

    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: 'Invalid request data', details: error.errors },
        { status: 400 }
      );
    }

    return NextResponse.json(
      { error: 'Failed to update feedback' },
      { status: 500 }
    );
  }
}
