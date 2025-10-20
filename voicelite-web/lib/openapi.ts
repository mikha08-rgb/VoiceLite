/**
 * OpenAPI Schema Generator for VoiceLite API
 *
 * This file generates OpenAPI 3.0 documentation from Zod schemas used in API routes.
 * The generated spec is used by Swagger UI at /api/docs
 */

import {
  OpenAPIRegistry,
  OpenApiGeneratorV3,
  extendZodWithOpenApi,
} from '@asteasolutions/zod-to-openapi';
import { z } from 'zod';

// Extend Zod with OpenAPI metadata support
extendZodWithOpenApi(z);

// Create OpenAPI registry
const registry = new OpenAPIRegistry();

// -----------------------------------------------------------------------------
// Shared Schemas (reusable across routes)
// -----------------------------------------------------------------------------

const ErrorResponseSchema = registry.register(
  'ErrorResponse',
  z.object({
    error: z.string().describe('Error message'),
    details: z.string().optional().describe('Additional error details'),
  })
);

const SuccessResponseSchema = registry.register(
  'SuccessResponse',
  z.object({
    message: z.string().describe('Success message'),
  })
);

// -----------------------------------------------------------------------------
// Auth Schemas
// -----------------------------------------------------------------------------

const AuthRequestBodySchema = registry.register(
  'AuthRequestBody',
  z.object({
    email: z.string().email().describe('User email address'),
  })
);

const OTPVerifyBodySchema = registry.register(
  'OTPVerifyBody',
  z.object({
    email: z.string().email().describe('User email address'),
    code: z.string().length(6).describe('6-digit OTP code'),
  })
);

const SessionResponseSchema = registry.register(
  'SessionResponse',
  z.object({
    sessionToken: z.string().describe('JWT session token'),
    user: z.object({
      id: z.string().describe('User ID'),
      email: z.string().email().describe('User email'),
    }),
  })
);

// -----------------------------------------------------------------------------
// Checkout Schemas
// -----------------------------------------------------------------------------

const CheckoutRequestSchema = registry.register(
  'CheckoutRequest',
  z.object({
    plan: z.enum(['quarterly', 'lifetime']).describe('Subscription plan type'),
    successUrl: z.string().url().optional().describe('Redirect URL after successful payment'),
    cancelUrl: z.string().url().optional().describe('Redirect URL after cancelled payment'),
  })
);

const CheckoutResponseSchema = registry.register(
  'CheckoutResponse',
  z.object({
    sessionId: z.string().describe('Stripe checkout session ID'),
    url: z.string().url().describe('Stripe checkout URL'),
  })
);

// -----------------------------------------------------------------------------
// License Schemas
// -----------------------------------------------------------------------------

const LicenseSchema = registry.register(
  'License',
  z.object({
    id: z.string().describe('License ID'),
    type: z.enum(['QUARTERLY', 'LIFETIME']).describe('License type'),
    validUntil: z.string().nullable().describe('License expiration date (ISO 8601)'),
    isActive: z.boolean().describe('Whether license is currently active'),
  })
);

const LicenseActivationRequestSchema = registry.register(
  'LicenseActivationRequest',
  z.object({
    licenseKey: z.string().describe('License key to activate'),
    deviceId: z.string().describe('Unique device identifier (machine fingerprint)'),
    deviceName: z.string().optional().describe('Human-readable device name'),
  })
);

const SignedLicenseResponseSchema = registry.register(
  'SignedLicenseResponse',
  z.object({
    license: z.string().describe('Base64-encoded signed license payload'),
    signature: z.string().describe('Ed25519 signature'),
    publicKey: z.string().describe('Public key for signature verification'),
  })
);

// -----------------------------------------------------------------------------
// Analytics Schemas
// -----------------------------------------------------------------------------

const AnalyticsEventSchema = registry.register(
  'AnalyticsEvent',
  z.object({
    eventType: z
      .enum(['APP_LAUNCHED', 'TRANSCRIPTION_COMPLETED', 'MODEL_CHANGED', 'SETTINGS_CHANGED', 'ERROR_OCCURRED', 'PRO_UPGRADE'])
      .describe('Type of analytics event'),
    anonymousUserId: z.string().describe('SHA256-hashed anonymous user ID'),
    tier: z.enum(['FREE', 'PRO']).describe('User tier'),
    appVersion: z.string().optional().describe('App version (e.g., "1.0.19")'),
    osVersion: z.string().optional().describe('OS version (e.g., "Windows 10")'),
    modelUsed: z.string().optional().describe('Whisper model used (e.g., "ggml-small.bin")'),
    metadata: z.record(z.string(), z.unknown()).optional().describe('Additional event metadata'),
  })
);

const AdminAnalyticsResponseSchema = registry.register(
  'AdminAnalyticsResponse',
  z.object({
    overview: z.object({
      totalEvents: z.number(),
      dailyActiveUsers: z.number(),
      monthlyActiveUsers: z.number(),
      dau_mau_ratio: z.string(),
    }),
    events: z.object({
      byType: z.record(z.string(), z.number()),
    }),
    users: z.object({
      tierDistribution: z.record(z.string(), z.number()),
    }),
    versions: z.object({
      distribution: z.array(
        z.object({
          version: z.string().nullable(),
          count: z.number(),
        })
      ),
    }),
    models: z.object({
      distribution: z.array(
        z.object({
          model: z.string().nullable(),
          count: z.number(),
        })
      ),
    }),
    os: z.object({
      distribution: z.array(
        z.object({
          os: z.string().nullable(),
          count: z.number(),
        })
      ),
    }),
    timeSeries: z.object({
      daily: z.array(
        z.object({
          date: z.string(),
          count: z.number(),
        })
      ),
    }),
    generatedAt: z.string(),
    dateRange: z.object({
      start: z.string(),
      end: z.string(),
      days: z.number(),
    }),
  })
);

