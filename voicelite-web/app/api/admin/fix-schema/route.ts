import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { isAdminAuthenticated } from '@/lib/admin-auth';

export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  // CRIT-2 FIX: Require admin authentication
  if (!isAdminAuthenticated(request)) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  try {
    console.log('🔧 Checking database schema...');

    // Check if userId column exists and is required
    const result = await prisma.$queryRaw<Array<{column_name: string, is_nullable: string}>>`
      SELECT column_name, is_nullable
      FROM information_schema.columns
      WHERE table_name = 'License'
      AND column_name = 'userId'
    `;

    if (result.length === 0) {
      return NextResponse.json({
        success: true,
        message: 'userId column does not exist - schema is correct',
        alreadyFixed: true,
      });
    }

    const isNullable = result[0].is_nullable;

    if (isNullable === 'YES') {
      return NextResponse.json({
        success: true,
        message: 'userId column is already nullable - schema is correct',
        alreadyFixed: true,
      });
    }

    console.log('🔧 Applying migration: Making userId nullable...');

    // Apply the fix
    await prisma.$executeRaw`ALTER TABLE "License" ALTER COLUMN "userId" DROP NOT NULL`;

    console.log('✅ Migration applied successfully');

    // Verify
    const verifyResult = await prisma.$queryRaw<Array<{is_nullable: string}>>`
      SELECT is_nullable
      FROM information_schema.columns
      WHERE table_name = 'License'
      AND column_name = 'userId'
    `;

    const fixed = verifyResult[0]?.is_nullable === 'YES';

    return NextResponse.json({
      success: true,
      message: fixed ? 'Schema fixed successfully!' : 'Migration applied but verification unclear',
      migrationApplied: true,
      verified: fixed,
    });

  } catch (error: any) {
    console.error('❌ Schema fix failed:', error);
    return NextResponse.json({
      success: false,
      error: error.message,
      // HIGH-1 FIX: Only expose stack trace in development (prevents info leak in production)
      details: process.env.NODE_ENV === 'development' ? error.stack : undefined,
    }, { status: 500 });
  }
}
