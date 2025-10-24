import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

/**
 * Rate limiting configuration for VoiceLite API endpoints.
 * Uses Upstash Redis for distributed rate limiting across Vercel instances.
 * Environment validation ensures Redis credentials exist at startup.
 */

// Create Redis client
// Environment validation ensures UPSTASH_REDIS_REST_URL and UPSTASH_REDIS_REST_TOKEN exist
const redis = new Redis({
  url: process.env.UPSTASH_REDIS_REST_URL!,
  token: process.env.UPSTASH_REDIS_REST_TOKEN!,
});

/**
 * Email rate limiter: 5 requests per hour per email
 * Used for magic link requests to prevent spam
 */
export const emailRateLimit = new Ratelimit({
  redis,
  limiter: Ratelimit.slidingWindow(5, '1 h'),
  analytics: true,
  prefix: 'ratelimit:email',
});

/**
 * OTP rate limiter: 10 attempts per hour per email
 * Used for OTP verification to prevent brute force
 */
export const otpRateLimit = new Ratelimit({
  redis,
  limiter: Ratelimit.slidingWindow(10, '1 h'),
  analytics: true,
  prefix: 'ratelimit:otp',
});

/**
 * License operations rate limiter: 30 operations per day per user
 * Used for license issue, activate, renew, deactivate
 */
export const licenseRateLimit = new Ratelimit({
  redis,
  limiter: Ratelimit.slidingWindow(30, '1 d'),
  analytics: true,
  prefix: 'ratelimit:license',
});

/**
 * Profile API rate limiter: 100 requests per hour per user
 * Used for /api/me endpoint to prevent abuse
 */
export const profileRateLimit = new Ratelimit({
  redis,
  limiter: Ratelimit.slidingWindow(100, '1 h'),
  analytics: true,
  prefix: 'ratelimit:profile',
});

/**
 * Helper to check rate limit and return appropriate response
 * @param identifier - User identifier (email, userId, etc.)
 * @param limiter - Rate limiter instance
 * @returns { allowed: boolean, limit: number, remaining: number, reset: Date }
 */
export async function checkRateLimit(
  identifier: string,
  limiter: Ratelimit
): Promise<{
  allowed: boolean;
  limit: number;
  remaining: number;
  reset: Date;
}> {
  const { success, limit, remaining, reset } = await limiter.limit(identifier);

  return {
    allowed: success,
    limit,
    remaining,
    reset: new Date(reset),
  };
}

