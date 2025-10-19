import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';
import { NextRequest } from 'next/server';

/**
 * Rate limiting configuration for VoiceLite API endpoints.
 * Uses Upstash Redis for distributed rate limiting across Vercel instances.
 */

// Check if Upstash is configured
// Using REST API credentials (UPSTASH_REDIS_REST_URL and UPSTASH_REDIS_REST_TOKEN)
const isConfigured = Boolean(
  process.env.UPSTASH_REDIS_REST_URL && process.env.UPSTASH_REDIS_REST_TOKEN
);

// Create Redis client (only if configured)
const redis = isConfigured
  ? new Redis({
      url: process.env.UPSTASH_REDIS_REST_URL!,
      token: process.env.UPSTASH_REDIS_REST_TOKEN!,
    })
  : null;

/**
 * Email rate limiter: 5 requests per hour per email
 * Used for magic link requests to prevent spam
 */
export const emailRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(5, '1 h'),
      analytics: true,
      prefix: 'ratelimit:email',
    })
  : null;

/**
 * OTP rate limiter: 10 attempts per hour per email
 * Used for OTP verification to prevent brute force
 */
export const otpRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(10, '1 h'),
      analytics: true,
      prefix: 'ratelimit:otp',
    })
  : null;

/**
 * License operations rate limiter: 30 operations per day per user
 * Used for license issue, activate, renew, deactivate
 */
export const licenseRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(30, '1 d'),
      analytics: true,
      prefix: 'ratelimit:license',
    })
  : null;

/**
 * Profile API rate limiter: 100 requests per hour per user
 * Used for /api/me endpoint to prevent abuse
 */
export const profileRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(100, '1 h'),
      analytics: true,
      prefix: 'ratelimit:profile',
    })
  : null;

/**
 * Checkout rate limiter: 5 requests per minute per IP
 * Used for /api/checkout to prevent spam session creation
 */
export const checkoutRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(5, '1 m'),
      analytics: true,
      prefix: 'ratelimit:checkout',
    })
  : null;

/**
 * License activation rate limiter: 10 requests per hour per IP
 * Used for /api/licenses/activate to prevent brute force
 */
export const activationRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(10, '1 h'),
      analytics: true,
      prefix: 'ratelimit:activation',
    })
  : null;

/**
 * License validation rate limiter: 100 requests per hour per IP
 * Used for /api/licenses/validate to prevent brute force license key enumeration
 */
export const validationRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(100, '1 h'),
      analytics: true,
      prefix: 'ratelimit:validation',
    })
  : null;

/**
 * Helper to check rate limit and return appropriate response
 * @param identifier - User identifier (email, userId, etc.)
 * @param limiter - Rate limiter instance
 * @returns { allowed: boolean, limit: number, remaining: number, reset: Date }
 */
export async function checkRateLimit(
  identifier: string,
  limiter: Ratelimit | null
): Promise<{
  allowed: boolean;
  limit: number;
  remaining: number;
  reset: Date;
}> {
  // SECURITY FIX: Fail closed in production when rate limiting not configured
  if (!limiter) {
    if (process.env.NODE_ENV === 'production') {
      console.error('CRITICAL: Rate limiting not configured in production (missing Upstash credentials)');
      throw new Error('Rate limiting is required in production. Please configure Upstash Redis.');
    }

    // Development mode: allow requests but warn
    console.warn('Rate limiting not configured (missing Upstash credentials) - DEVELOPMENT MODE');
    return {
      allowed: true,
      limit: 999,
      remaining: 999,
      reset: new Date(Date.now() + 3600000),
    };
  }

  const { success, limit, remaining, reset } = await limiter.limit(identifier);

  return {
    allowed: success,
    limit,
    remaining,
    reset: new Date(reset),
  };
}

/**
 * Extract IP address from Next.js request
 * Handles various deployment environments (Vercel, localhost, etc.)
 */
export function getClientIp(request: NextRequest): string {
  // Vercel provides x-forwarded-for
  const forwarded = request.headers.get('x-forwarded-for');
  if (forwarded) {
    return forwarded.split(',')[0].trim();
  }

  // Cloudflare uses cf-connecting-ip
  const cfIp = request.headers.get('cf-connecting-ip');
  if (cfIp) {
    return cfIp;
  }

  // Standard x-real-ip header
  const realIp = request.headers.get('x-real-ip');
  if (realIp) {
    return realIp;
  }

  // Fallback for localhost/development (Next.js 15+ doesn't expose request.ip)
  return '127.0.0.1';
}
