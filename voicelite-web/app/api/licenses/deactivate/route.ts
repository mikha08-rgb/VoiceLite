import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { ipAddress } from '@vercel/edge';
import { prisma } from '@/lib/prisma';
import {
  checkRateLimit,
  licenseValidationIpRateLimit,
  licenseValidationKeyRateLimit,
  licenseValidationGlobalRateLimit,
  fallbackLicenseValidationIpLimit,
  fallbackLicenseValidationKeyLimit,
  fallbackLicenseValidationGlobalLimit,
  isUpstashConfigured
} from '@/lib/ratelimit';
import { deactivateLicenseActivation, recordLicenseEvent } from '@/lib/licensing';
import { logger } from '@/lib/logger';

// Input validation mirrors validate/route.ts (same key formats, same machineId regex).
// machineId is REQUIRED here - you must name the device you want to deactivate.
// The reserved 'legacy-no-machine-id' slot matches the regex, so the shared legacy
// slot can be freed through this endpoint too.
const bodySchema = z.object({
  licenseKey: z.string()
    .regex(/^(VL-[A-Za-z0-9_-]{6}-[A-Za-z0-9_-]{6}-[A-Za-z0-9_-]{6}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i,
      "Invalid license key format"),

  machineId: z.string()
    .regex(/^[a-zA-Z0-9+/=\-_]{10,100}$/,
      "Invalid machine ID format"),
});

// Enumeration-safe: the response is identical whether the license doesn't exist,
// isn't active, or the machineId was never activated - mirrors the generic
// responses used by validate and resend-email.
const GENERIC_RESPONSE = {
  success: true,
  message: 'If this device was activated on the license, it has been deactivated.',
};

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { licenseKey, machineId } = bodySchema.parse(body);

    // Use Vercel's trusted IP detection (prevents X-Forwarded-For spoofing)
    const ip = ipAddress(request) || 'unknown';

    // Same multi-layer rate limiting as validate/route.ts
    if (isUpstashConfigured) {
      const ipRateLimit = await checkRateLimit(ip, licenseValidationIpRateLimit, fallbackLicenseValidationIpLimit);
      const keyRateLimit = await checkRateLimit(licenseKey, licenseValidationKeyRateLimit, fallbackLicenseValidationKeyLimit);
      const globalRateLimit = await checkRateLimit('global', licenseValidationGlobalRateLimit, fallbackLicenseValidationGlobalLimit);

      if (!ipRateLimit.allowed || !keyRateLimit.allowed || !globalRateLimit.allowed) {
        const reason = !ipRateLimit.allowed ? 'IP limit exceeded' :
                       !keyRateLimit.allowed ? 'License key limit exceeded' :
                       'Global rate limit exceeded';
        logger.warn('Rate limit exceeded', { reason, ip });

        return NextResponse.json(
          { error: 'Too many requests. Please try again later.' },
          { status: 429 }
        );
      }
    } else {
      logger.warn('Rate limiting disabled for license deactivation - Upstash not configured', { ip });
    }

    const license = await prisma.license.findUnique({
      where: { licenseKey },
      select: { id: true },
    });

    if (!license) {
      return NextResponse.json(GENERIC_RESPONSE);
    }

    try {
      await deactivateLicenseActivation({
        licenseId: license.id,
        machineId,
      });
    } catch (error: any) {
      // P2025 = no activation record for this (licenseId, machineId) - treat the
      // same as success so responses don't reveal which machineIds exist.
      if (error?.code === 'P2025') {
        return NextResponse.json(GENERIC_RESPONSE);
      }
      throw error;
    }

    logger.info('Device deactivated', { licenseId: license.id, machineId });
    await recordLicenseEvent(license.id, 'device_deactivated', {
      machineId,
      ip,
      timestamp: new Date().toISOString(),
    }).catch(logError => {
      // Don't fail the request if audit logging fails
      logger.warn('Failed to record deactivation event', { error: logError });
    });

    return NextResponse.json(GENERIC_RESPONSE);
  } catch (error) {
    if (error instanceof z.ZodError) {
      logger.warn('License deactivation: Invalid request format', { issues: error.issues });
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }

    const isPrismaError = error && typeof error === 'object' && 'code' in error;
    logger.error('License deactivation failed', error, { isPrismaError });

    const status = isPrismaError ? 503 : 500;
    return NextResponse.json(
      { error: 'Unable to deactivate device. Please try again.' },
      { status }
    );
  }
}
