import { Ratelimit } from '@upstash/ratelimit';
import { Redis } from '@upstash/redis';

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
 * Email resend rate limiter: 3 requests per hour per IP
 * Used for /api/licenses/resend-email to prevent enumeration and spam
 */
export const emailResendRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(3, '1 h'),
      analytics: true,
      prefix: 'ratelimit:email-resend',
    })
  : null;

/**
 * License validation multi-layer rate limiting (prevents brute force attacks)
 * Layer 1: IP-based (30/hour per IP) - allows retries for typos, NAT/VPN users
 * Layer 2: License key-based (30/hour per key)
 * Layer 3: Global (1000/hour across all requests)
 */
export const licenseValidationIpRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(30, '1 h'),
      analytics: true,
      prefix: 'ratelimit:license-validation:ip',
    })
  : null;

export const licenseValidationKeyRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(30, '1 h'),
      analytics: true,
      prefix: 'ratelimit:license-validation:key',
    })
  : null;

export const licenseValidationGlobalRateLimit = redis
  ? new Ratelimit({
      redis,
      limiter: Ratelimit.slidingWindow(1000, '1 h'),
      analytics: true,
      prefix: 'ratelimit:license-validation:global',
    })
  : null;

/**
 * Helper to check rate limit and return appropriate response
 * CRITICAL-3 FIX: Changed from fail-open to fail-closed with in-memory fallback
 *
 * @param identifier - User identifier (email, userId, etc.)
 * @param limiter - Rate limiter instance (Upstash)
 * @param fallbackLimiter - In-memory fallback limiter (used when Upstash unavailable)
 * @returns { allowed: boolean, limit: number, remaining: number, reset: Date }
 */
export async function checkRateLimit(
  identifier: string,
  limiter: Ratelimit | null,
  fallbackLimiter?: InMemoryRateLimiter
): Promise<{
  allowed: boolean;
  limit: number;
  remaining: number;
  reset: Date;
}> {
  // CRITICAL-3 FIX: If Upstash not configured, use in-memory fallback (fail-closed, not fail-open)
  if (!limiter) {
    if (fallbackLimiter) {
      console.warn('Rate limiting: Upstash not configured, using in-memory fallback');
      const allowed = await fallbackLimiter.check(identifier);
      return {
        allowed,
        limit: fallbackLimiter.maxRequests,
        remaining: allowed ? fallbackLimiter.maxRequests - 1 : 0,
        reset: new Date(Date.now() + fallbackLimiter.windowMs),
      };
    }
    // No fallback provided - fail closed with stricter default limits
    console.warn('Rate limiting: No Upstash or fallback configured, applying strict default limit');
    return {
      allowed: false,
      limit: 1,
      remaining: 0,
      reset: new Date(Date.now() + 3600000),
    };
  }

  try {
    const { success, limit, remaining, reset } = await limiter.limit(identifier);

    return {
      allowed: success,
      limit,
      remaining,
      reset: new Date(reset),
    };
  } catch (error) {
    // CRITICAL-3 FIX: If Redis fails, use fallback limiter instead of allowing through
    console.error('Rate limiting failed (Redis unreachable), using fallback:', error);

    if (fallbackLimiter) {
      const allowed = await fallbackLimiter.check(identifier);
      return {
        allowed,
        limit: fallbackLimiter.maxRequests,
        remaining: allowed ? fallbackLimiter.maxRequests - 1 : 0,
        reset: new Date(Date.now() + fallbackLimiter.windowMs),
      };
    }

    // No fallback - fail closed
    console.error('No fallback limiter available, blocking request');
    return {
      allowed: false,
      limit: 1,
      remaining: 0,
      reset: new Date(Date.now() + 3600000),
    };
  }
}

/**
 * Simple async lock for preventing race conditions in concurrent access
 * HIGH-2 FIX: Added to prevent rate limit bypass when multiple requests arrive simultaneously
 */
class AsyncLock {
  private locked = false;
  private waitQueue: (() => void)[] = [];

  async acquire(): Promise<void> {
    if (!this.locked) {
      this.locked = true;
      return;
    }

    return new Promise<void>(resolve => {
      this.waitQueue.push(resolve);
    });
  }

  release(): void {
    const next = this.waitQueue.shift();
    if (next) {
      next();
    } else {
      this.locked = false;
    }
  }
}

/**
 * In-memory fallback rate limiter (not recommended for production with multiple instances)
 * Used as fallback when Upstash is not configured
 * CRITICAL-3 FIX: Made properties public for access in checkRateLimit
 * HIGH-2 FIX: Added async lock for thread-safe access in concurrent requests
 */
export class InMemoryRateLimiter {
  private requests: Map<string, { count: number; resetAt: number }> = new Map();
  private lock = new AsyncLock();
  public readonly maxRequests: number;
  public readonly windowMs: number;

  constructor(maxRequests: number, windowMs: number) {
    this.maxRequests = maxRequests;
    this.windowMs = windowMs;
  }

  async check(identifier: string): Promise<boolean> {
    // HIGH-2 FIX: Acquire lock to prevent race conditions
    // Without this, concurrent requests could both read count=2, both pass,
    // and both increment to count=3, bypassing the limit
    await this.lock.acquire();

    try {
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
    } finally {
      this.lock.release();
    }
  }

  // Cleanup old entries periodically
  async cleanup(): Promise<void> {
    await this.lock.acquire();
    try {
      const now = Date.now();
      for (const [key, record] of this.requests.entries()) {
        if (now > record.resetAt) {
          this.requests.delete(key);
        }
      }
    } finally {
      this.lock.release();
    }
  }
}

// Fallback limiters (in-memory, single-instance only)
export const fallbackEmailLimit = new InMemoryRateLimiter(5, 60 * 60 * 1000); // 5/hour
export const fallbackOtpLimit = new InMemoryRateLimiter(10, 60 * 60 * 1000); // 10/hour
export const fallbackLicenseLimit = new InMemoryRateLimiter(30, 24 * 60 * 60 * 1000); // 30/day
export const fallbackEmailResendLimit = new InMemoryRateLimiter(3, 60 * 60 * 1000); // 3/hour

// Fallback limiters for license validation (matches Redis limits for consistency)
export const fallbackLicenseValidationIpLimit = new InMemoryRateLimiter(30, 60 * 60 * 1000); // 30/hour per IP
export const fallbackLicenseValidationKeyLimit = new InMemoryRateLimiter(30, 60 * 60 * 1000); // 30/hour per key
export const fallbackLicenseValidationGlobalLimit = new InMemoryRateLimiter(1000, 60 * 60 * 1000); // 1000/hour global
export const fallbackProfileLimit = new InMemoryRateLimiter(100, 60 * 60 * 1000); // 100/hour

// Cleanup fallback limiters every 10 minutes
// HIGH-2 FIX: Updated to use async cleanup methods
if (!isConfigured) {
  setInterval(async () => {
    await Promise.all([
      fallbackEmailLimit.cleanup(),
      fallbackOtpLimit.cleanup(),
      fallbackLicenseLimit.cleanup(),
      fallbackEmailResendLimit.cleanup(),
      fallbackLicenseValidationIpLimit.cleanup(),
      fallbackLicenseValidationKeyLimit.cleanup(),
      fallbackLicenseValidationGlobalLimit.cleanup(),
      fallbackProfileLimit.cleanup(),
    ]);
  }, 10 * 60 * 1000);
}
