import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getLicenseByKey } from '@/lib/licensing';
import { LicenseStatus } from '@prisma/client';

/**
 * POST /api/licenses/validate
 *
 * Validates a license key and returns license status + features.
 * Used by desktop app to check if user has Pro features unlocked.
 * No authentication required - license key is the credential.
 *
 * Request body:
 * {
 *   "licenseKey": "VL-ABC123-DEF456-GHI789"
 * }
 *
 * Response:
 * {
 *   "valid": true,
 *   "status": "ACTIVE",
 *   "type": "SUBSCRIPTION" | "LIFETIME",
 *   "features": ["all_models"],
 *   "expiresAt": "2025-12-31T00:00:00.000Z" | null
 * }
 */

const bodySchema = z.object({
  licenseKey: z.string().min(10),
});

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { licenseKey } = bodySchema.parse(body);

    // Look up license
    const license = await getLicenseByKey(licenseKey);

    if (!license) {
      return NextResponse.json(
        {
          valid: false,
          error: 'License key not found',
          status: 'INVALID',
          features: []
        },
        { status: 404 }
      );
    }

    // Check if license is active
    const isActive = license.status === LicenseStatus.ACTIVE;

    // Check if expired (for subscriptions)
    const now = new Date();
    const isExpired = license.expiresAt && license.expiresAt < now;

    if (isExpired) {
      return NextResponse.json(
        {
          valid: false,
          status: 'EXPIRED',
          type: license.type,
          features: [],
          expiresAt: license.expiresAt?.toISOString() || null
        },
        { status: 200 }
      );
    }

    if (!isActive) {
      return NextResponse.json(
        {
          valid: false,
          status: license.status,
          type: license.type,
          features: [],
          expiresAt: license.expiresAt?.toISOString() || null
        },
        { status: 200 }
      );
    }

    // Parse features from JSON string
    let features: string[] = [];
    try {
      features = JSON.parse(license.features);
    } catch (e) {
      console.error('Failed to parse license features:', e);
      features = [];
    }

    // Return valid license with features
    return NextResponse.json(
      {
        valid: true,
        status: license.status,
        type: license.type,
        features,
        expiresAt: license.expiresAt?.toISOString() || null
      },
      { status: 200 }
    );

  } catch (error) {
    console.error('License validation error:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}