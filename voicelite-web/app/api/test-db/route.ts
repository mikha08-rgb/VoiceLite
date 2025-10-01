import { NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

export async function GET() {
  try {
    console.log('=== DATABASE TEST ===');
    console.log('DATABASE_URL exists:', !!process.env.DATABASE_URL);

    // Try to query the database
    const userCount = await prisma.user.count();
    console.log('User count:', userCount);

    // Try to create a test user
    const testUser = await prisma.user.upsert({
      where: { email: 'test-db-connection@example.com' },
      create: { email: 'test-db-connection@example.com' },
      update: {},
    });
    console.log('Test user created/updated:', testUser.id);

    return NextResponse.json({
      success: true,
      userCount,
      testUserId: testUser.id
    });
  } catch (error) {
    console.error('Database test failed:', error);
    return NextResponse.json({
      success: false,
      error: error instanceof Error ? error.message : 'Unknown error',
      stack: error instanceof Error ? error.stack : undefined
    }, { status: 500 });
  }
}
