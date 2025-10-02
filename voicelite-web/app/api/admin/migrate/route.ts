import { NextRequest, NextResponse } from 'next/server';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

// One-time migration endpoint - DELETE after running!
export async function POST(req: NextRequest) {
  try {
    const { secret } = await req.json();

    // Simple secret check (you'll need to provide this)
    if (secret !== process.env.MIGRATION_SECRET) {
      return NextResponse.json(
        { error: 'Unauthorized' },
        { status: 401 }
      );
    }

    // Run migration
    const { stdout, stderr } = await execAsync('npx prisma migrate deploy');

    return NextResponse.json({
      success: true,
      stdout,
      stderr,
      message: 'Migration completed successfully. DELETE THIS ENDPOINT NOW!',
    });
  } catch (error) {
    console.error('Migration error:', error);
    return NextResponse.json(
      {
        error: 'Migration failed',
        details: error instanceof Error ? error.message : String(error)
      },
      { status: 500 }
    );
  }
}
