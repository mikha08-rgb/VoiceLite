import { NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

export async function GET() {
  try {
    // Try a simple query
    await prisma.$queryRaw`SELECT 1 as test`;

    return NextResponse.json({
      success: true,
      message: 'Database connection successful',
    });
  } catch (error: any) {
    return NextResponse.json({
      success: false,
      error: error.message,
      code: error.code,
      meta: error.meta,
    }, { status: 500 });
  }
}
