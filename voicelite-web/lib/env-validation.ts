/**
 * Environment variable validation (Enhanced with Zod)
 * Ensures all required configuration is present before the app starts
 *
 * Uses Zod for type-safe validation with helpful error messages
 */

import { z } from 'zod';

const envSchema = z.object({
  // -----------------------------------------------------------------------------
  // Database (Supabase Postgres)
  // -----------------------------------------------------------------------------
  DATABASE_URL: z
    .string()
    .min(1, 'DATABASE_URL is required')
    .refine(
      (val) => val.startsWith('postgresql://'),
      'DATABASE_URL must be a PostgreSQL connection string (postgresql://...)'
    )
    .refine(
      (val) => !val.includes('PLACEHOLDER') && !val.includes('EXAMPLE'),
      'DATABASE_URL contains a placeholder value - get from https://app.supabase.com'
    ),

  // -----------------------------------------------------------------------------
  // Ed25519 Signing Keys (License & CRL)
  // -----------------------------------------------------------------------------
  LICENSE_SIGNING_PRIVATE_B64: z
    .string()
    .length(64, 'LICENSE_SIGNING_PRIVATE_B64 must be 64 characters (generate with: npm run keygen)')
    .refine(
      (val) => !val.includes('GENERATE') && !val.includes('PLACEHOLDER'),
      '⚠️ CRITICAL: LICENSE_SIGNING_PRIVATE_B64 is a placeholder! Run: npm run keygen'
    ),

  LICENSE_SIGNING_PUBLIC_B64: z
    .string()
    .length(64, 'LICENSE_SIGNING_PUBLIC_B64 must be 64 characters (generate with: npm run keygen)')
    .refine(
      (val) => !val.includes('GENERATE') && !val.includes('PLACEHOLDER'),
      '⚠️ CRITICAL: LICENSE_SIGNING_PUBLIC_B64 is a placeholder! Run: npm run keygen'
    ),

  CRL_SIGNING_PRIVATE_B64: z
    .string()
    .length(64, 'CRL_SIGNING_PRIVATE_B64 must be 64 characters (generate with: npm run keygen)')
    .refine(
      (val) => !val.includes('GENERATE') && !val.includes('PLACEHOLDER'),
      'CRL_SIGNING_PRIVATE_B64 is a placeholder'
    )
    .optional(),

  CRL_SIGNING_PUBLIC_B64: z
    .string()
    .length(64, 'CRL_SIGNING_PUBLIC_B64 must be 64 characters (generate with: npm run keygen)')
    .refine(
      (val) => !val.includes('GENERATE') && !val.includes('PLACEHOLDER'),
      'CRL_SIGNING_PUBLIC_B64 is a placeholder'
    )
    .optional(),

  // -----------------------------------------------------------------------------
  // Redis (Upstash - Rate Limiting) - Now OPTIONAL per .env.example
  // -----------------------------------------------------------------------------
  UPSTASH_REDIS_REST_URL: z
    .string()
    .url('UPSTASH_REDIS_REST_URL must be a valid URL')
    .startsWith('https://', 'UPSTASH_REDIS_REST_URL must use HTTPS')
    .refine(
      (val) => !val.includes('placeholder'),
      'UPSTASH_REDIS_REST_URL contains a placeholder - get from https://console.upstash.com'
    )
    .optional(),

  UPSTASH_REDIS_REST_TOKEN: z.string().min(1).optional(),

  // -----------------------------------------------------------------------------
  // Stripe (Payments) - Warnings only, not hard requirements
  // -----------------------------------------------------------------------------
  STRIPE_SECRET_KEY: z
    .string()
    .regex(/^sk_(test|live)_/, 'STRIPE_SECRET_KEY must start with sk_test_ or sk_live_')
    .optional(),

  STRIPE_WEBHOOK_SECRET: z
    .string()
    .regex(/^whsec_/, 'STRIPE_WEBHOOK_SECRET must start with whsec_')
    .optional(),

  STRIPE_QUARTERLY_PRICE_ID: z
    .string()
    .regex(/^price_/, 'STRIPE_QUARTERLY_PRICE_ID must start with price_')
    .refine((val) => !val.includes('placeholder'), 'STRIPE_QUARTERLY_PRICE_ID is a placeholder')
    .optional(),

  STRIPE_LIFETIME_PRICE_ID: z
    .string()
    .regex(/^price_/, 'STRIPE_LIFETIME_PRICE_ID must start with price_')
    .refine((val) => !val.includes('placeholder'), 'STRIPE_LIFETIME_PRICE_ID is a placeholder')
    .optional(),

  // -----------------------------------------------------------------------------
  // Email (Resend)
  // -----------------------------------------------------------------------------
  RESEND_API_KEY: z
    .string()
    .regex(/^re_/, 'RESEND_API_KEY must start with re_')
    .optional(),

  RESEND_FROM_EMAIL: z
    .string()
    .email()
    .or(z.string().regex(/^.+<.+@.+>$/))
    .optional(),

  // -----------------------------------------------------------------------------
  // Application URLs
  // -----------------------------------------------------------------------------
  NEXT_PUBLIC_APP_URL: z.string().url().optional(),

  // -----------------------------------------------------------------------------
  // Admin Access
  // -----------------------------------------------------------------------------
  ADMIN_EMAILS: z
    .string()
    .min(1)
    .refine(
      (val) => {
        const emails = val.split(',').map((e) => e.trim());
        return emails.every((email) => z.string().email().safeParse(email).success);
      },
      'ADMIN_EMAILS must be comma-separated valid email addresses'
    )
    .optional(),

  // -----------------------------------------------------------------------------
  // Node Environment
  // -----------------------------------------------------------------------------
  NODE_ENV: z.enum(['development', 'production', 'test']).optional(),
});

