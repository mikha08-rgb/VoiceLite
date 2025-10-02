import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

// One-time setup endpoint to create AnalyticsEvent table
// TEMPORARY: No auth - DELETE THIS ENDPOINT AFTER USE!
export async function POST(req: NextRequest) {
  try {

    // Create ENUM types first
    await prisma.$executeRaw`
      DO $$ BEGIN
        CREATE TYPE "AnalyticsEventType" AS ENUM (
          'APP_LAUNCHED',
          'TRANSCRIPTION_COMPLETED',
          'MODEL_CHANGED',
          'SETTINGS_CHANGED',
          'ERROR_OCCURRED',
          'PRO_UPGRADE'
        );
      EXCEPTION
        WHEN duplicate_object THEN null;
      END $$;
    `;

    await prisma.$executeRaw`
      DO $$ BEGIN
        CREATE TYPE "TierType" AS ENUM ('FREE', 'PRO');
      EXCEPTION
        WHEN duplicate_object THEN null;
      END $$;
    `;

    // Create AnalyticsEvent table using raw SQL
    await prisma.$executeRaw`
      CREATE TABLE IF NOT EXISTS "AnalyticsEvent" (
        "id" TEXT NOT NULL,
        "anonymousUserId" TEXT NOT NULL,
        "eventType" "AnalyticsEventType" NOT NULL,
        "tier" "TierType" NOT NULL DEFAULT 'FREE',
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
