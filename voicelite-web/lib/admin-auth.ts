import { NextRequest } from 'next/server';
import crypto from 'crypto';

/**
 * Admin Authentication Utility
 *
 * Validates admin requests using a secret token from environment variables.
 * Uses constant-time comparison to prevent timing attacks.
 */

export function isAdminAuthenticated(request: NextRequest): boolean {
  const adminToken = request.headers.get('x-admin-token');
  const adminSecret = process.env.ADMIN_SECRET_TOKEN;

  // No token provided or secret not configured
  if (!adminToken || !adminSecret) {
    return false;
  }

  // Use constant-time comparison to prevent timing attacks
  try {
    const tokenBuffer = Buffer.from(adminToken);
    const secretBuffer = Buffer.from(adminSecret);

    // Buffers must be same length for timingSafeEqual
    if (tokenBuffer.length !== secretBuffer.length) {
      return false;
    }

    return crypto.timingSafeEqual(tokenBuffer, secretBuffer);
  } catch {
    return false;
  }
}

/**
 * Get unauthorized response for admin endpoints
 */
export function unauthorizedResponse() {
  return new Response(
    JSON.stringify({ error: 'Unauthorized' }),
    {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    }
  );
}
