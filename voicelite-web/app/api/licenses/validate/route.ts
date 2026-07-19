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
import { recordLicenseActivation, recordLicenseEvent } from '@/lib/licensing';
import { logger } from '@/lib/logger';

// DEVICE-LIMIT BYPASS FIX: machineId stays optional in the schema because shipped desktop
// clients v1.0.x-v1.2.0.1 (pre-2025-10-29, before commit 65d53bc added the 3-device limit)
// call this endpoint with only { licenseKey } — hard-requiring machineId (400) would lock
// out legitimate paying users on those builds. Instead, requests without a machineId are
// recorded against a single reserved "no-device-id" activation slot per license, so they
// still count toward the 3-device cap instead of bypassing device accounting entirely.
const LEGACY_NO_MACHINE_ID = 'legacy-no-machine-id';

const bodySchema = z.object({
  // Accept both formats:
  // - New: VL-XXXXXX-XXXXXX-XXXXXX (nanoid, may include A-Z0-9_-)
  // - Legacy: UUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
  licenseKey: z.string()
    .regex(/^(VL-[A-Za-z0-9_-]{6}-[A-Za-z0-9_-]{6}-[A-Za-z0-9_-]{6}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i,
      "Invalid license key format"),

  // Base64 encoded machine ID (SHA256 hash, truncated to 32 chars)
  // Allows standard Base64 chars: A-Za-z0-9+/=
  machineId: z.string()
    .regex(/^[a-zA-Z0-9+/=\-_]{10,100}$/,
      "Invalid machine ID format")
    .optional(),

  // Sanitize HTML/script tags to prevent XSS in admin dashboard
  machineLabel: z.string()
    .max(255)
    .transform(s => s.replace(/<[^>]*>/g, '')) // Strip all HTML tags
    .optional(),

  // Base64 encoded SHA256 hash (exactly 43-44 chars for proper SHA256).
  // EMPTY-HASH FIX: the shipped C# client sends machineHash: "" when WMI fails
  // (HardwareIdService.GetMachineHash returns string.Empty on error). Rejecting
  // that with a 400 blocked those users from activating at all - treat empty
  // string as "not provided". (machineId/machineLabel have non-empty fallbacks
  // client-side, so only machineHash needs this.)
  machineHash: z.string()
    .regex(/^[a-zA-Z0-9+/=]{43,44}$/,
      "Invalid machine hash format")
    .optional()
    .or(z.literal('').transform(() => undefined)),
});

export async function POST(request: NextRequest) {
  try {
    // Parse request body first (for license key rate limiting)
    const body = await request.json();
    const { licenseKey, machineId, machineLabel, machineHash } = bodySchema.parse(body);

    // HIGH-1 FIX: Use Vercel's trusted IP detection (prevents X-Forwarded-For spoofing)
    const ip = ipAddress(request) || 'unknown';

    // LONG-TERM FIX: Only apply rate limiting when Upstash Redis is properly configured
    // In-memory fallback on serverless is unreliable (persists across warm starts, inconsistent across instances)
    // When Upstash isn't configured, rely on database-level protection (3-device activation limit)
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
          { error: 'Too many validation attempts. Please try again later.' },
          { status: 429 }
        );
      }
    } else {
      // Log warning that rate limiting is disabled (will appear in Vercel logs)
      logger.warn('Rate limiting disabled for license validation - Upstash not configured', { ip });
    }

    // Look up license in database
    // HIGH-7 FIX: Include email in the select to return to desktop client
    const license = await prisma.license.findUnique({
      where: { licenseKey },
      select: {
        id: true,
        email: true, // HIGH-7 FIX: Include email for desktop client
        status: true,
        type: true,
        expiresAt: true,
        activations: {
          where: { status: 'ACTIVE' },
        },
      },
    });

    // If license doesn't exist or is not active, return invalid
    if (!license || license.status !== 'ACTIVE') {
      return NextResponse.json({
        valid: false,
        tier: 'free',
      });
    }

    // Check if license has expired (subscription past due or ended)
    if (license.expiresAt && license.expiresAt < new Date()) {
      return NextResponse.json({
        valid: false,
        tier: 'free',
        reason: 'expired',
      });
    }

    // MED-6 FIX: Track activation count AFTER recording (not from initial query)
    // Previously returned stale count showing "2/3" when actually "3/3"
    let activationsUsed = license.activations.length;

    // DEVICE-LIMIT BYPASS FIX: Always record an activation (enforces 3-device limit).
    // Requests without a machineId (legacy clients <= v1.2.0.1) share one reserved slot
    // instead of skipping device accounting entirely.
    const effectiveMachineId = machineId ?? LEGACY_NO_MACHINE_ID;
    const effectiveMachineLabel = machineId ? machineLabel : 'Legacy client (no machine ID)';
    try {
      await recordLicenseActivation({
        licenseId: license.id,
        machineId: effectiveMachineId,
        machineLabel: effectiveMachineLabel,
        machineHash,
      });

      // MED-6 FIX: Query fresh activation count AFTER recording
      // This ensures the UI shows accurate "X/3 devices" count
      const freshCount = await prisma.licenseActivation.count({
        where: {
          licenseId: license.id,
          status: 'ACTIVE',
        },
      });
      activationsUsed = freshCount;
    } catch (error: any) {
      // Check if it's the activation limit error
      if (error.message && error.message.startsWith('ACTIVATION_LIMIT_REACHED')) {
        // MED-5 FIX: Audit log activation limit failures for abuse tracking
        // This allows monitoring for patterns like repeated limit hits from different IPs
        await recordLicenseEvent(license.id, 'activation_limit_reached', {
          machineId: effectiveMachineId,
          machineLabel: effectiveMachineLabel,
          ip,
          timestamp: new Date().toISOString(),
        }).catch(logError => {
          // Don't fail the request if audit logging fails
          logger.warn('Failed to record activation limit event', { error: logError });
        });

        logger.warn('Activation limit reached', {
          licenseId: license.id,
          machineId: effectiveMachineId,
          ip,
        });

        return NextResponse.json({
          valid: false,
          tier: 'free',
          // There is no account UI to deactivate devices from - point users at
          // support instead (same contact used on the site and in license emails).
          error: 'Maximum 3 device activations reached. Reply to your license email or contact basementhustlellc@gmail.com to free up a device.',
          activationsUsed: 3,
          maxActivations: 3,
        }, { status: 403 });
      }
      throw error; // Re-throw other errors
    }

    // License is valid!
    // HIGH-7 FIX: Include email in response for desktop client to display
    return NextResponse.json({
      valid: true,
      tier: 'pro',
      license: {
        type: license.type,
        email: license.email, // HIGH-7 FIX: Return email to desktop client
        expiresAt: license.expiresAt,
        activationsUsed, // MED-6 FIX: Use fresh count from after activation recording
        maxActivations: 3,
      },
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      logger.warn('License validation: Invalid request format', { issues: error.issues });
      return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
    }

    // Differentiate between database errors (retriable) and other errors
    const isPrismaError = error && typeof error === 'object' && 'code' in error;
    logger.error('License validation failed', error, { isPrismaError });

    // Return 503 for database errors (retriable), 500 for others
    const status = isPrismaError ? 503 : 500;
    return NextResponse.json(
      { error: 'Unable to validate license. Please try again.' },
      { status }
    );
  }
}
