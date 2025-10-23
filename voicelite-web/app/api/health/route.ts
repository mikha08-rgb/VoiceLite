import { NextResponse } from 'next/server';

/**
 * Simple health check endpoint - no database, no dependencies
 */
export async function GET() {
  return NextResponse.json({
    status: 'ok',
    timestamp: new Date().toISOString(),
    env: process.env.NODE_ENV
  });
}
