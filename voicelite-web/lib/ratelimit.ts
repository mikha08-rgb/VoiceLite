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
  // If rate limiting not configured, allow all requests
  if (!limiter) {
    console.warn('Rate limiting not configured (missing Upstash credentials)');
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
 * In-memory fallback rate limiter (not recommended for production with multiple instances)
 * Used as fallback when Upstash is not configured
 */
class InMemoryRateLimiter {
  private requests: Map<string, { count: number; resetAt: number }> = new Map();

  constructor(private maxRequests: number, private windowMs: number) {}

  async check(identifier: string): Promise<boolean> {
    const now = Date.now();
    const record = this.requests.get(identifier);

    if (!record || now > record.resetAt) {
      this.requests.set(identifier, {
        count: 1,
        resetAt: now + this.windowMs,
      });
      return true;
    }

    if (record.count >= this.maxRequests) {
      return false;
    }

    record.count++;
    return true;
  }

  // Cleanup old entries periodically
  cleanup() {
    const now = Date.now();
    for (const [key, record] of this.requests.entries()) {
      if (now > record.resetAt) {
        this.requests.delete(key);
      }
    }
  }
}

// Fallback limiters (in-memory, single-instance only)
export const fallbackEmailLimit = new InMemoryRateLimiter(5, 60 * 60 * 1000); // 5/hour
export const fallbackOtpLimit = new InMemoryRateLimiter(10, 60 * 60 * 1000); // 10/hour
export const fallbackLicenseLimit = new InMemoryRateLimiter(30, 24 * 60 * 60 * 1000); // 30/day
export const fallbackCheckoutLimit = new InMemoryRateLimiter(5, 60 * 1000); // 5/minute
export const fallbackActivationLimit = new InMemoryRateLimiter(10, 60 * 60 * 1000); // 10/hour
export const fallbackValidationLimit = new InMemoryRateLimiter(100, 60 * 60 * 1000); // 100/hour

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

  // Fallback for localhost/development
  return request.ip || '127.0.0.1';
}

// Cleanup fallback limiters every 10 minutes
if (!isConfigured) {
  setInterval(() => {
    fallbackEmailLimit.cleanup();
    fallbackOtpLimit.cleanup();
    fallbackLicenseLimit.cleanup();
    fallbackCheckoutLimit.cleanup();
    fallbackActivationLimit.cleanup();
    fallbackValidationLimit.cleanup();
  }, 10 * 60 * 1000);
}
