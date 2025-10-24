import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

/**
 * Rate limiting configuration for VoiceLite API endpoints.
 * Uses Upstash Redis for distributed rate limiting across Vercel instances.
 * Lazy initialization avoids build-time errors while environment validation ensures runtime safety.
 */

// Lazy Redis client initialization (deferred until first rate limit check)
// Environment validation ensures UPSTASH_REDIS_REST_URL and UPSTASH_REDIS_REST_TOKEN exist at runtime
let redis: Redis | null = null;

function getRedis(): Redis {
  if (!redis) {
    redis = new Redis({
      url: process.env.UPSTASH_REDIS_REST_URL!,
      token: process.env.UPSTASH_REDIS_REST_TOKEN!,
    });
  }
  return redis;
}

// Lazy rate limiter initialization
let emailRateLimitInstance: Ratelimit | null = null;
let otpRateLimitInstance: Ratelimit | null = null;
let licenseRateLimitInstance: Ratelimit | null = null;
let profileRateLimitInstance: Ratelimit | null = null;

/**
 * Email rate limiter: 5 requests per hour per email
 * Used for magic link requests to prevent spam
 */
export function getEmailRateLimit(): Ratelimit {
  if (!emailRateLimitInstance) {
    emailRateLimitInstance = new Ratelimit({
      redis: getRedis(),
      limiter: Ratelimit.slidingWindow(5, '1 h'),
      analytics: true,
      prefix: 'ratelimit:email',
    });
  }
  return emailRateLimitInstance;
}

/**
 * OTP rate limiter: 10 attempts per hour per email
 * Used for OTP verification to prevent brute force
 */
export function getOtpRateLimit(): Ratelimit {
  if (!otpRateLimitInstance) {
    otpRateLimitInstance = new Ratelimit({
      redis: getRedis(),
      limiter: Ratelimit.slidingWindow(10, '1 h'),
      analytics: true,
      prefix: 'ratelimit:otp',
    });
  }
  return otpRateLimitInstance;
}

/**
 * License operations rate limiter: 30 operations per day per user
 * Used for license issue, activate, renew, deactivate
 */
export function getLicenseRateLimit(): Ratelimit {
  if (!licenseRateLimitInstance) {
    licenseRateLimitInstance = new Ratelimit({
      redis: getRedis(),
      limiter: Ratelimit.slidingWindow(30, '1 d'),
      analytics: true,
      prefix: 'ratelimit:license',
    });
  }
  return licenseRateLimitInstance;
}

/**
 * Profile API rate limiter: 100 requests per hour per user
 * Used for /api/me endpoint to prevent abuse
 */
export function getProfileRateLimit(): Ratelimit {
  if (!profileRateLimitInstance) {
    profileRateLimitInstance = new Ratelimit({
      redis: getRedis(),
      limiter: Ratelimit.slidingWindow(100, '1 h'),
      analytics: true,
      prefix: 'ratelimit:profile',
    });
  }
  return profileRateLimitInstance;
}

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

