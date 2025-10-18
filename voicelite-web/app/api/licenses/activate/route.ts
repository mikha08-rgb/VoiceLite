import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { prisma } from '@/lib/prisma';
import { LicenseStatus } from '@prisma/client';
import { recordLicenseActivation } from '@/lib/licensing';
import { activationRateLimit, checkRateLimit, getClientIp } from '@/lib/ratelimit';

/**
 * POST /api/licenses/activate
 *
 * Activate a license on a new device.
 * Enforces device limits to prevent piracy.
 * No authentication required - the license key itself is the auth.
 */

const bodySchema = z.object({
  licenseKey: z.string().min(10, 'License key is required'),
  machineId: z.string().min(10, 'Machine ID is required'),
  machineLabel: z.string().optional(),
});

// License key format validation
const LICENSE_KEY_REGEX = /^VL-[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{6}$/;

export async function POST(request: NextRequest) {
  // Rate limiting: 10 requests per hour per IP
  const clientIp = getClientIp(request);
  const rateLimitResult = await checkRateLimit(clientIp, activationRateLimit);

  if (!rateLimitResult.allowed) {
    return NextResponse.json(
      {
        success: false,
        error: 'Too many activation attempts',
        message: 'Please wait before trying again.',
        retryAfter: rateLimitResult.reset.toISOString(),
      },
      {
        status: 429,
        headers: {
          'X-RateLimit-Limit': rateLimitResult.limit.toString(),
          'X-RateLimit-Remaining': rateLimitResult.remaining.toString(),
          'X-RateLimit-Reset': rateLimitResult.reset.getTime().toString(),
          'Retry-After': Math.ceil((rateLimitResult.reset.getTime() - Date.now()) / 1000).toString(),
        },
      }
    );
  }

  try {
    const body = await request.json();
    const { licenseKey, machineId, machineLabel } = bodySchema.parse(body);

    // Validate license key format
    if (!LICENSE_KEY_REGEX.test(licenseKey)) {
      return NextResponse.json(
        {
          success: false,
          error: 'Invalid license key format',
        },
        { status: 400 }
      );
    }

    // Find license
    const license = await prisma.license.findUnique({
      where: { licenseKey },
      include: { activations: true },
    });

    if (!license) {
      return NextResponse.json(
        {
          success: false,
          error: 'License key not found',
        },
        { status: 404 }
      );
    }

    // Check if license is active
    if (license.status !== LicenseStatus.ACTIVE) {
      return NextResponse.json(
        {
          success: false,
          error: 'This license has been canceled or expired',
        },
        { status: 403 }
      );
    }

    // Check if this machine is already activated
    const existingActivation = license.activations.find(
      (a) => a.machineId === machineId
    );

    if (existingActivation) {
      // Already activated, update lastValidatedAt
      await recordLicenseActivation({
        licenseId: license.id,
        machineId,
        machineLabel,
      });

      return NextResponse.json({
        success: true,
        license: {
          type: license.type,
          email: license.email,
        },
        activatedDevices: license.activations.length,
        maxDevices: license.maxDevices,
        message: 'License already activated on this device',
      });
    }

    // Check device limit
    if (license.activations.length >= license.maxDevices) {
      return NextResponse.json(
        {
          success: false,
          error: `This license is already activated on ${license.maxDevices} devices (maximum allowed). Please deactivate it on another device first, or contact support@voicelite.app for help.`,
          activatedDevices: license.activations.length,
          maxDevices: license.maxDevices,
        },
        { status: 403 }
      );
    }

    // Create new activation
    await recordLicenseActivation({
      licenseId: license.id,
      machineId,
      machineLabel,
    });

    console.log(
      `License ${licenseKey} activated on device ${machineLabel || machineId} (${
        license.activations.length + 1
      }/${license.maxDevices})`
    );

    return NextResponse.json({
      success: true,
      license: {
        type: license.type,
        email: license.email,
      },
      activatedDevices: license.activations.length + 1,
      maxDevices: license.maxDevices,
      message: 'License activated successfully',
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        {
          success: false,
          error: 'Invalid request',
          details: error.issues,
        },
        { status: 400 }
      );
    }

    console.error('License activation error:', error);
    return NextResponse.json(
      {
        success: false,
        error: 'Internal server error',
      },
      { status: 500 }
    );
  }
}