// -----------------------------------------------------------------------------
// Feedback Schemas
// -----------------------------------------------------------------------------

const FeedbackSubmitSchema = registry.register(
  'FeedbackSubmit',
  z.object({
    email: z.string().email().describe('User email address'),
    message: z.string().min(10).max(2000).describe('Feedback message (10-2000 characters)'),
  })
);

// -----------------------------------------------------------------------------
// Register API Routes (Only endpoints that actually exist)
// -----------------------------------------------------------------------------

// Checkout Routes
registry.registerPath({
  method: 'post',
  path: '/api/checkout',
  tags: ['Payments'],
  summary: 'Create Stripe checkout session',
  description: 'Create a Stripe checkout session for purchasing a Pro subscription',
  request: {
    body: {
      content: {
        'application/json': {
          schema: CheckoutRequestSchema,
        },
      },
    },
  },
  responses: {
    200: {
      description: 'Checkout session created',
      content: {
        'application/json': {
          schema: CheckoutResponseSchema,
        },
      },
    },
    401: {
      description: 'Authentication required',
      content: {
        'application/json': {
          schema: ErrorResponseSchema,
        },
      },
    },
  },
});

// License Routes
registry.registerPath({
  method: 'post',
  path: '/api/licenses/activate',
  tags: ['Licenses'],
  summary: 'Activate license on device',
  description: 'Activate a license key on a specific device using machine fingerprinting',
  request: {
    body: {
      content: {
        'application/json': {
          schema: LicenseActivationRequestSchema,
        },
      },
    },
  },
  responses: {
    200: {
      description: 'License activated successfully',
      content: {
        'application/json': {
          schema: SuccessResponseSchema,
        },
      },
    },
    400: {
      description: 'Invalid license or activation limit reached',
      content: {
        'application/json': {
          schema: ErrorResponseSchema,
        },
      },
    },
  },
});

// -----------------------------------------------------------------------------
// Generate OpenAPI Document
// -----------------------------------------------------------------------------

export function generateOpenAPIDocument() {
  const generator = new OpenApiGeneratorV3(registry.definitions);

  return generator.generateDocument({
    openapi: '3.0.0',
    info: {
      title: 'VoiceLite API',
      version: '1.0.0',
      description: `
# VoiceLite API Documentation

This API powers the VoiceLite web platform for Pro license purchases and validation.

## Rate Limiting

API endpoints are rate-limited using Redis (Upstash). Limits vary by endpoint:
- License activation: 10 requests per hour per IP
- License validation: 100 requests per hour per IP
- Checkout: 5 requests per minute per IP
- Webhook: Internal only (Stripe signature validation)

## Privacy

VoiceLite is privacy-first:
- **100% local transcription** - your voice never leaves your computer
- No analytics or telemetry
- User emails only used for license delivery
- Minimal data collection

## Desktop App Integration

The desktop app communicates with this API for:
- Pro license activation (one-time, hardware-bound)
- License validation (online check with rate limiting)

After activation, the desktop app stores the license locally for offline use.
      `.trim(),
      contact: {
        name: 'VoiceLite Support',
        url: 'https://voicelite.app',
        email: 'support@voicelite.app',
      },
      license: {
        name: 'Proprietary',
        url: 'https://voicelite.app/terms',
      },
    },
    servers: [
      {
        url: 'https://voicelite.app',
        description: 'Production server',
      },
      {
        url: 'http://localhost:3000',
        description: 'Development server',
      },
    ],
    tags: [
      {
        name: 'Payments',
        description: 'Stripe checkout sessions for Pro license purchase ($20 one-time)',
      },
      {
        name: 'Licenses',
        description: 'License key activation and validation (hardware-bound)',
      },
      {
        name: 'Internal',
        description: 'Internal endpoints (webhooks)',
      },
    ],
  });
}

// Add actually implemented routes
registry.registerPath({
  method: 'post',
  path: '/api/licenses/validate',
  tags: ['Licenses'],
  summary: 'Validate license key',
  description: 'Validate a license key without activating it (rate limited: 100 req/hour)',
  request: {
    body: {
      content: {
        'application/json': {
          schema: z.object({
            licenseKey: z.string(),
          }),
        },
      },
    },
  },
  responses: {
    200: {
      description: 'License validation result',
      content: {
        'application/json': {
          schema: z.object({
            valid: z.boolean(),
            status: z.string().optional(),
            type: z.string().optional(),
          }),
        },
      },
    },
    429: {
      description: 'Too many requests',
      content: {
        'application/json': {
          schema: ErrorResponseSchema,
        },
      },
    },
  },
});

registry.registerPath({
  method: 'post',
  path: '/api/webhook',
  tags: ['Internal'],
  summary: 'Stripe webhook handler',
  description: 'Internal endpoint for processing Stripe webhook events (with timestamp validation)',
  responses: {
    200: {
      description: 'Webhook processed',
    },
    400: {
      description: 'Invalid signature or event too old',
    },
  },
});
