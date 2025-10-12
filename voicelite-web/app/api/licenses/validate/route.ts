import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import { getLicenseByKey } from '@/lib/licensing';
import { LicenseStatus } from '@prisma/client';

/**
 * POST /api/licenses/validate
 *
 * Simple license validation endpoint for desktop app.
 * No authentication required - the license key itself is the auth.
 * Returns whether the license is valid and what features it unlocks.
 */

const bodySchema = z.object({
  licenseKey: z.string().min(10, 'License key must be at least 10 characters'),
});

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { licenseKey } = bodySchema.parse(body);

    // Lookup license in database
    const license = await getLicenseByKey(licenseKey);

    if (!license) {
      return NextResponse.json(
        {
          valid: false,
          error: 'License key not found',
        },
        { status: 404 }
      );
    }

    // Check if license is active
    const isActive = license.status === LicenseStatus.ACTIVE;

    // Check if expired (for subscription types - though we only use LIFETIME now)
    let isExpired = false;
    if (license.expiresAt) {
      isExpired = new Date() > license.expiresAt;
    }

    const valid = isActive && !isExpired;

    return NextResponse.json({
      valid,
      status: license.status,
      type: license.type,
      expiresAt: license.expiresAt?.toISOString() ?? null,
      email: license.user.email,
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { valid: false, error: 'Invalid request', details: error.errors },
        { status: 400 }
      );
    }

    console.error('License validation error:', error);
    return NextResponse.json(
      { valid: false, error: 'Internal server error' },
      { status: 500 }
    );
  }
}