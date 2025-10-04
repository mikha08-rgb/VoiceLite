import { NextRequest } from 'next/server';
import { prisma } from '@/lib/prisma';

/**
 * Admin Authentication Utility
 *
 * Centralized admin verification for all admin endpoints.
 * Security features:
 * - Session-based authentication (JWT in HTTP-only cookie)
 * - Email whitelist via ADMIN_EMAILS environment variable
 * - Session expiry and revocation checks
 * - Fail-safe defaults (deny access on errors)
 */

export interface AdminVerificationResult {
  isAdmin: boolean;
  userId?: string;
  email?: string;
  error?: string;
}

/**
 * Verify if the request is from an authenticated admin user.
 *
 * @param req - Next.js request object
 * @returns Admin verification result with user info or error
 *
 * @example
 * ```ts
 * const { isAdmin, userId, email } = await verifyAdmin(req);
 * if (!isAdmin) {
 *   return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
 * }
 * ```
 */
export async function verifyAdmin(req: NextRequest): Promise<AdminVerificationResult> {
  try {
    // 1. Check session cookie exists
    const sessionCookie = req.cookies.get('session');
    if (!sessionCookie?.value) {
      return {
        isAdmin: false,
        error: 'No session cookie found'
      };
    }

    // 2. Validate session token (prevent injection)
    const sessionHash = sessionCookie.value;
    if (!sessionHash || typeof sessionHash !== 'string' || sessionHash.length > 100) {
      return {
        isAdmin: false,
        error: 'Invalid session token format'
      };
    }

    // 3. Query session from database
    const session = await prisma.session.findUnique({
      where: { sessionHash },
      select: {
        userId: true,
        expiresAt: true,
        revokedAt: true,
        user: {
          select: {
            id: true,
            email: true,
          },
        },
      },
    });

    // 4. Check session exists
    if (!session) {
      return {
        isAdmin: false,
        error: 'Session not found'
      };
    }

    // 5. Check session not expired
    const now = new Date();
    if (session.expiresAt < now) {
      return {
        isAdmin: false,
        error: 'Session expired'
      };
    }

    // 6. Check session not revoked
    if (session.revokedAt) {
      return {
        isAdmin: false,
        error: 'Session revoked'
      };
    }

    // 7. Check ADMIN_EMAILS environment variable
    const adminEmailsEnv = process.env.ADMIN_EMAILS;
    if (!adminEmailsEnv) {
      console.error('ADMIN_EMAILS environment variable not set');
      return {
        isAdmin: false,
        error: 'Admin configuration missing'
      };
    }

    // 8. Parse admin emails (trim whitespace, filter empty)
    const adminEmails = adminEmailsEnv
      .split(',')
      .map(e => e.trim().toLowerCase())
      .filter(e => e.length > 0);

    if (adminEmails.length === 0) {
      console.error('ADMIN_EMAILS is empty');
      return {
        isAdmin: false,
        error: 'No admins configured'
      };
    }

    // 9. Check if user's email is in admin list (case-insensitive)
    const userEmail = session.user.email.toLowerCase();
    const isAdmin = adminEmails.includes(userEmail);

    if (!isAdmin) {
      return {
        isAdmin: false,
        error: `Email ${session.user.email} is not an admin`
      };
    }

    // 10. Success - user is authenticated admin
    return {
      isAdmin: true,
      userId: session.userId,
      email: session.user.email,
    };

  } catch (error) {
    // Fail-safe: deny access on any error
    console.error('Admin verification error:', error);
    return {
      isAdmin: false,
      error: error instanceof Error ? error.message : 'Authentication failed'
    };
  }
}

/**
 * Get list of admin emails from environment variable.
 *
 * @returns Array of admin email addresses (lowercased, trimmed)
 */
export function getAdminEmails(): string[] {
  const adminEmailsEnv = process.env.ADMIN_EMAILS || '';
  return adminEmailsEnv
    .split(',')
    .map(e => e.trim().toLowerCase())
    .filter(e => e.length > 0);
}

/**
 * Check if an email is an admin email.
 *
 * @param email - Email address to check
 * @returns True if email is in ADMIN_EMAILS list
 */
export function isAdminEmail(email: string): boolean {
  const adminEmails = getAdminEmails();
  return adminEmails.includes(email.toLowerCase());
}
