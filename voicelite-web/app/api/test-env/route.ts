import { NextResponse } from 'next/server';

export async function GET() {
  const hasDbUrl = !!process.env.DATABASE_URL;
  const hasDirectDbUrl = !!process.env.DIRECT_DATABASE_URL;
  const dbUrlPrefix = process.env.DATABASE_URL?.substring(0, 30) || 'not set';

  return NextResponse.json({
    hasDbUrl,
    hasDirectDbUrl,
    dbUrlPrefix,
    nodeEnv: process.env.NODE_ENV,
  });
}
