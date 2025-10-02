import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

// One-time setup endpoint to create AnalyticsEvent table
export async function POST(req: NextRequest) {
  try {
    const { secret } = await req.json();

    // Simple secret check
    if (secret !== process.env.MIGRATION_SECRET) {
      return NextResponse.json(
        { error: 'Unauthorized' },
        { status: 401 }
      );
    }

    // Create AnalyticsEvent table using raw SQL
    await prisma.$executeRaw`
      CREATE TABLE IF NOT EXISTS "AnalyticsEvent" (
        "id" TEXT NOT NULL,
        "anonymousUserId" TEXT NOT NULL,
        "eventType" TEXT NOT NULL,
        "tier" TEXT NOT NULL DEFAULT 'FREE',
        "appVersion" TEXT,
        "osVersion" TEXT,
        "modelUsed" TEXT,
        "metadata" TEXT,
        "ipAddress" TEXT,
        "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

        CONSTRAINT "AnalyticsEvent_pkey" PRIMARY KEY ("id")
      );
    `;

    // Create indexes
    await prisma.$executeRaw`
      CREATE INDEX IF NOT EXISTS "AnalyticsEvent_anonymousUserId_idx" ON "AnalyticsEvent"("anonymousUserId");
    `;

    await prisma.$executeRaw`
      CREATE INDEX IF NOT EXISTS "AnalyticsEvent_eventType_idx" ON "AnalyticsEvent"("eventType");
    `;

    await prisma.$executeRaw`
      CREATE INDEX IF NOT EXISTS "AnalyticsEvent_createdAt_idx" ON "AnalyticsEvent"("createdAt");
    `;

    await prisma.$executeRaw`
      CREATE INDEX IF NOT EXISTS "AnalyticsEvent_tier_idx" ON "AnalyticsEvent"("tier");
    `;

    await prisma.$executeRaw`
      CREATE INDEX IF NOT EXISTS "AnalyticsEvent_appVersion_idx" ON "AnalyticsEvent"("appVersion");
    `;

    return NextResponse.json({
      success: true,
      message: 'AnalyticsEvent table created successfully!',
    });
  } catch (error) {
    console.error('Table creation error:', error);
    return NextResponse.json(
      {
        error: 'Table creation failed',
        details: error instanceof Error ? error.message : String(error)
      },
      { status: 500 }
    );
  }
}
