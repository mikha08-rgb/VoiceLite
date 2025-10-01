/**
 * Environment variable validation
 * Ensures all required configuration is present before the app starts
 */

export function validateEnvironment() {
  const errors: string[] = [];
  const warnings: string[] = [];

  // Required environment variables
  const required = {
    DATABASE_URL: process.env.DATABASE_URL,
    LICENSE_SIGNING_PRIVATE_B64: process.env.LICENSE_SIGNING_PRIVATE_B64,
    LICENSE_SIGNING_PUBLIC_B64: process.env.LICENSE_SIGNING_PUBLIC_B64,
    UPSTASH_REDIS_REST_URL: process.env.UPSTASH_REDIS_REST_URL,
    UPSTASH_REDIS_REST_TOKEN: process.env.UPSTASH_REDIS_REST_TOKEN,
  };

  // Check for missing or placeholder values
  for (const [key, value] of Object.entries(required)) {
    if (!value) {
      errors.push(`${key} is not set`);
    } else if (
      value.includes('PLACEHOLDER') ||
      value.includes('GENERATE') ||
      value.includes('EXAMPLE') ||
      value.includes('TODO')
    ) {
      errors.push(`${key} contains a placeholder value: "${value.substring(0, 20)}..."`);
    }
  }

  // Warn for recommended but not strictly required variables
  if (!process.env.RESEND_API_KEY || process.env.RESEND_API_KEY.includes('placeholder')) {
    warnings.push('RESEND_API_KEY not configured - email sending will fail');
  }

  if (!process.env.STRIPE_SECRET_KEY || process.env.STRIPE_SECRET_KEY.includes('placeholder')) {
    warnings.push('STRIPE_SECRET_KEY not configured - payments will fail');
  }

  if (
    !process.env.STRIPE_WEBHOOK_SECRET ||
    process.env.STRIPE_WEBHOOK_SECRET.includes('placeholder')
  ) {
    warnings.push('STRIPE_WEBHOOK_SECRET not configured - webhook processing will fail');
  }

  if (!process.env.NEXT_PUBLIC_APP_URL) {
    warnings.push('NEXT_PUBLIC_APP_URL not set - using fallback localhost:3000');
  }

  // Log warnings
  if (warnings.length > 0) {
    console.warn('⚠️  Environment configuration warnings:');
    warnings.forEach((warning) => console.warn(`   - ${warning}`));
  }

  // Throw if critical errors exist
  if (errors.length > 0) {
    const message = [
      '❌ Critical environment configuration errors:',
      ...errors.map((error) => `   - ${error}`),
      '',
      'Please check your .env.local file and ensure all required variables are set.',
      'See .env.example for reference.',
    ].join('\n');

    throw new Error(message);
  }

  if (process.env.NODE_ENV === 'production') {
    console.log('✅ Environment validation passed');
  }
}

/**
 * Validate environment on module load in production (runtime only, not build time)
 * In development, we allow missing vars for flexibility
 * Skip validation during build to allow builds without production secrets
 */
if (
  process.env.NODE_ENV === 'production' &&
  typeof window === 'undefined' // Server-side only
) {
  // Don't validate during build, only at runtime
  if (process.env.VERCEL || process.argv.includes('start')) {
    try {
      validateEnvironment();
    } catch (error) {
      console.error(error);
      process.exit(1);
    }
  }
}
