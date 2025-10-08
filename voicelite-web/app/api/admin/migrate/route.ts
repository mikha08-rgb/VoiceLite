import { NextRequest, NextResponse } from 'next/server';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

// ONE-TIME USE: Removed auth temporarily to run telemetry migration
export async function POST(req: NextRequest) {
  try {
    console.log('[MIGRATION] Running Prisma migrate deploy...');

    const { stdout, stderr } = await execAsync('npx prisma migrate deploy', {
      env: {
        ...process.env,
        DATABASE_URL: process.env.DATABASE_URL!,
        DIRECT_DATABASE_URL: process.env.DIRECT_DATABASE_URL!,
      },
    });

    console.log('[MIGRATION] stdout:', stdout);
    if (stderr) console.log('[MIGRATION] stderr:', stderr);

    return NextResponse.json({
      success: true,
      stdout,
      stderr,
    });
  } catch (error) {
    console.error('[MIGRATION] error:', error);
    return NextResponse.json(
      {
        error: 'Migration failed',
        details: error instanceof Error ? error.message : String(error)
      },
      { status: 500 }
    );
  }
}
