import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { ipAddress } from '@vercel/edge';
import { prisma } from '@/lib/prisma';
import { checkRateLimit,
  licenseValidationIpRateLimit,
  licenseValidationKeyRateLimit,
  licenseValidationGlobalRateLimit
} from '@/lib/ratelimit';
import { recordLicenseActivation } from '@/lib/licensing';
import { logger } from '@/lib/logger';

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

  // Base64 encoded SHA256 hash (exactly 43-44 chars for proper SHA256)
  machineHash: z.string()
    .regex(/^[a-zA-Z0-9+/=]{43,44}$/,
      "Invalid machine hash format")
    .optional(),
});

export async function POST(request: NextRequest) {
  try {
    // Parse request body first (for license key rate limiting)
    const body = await request.json();
    const { licenseKey, machineId, machineLabel, machineHash } = bodySchema.parse(body);

    // HIGH-1 FIX: Use Vercel's trusted IP detection (prevents X-Forwarded-For spoofing)
    const ip = ipAddress(request) || 'unknown';

    // Check all three rate limiters (IP, license key, global)
    const ipRateLimit = await checkRateLimit(ip, licenseValidationIpRateLimit);
    const keyRateLimit = await checkRateLimit(licenseKey, licenseValidationKeyRateLimit);
    const globalRateLimit = await checkRateLimit('global', licenseValidationGlobalRateLimit);

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

    // Look up license in database
    const license = await prisma.license.findUnique({
      where: { licenseKey },
      select: {
        id: true,
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

    // If machineId provided, record activation (enforces 3-device limit)
    if (machineId) {
      try {
        await recordLicenseActivation({
          licenseId: license.id,
          machineId,
          machineLabel,
          machineHash,
        });
      } catch (error: any) {
        // Check if it's the activation limit error
        if (error.message && error.message.startsWith('ACTIVATION_LIMIT_REACHED')) {
          return NextResponse.json({
            valid: false,
            tier: 'free',
            error: 'Maximum 3 device activations reached. Deactivate a device in your account to continue.',
            activationsUsed: 3,
            maxActivations: 3,
          }, { status: 403 });
        }
        throw error; // Re-throw other errors
      }
    }

    // License is valid!
    return NextResponse.json({
      valid: true,
      tier: 'pro',
      license: {
        type: license.type,
        expiresAt: license.expiresAt,
        activationsUsed: license.activations.length,
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
