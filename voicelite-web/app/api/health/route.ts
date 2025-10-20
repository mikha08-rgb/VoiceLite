import { NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

/**
 * Health Check Endpoint
 *
 * Used by UptimeRobot and other monitoring services to verify:
 * - Web server is responding
 * - Database connection is healthy
 * - Application is operational
 *
 * Returns:
 * - 200 OK: System healthy
 * - 503 Service Unavailable: Database connection failed
 */
export async function GET() {
  const startTime = Date.now();

  try {
    // Quick database ping with 5-second timeout
    await prisma.$queryRaw`SELECT 1 AS health_check`;

    const responseTime = Date.now() - startTime;

    return NextResponse.json({
      status: 'ok',
      timestamp: new Date().toISOString(),
      version: '1.0.69',
      services: {
        database: 'connected',
        responseTimeMs: responseTime
      }
    }, {
      status: 200,
      headers: {
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        'Pragma': 'no-cache',
        'Expires': '0'
      }
    });
  } catch (error) {
    console.error('Health check failed:', error);

    return NextResponse.json({
      status: 'error',
      timestamp: new Date().toISOString(),
      version: '1.0.69',
      services: {
        database: 'disconnected',
        error: error instanceof Error ? error.message : 'Unknown database error'
      }
    }, {
      status: 503,
      headers: {
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        'Pragma': 'no-cache',
        'Expires': '0'
      }
    });
  }
}

// Disable Next.js caching for health checks
export const dynamic = 'force-dynamic';
export const revalidate = 0;
