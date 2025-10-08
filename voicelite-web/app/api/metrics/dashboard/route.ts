import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('timeRange') || '24h'; // 24h, 7d, 30d
    const version = searchParams.get('version'); // optional version filter

    // Calculate time range
    const now = new Date();
    const startTime = new Date();

    switch (timeRange) {
      case '1h':
        startTime.setHours(now.getHours() - 1);
        break;
      case '24h':
        startTime.setHours(now.getHours() - 24);
        break;
      case '7d':
        startTime.setDate(now.getDate() - 7);
        break;
      case '30d':
        startTime.setDate(now.getDate() - 30);
        break;
      default:
        startTime.setHours(now.getHours() - 24);
    }

    // Build where clause
    const whereClause: any = {
      timestamp: {
        gte: startTime,
      },
    };

    // Performance Metrics
    const appStartMetrics = await prisma.telemetryMetric.aggregate({
      where: {
        ...whereClause,
        metricType: 'app_start_time_ms',
      },
      _avg: { value: true },
      _max: { value: true },
      _min: { value: true },
      _count: true,
    });

    const hotkeyResponseMetrics = await prisma.telemetryMetric.aggregate({
      where: {
        ...whereClause,
        metricType: 'hotkey_response_time_ms',
      },
      _avg: { value: true },
      _max: { value: true },
      _count: true,
    });

    const transcriptionDurationMetrics = await prisma.telemetryMetric.aggregate({
      where: {
        ...whereClause,
        metricType: 'transcription_duration_ms',
      },
      _avg: { value: true },
      _max: { value: true },
      _min: { value: true },
      _count: true,
    });

    const memoryUsageMetrics = await prisma.telemetryMetric.aggregate({
      where: {
        ...whereClause,
        metricType: 'memory_usage_mb',
      },
      _avg: { value: true },
      _max: { value: true },
    });

    // Reliability Metrics
    const crashCount = await prisma.telemetryMetric.count({
      where: {
        ...whereClause,
        metricType: 'crash',
      },
    });

    const errorCount = await prisma.telemetryMetric.count({
      where: {
        ...whereClause,
        metricType: 'error',
      },
    });

    // Error breakdown by type
    const errorsByType = await prisma.telemetryMetric.groupBy({
      by: ['metadata'],
      where: {
        ...whereClause,
        metricType: 'error',
      },
      _count: true,
    });

    // Feature success rate
    const featureAttempts = await prisma.telemetryMetric.findMany({
      where: {
        ...whereClause,
        metricType: 'feature_attempt',
      },
      select: {
        value: true,
        metadata: true,
      },
    });

    const featureSuccessRate = featureAttempts.length > 0
      ? (featureAttempts.filter(f => f.value === 1).length / featureAttempts.length) * 100
      : 0;

    // Recovery attempts
    const recoveryAttempts = await prisma.telemetryMetric.findMany({
      where: {
        ...whereClause,
        metricType: 'recovery_attempt',
      },
      select: {
        value: true,
      },
    });

    const recoverySuccessRate = recoveryAttempts.length > 0
      ? (recoveryAttempts.filter(r => r.value === 1).length / recoveryAttempts.length) * 100
      : 0;

    // Usage Metrics
    const dailyActiveUsers = await prisma.telemetryMetric.groupBy({
      by: ['anonymousUserId'],
      where: {
        ...whereClause,
        metricType: 'daily_active_user',
      },
      _count: true,
    });

    const transcriptionsPerSession = await prisma.telemetryMetric.aggregate({
      where: {
        ...whereClause,
        metricType: 'transcriptions_per_session',
      },
      _avg: { value: true },
      _max: { value: true },
    });

    const sessionLengthMetrics = await prisma.telemetryMetric.aggregate({
      where: {
        ...whereClause,
        metricType: 'session_length_minutes',
      },
      _avg: { value: true },
      _max: { value: true },
    });

    // Feature usage breakdown
    const featureUsage = await prisma.telemetryMetric.groupBy({
      by: ['metadata'],
      where: {
        ...whereClause,
        metricType: 'feature_usage',
      },
      _count: true,
    });

    // Return aggregated dashboard data
    return NextResponse.json({
      timeRange,
      startTime: startTime.toISOString(),
      endTime: now.toISOString(),
      performance: {
        appStartTime: {
          avg: appStartMetrics._avg.value || 0,
          max: appStartMetrics._max.value || 0,
          min: appStartMetrics._min.value || 0,
          count: appStartMetrics._count,
        },
        hotkeyResponseTime: {
          avg: hotkeyResponseMetrics._avg.value || 0,
          max: hotkeyResponseMetrics._max.value || 0,
          count: hotkeyResponseMetrics._count,
        },
        transcriptionDuration: {
          avg: transcriptionDurationMetrics._avg.value || 0,
          max: transcriptionDurationMetrics._max.value || 0,
          min: transcriptionDurationMetrics._min.value || 0,
          count: transcriptionDurationMetrics._count,
        },
        memoryUsage: {
          avg: memoryUsageMetrics._avg.value || 0,
          max: memoryUsageMetrics._max.value || 0,
        },
      },
      reliability: {
        crashCount,
        errorCount,
        errorsByType: errorsByType.map((e) => ({
          type: e.metadata ? JSON.parse(e.metadata).errorType : 'unknown',
          count: e._count,
        })),
        featureSuccessRate: featureSuccessRate.toFixed(2),
        recoverySuccessRate: recoverySuccessRate.toFixed(2),
      },
      usage: {
        dailyActiveUsers: dailyActiveUsers.length,
        transcriptionsPerSession: {
          avg: transcriptionsPerSession._avg.value || 0,
          max: transcriptionsPerSession._max.value || 0,
        },
        sessionLength: {
          avg: sessionLengthMetrics._avg.value || 0,
          max: sessionLengthMetrics._max.value || 0,
        },
        featureUsage: featureUsage.map((f) => ({
          feature: f.metadata ? JSON.parse(f.metadata).featureName : 'unknown',
          count: f._count,
        })),
      },
    });
  } catch (error) {
    console.error('Dashboard metrics error:', error);
    return NextResponse.json(
      { error: 'Failed to fetch dashboard metrics' },
      { status: 500 }
    );
  }
}