export function validateEnvironment() {
  const errors: string[] = [];
  const warnings: string[] = [];

  try {
    // Validate using Zod schema
    envSchema.parse(process.env);

    // Additional warnings for optional but recommended vars
    if (!process.env.RESEND_API_KEY) {
      warnings.push('RESEND_API_KEY not configured - email sending will fail');
    }

    if (!process.env.STRIPE_SECRET_KEY) {
      warnings.push('STRIPE_SECRET_KEY not configured - payments will fail');
    }

    if (!process.env.STRIPE_WEBHOOK_SECRET) {
      warnings.push('STRIPE_WEBHOOK_SECRET not configured - webhook processing will fail');
    }

    if (!process.env.NEXT_PUBLIC_APP_URL) {
      warnings.push('NEXT_PUBLIC_APP_URL not set - using fallback localhost:3000');
    }

    if (!process.env.ADMIN_EMAILS) {
      warnings.push('ADMIN_EMAILS not set - admin dashboard will be inaccessible');
    }

    if (!process.env.UPSTASH_REDIS_REST_URL) {
      warnings.push('UPSTASH_REDIS_REST_URL not set - rate limiting disabled (acceptable per .env.example)');
    }

    // Log warnings
    if (warnings.length > 0 && process.env.NODE_ENV !== 'test') {
      console.warn('\n⚠️  Environment configuration warnings:');
      warnings.forEach((warning) => console.warn(`   - ${warning}`));
      console.warn('');
    }

    if (process.env.NODE_ENV === 'production') {
      console.log('✅ Environment validation passed');
    }
  } catch (error) {
    if (error instanceof z.ZodError) {
      error.errors.forEach((err) => {
        const path = err.path.join('.');
        errors.push(`${path}: ${err.message}`);
      });

      const message = [
        '❌ Critical environment configuration errors:',
        ...errors.map((error) => `   - ${error}`),
        '',
        'Please check your .env.local file and ensure all required variables are set.',
        'See .env.example for reference.',
        'Generate signing keys with: npm run keygen',
      ].join('\n');

      throw new Error(message);
    }
    throw error;
  }
}

/**
 * Validate environment on module load in production (runtime only, not build time)
 * In development, we allow missing vars for flexibility
 * Skip validation during build to allow builds without production secrets
 *
 * This only runs on Vercel in production at runtime, never during `next build`
 */
if (
  typeof window === 'undefined' && // Server-side only
  process.env.VERCEL && // Only in Vercel production
  !process.env.SKIP_ENV_VALIDATION // Allow manual skip
) {
  try {
    validateEnvironment();
  } catch (error) {
    console.error(error);
    process.exit(1);
  }
}

// Export validated env for type-safe access (development helper)
export const env = process.env as z.infer<typeof envSchema>;
