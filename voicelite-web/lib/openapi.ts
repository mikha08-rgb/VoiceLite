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
// Register API Routes
// -----------------------------------------------------------------------------

// Auth Routes
registry.registerPath({
  method: 'post',
  path: '/api/auth/request',
  tags: ['Authentication'],
  summary: 'Request magic link login',
  description: 'Send a magic link to the user\'s email for passwordless authentication',
  request: {
    body: {
      content: {
        'application/json': {
          schema: AuthRequestBodySchema,
        },
      },
    },
  },
  responses: {
    200: {
      description: 'Magic link sent successfully',
      content: {
        'application/json': {
          schema: SuccessResponseSchema,
        },
      },
    },
    400: {
      description: 'Invalid request',
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
  path: '/api/auth/otp',
  tags: ['Authentication'],
  summary: 'Verify OTP code',
  description: 'Verify the 6-digit OTP code sent via email and create a session',
  request: {
    body: {
      content: {
        'application/json': {
          schema: OTPVerifyBodySchema,
        },
      },
    },
  },
  responses: {
    200: {
      description: 'OTP verified, session created',
      content: {
        'application/json': {
          schema: SessionResponseSchema,
        },
      },
    },
    401: {
      description: 'Invalid or expired OTP',
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
  path: '/api/auth/logout',
  tags: ['Authentication'],
  summary: 'Logout user',
  description: 'Revoke the current session token',
  responses: {
    200: {
      description: 'Logged out successfully',
      content: {
        'application/json': {
          schema: SuccessResponseSchema,
        },
      },
    },
  },
});

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

registry.registerPath({
  method: 'post',
  path: '/api/licenses/issue',
  tags: ['Licenses'],
  summary: 'Issue signed license file',
  description: 'Generate a cryptographically signed license file for desktop app validation',
  responses: {
    200: {
      description: 'Signed license issued',
      content: {
        'application/json': {
          schema: SignedLicenseResponseSchema,
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

registry.registerPath({
  method: 'get',
  path: '/api/licenses/crl',
  tags: ['Licenses'],
  summary: 'Get Certificate Revocation List',
  description: 'Fetch the list of revoked license IDs for offline validation',
  responses: {
    200: {
      description: 'CRL retrieved successfully',
      content: {
        'application/json': {
          schema: z.object({
            revokedLicenses: z.array(z.string()),
            signature: z.string(),
            issuedAt: z.string(),
          }),
        },
      },
    },
  },
});

// Analytics Routes
registry.registerPath({
  method: 'post',
  path: '/api/analytics/event',
  tags: ['Analytics'],
  summary: 'Submit analytics event',
  description: 'Submit a privacy-first analytics event (opt-in only, anonymous)',
  request: {
    body: {
      content: {
        'application/json': {
          schema: AnalyticsEventSchema,
        },
      },
    },
  },
  responses: {
    200: {
      description: 'Event recorded',
      content: {
        'application/json': {
          schema: SuccessResponseSchema,
        },
      },
    },
  },
});

registry.registerPath({
  method: 'get',
  path: '/api/admin/analytics',
  tags: ['Admin'],
  summary: 'Get analytics dashboard data',
  description: 'Retrieve aggregated analytics data (admin only)',
  parameters: [
    {
      name: 'days',
      in: 'query',
      description: 'Number of days to include (default: 30, max: 365)',
      required: false,
      schema: {
        type: 'number',
        default: 30,
      },
    },
  ],
  responses: {
    200: {
      description: 'Analytics data retrieved',
      content: {
        'application/json': {
          schema: AdminAnalyticsResponseSchema,
        },
      },
    },
    401: {
      description: 'Unauthorized - admin access required',
      content: {
        'application/json': {
          schema: ErrorResponseSchema,
        },
      },
    },
  },
});

// Feedback Routes
registry.registerPath({
  method: 'post',
  path: '/api/feedback/submit',
  tags: ['Feedback'],
  summary: 'Submit user feedback',
  description: 'Submit feedback or bug reports',
  request: {
    body: {
      content: {
        'application/json': {
          schema: FeedbackSubmitSchema,
        },
      },
    },
  },
  responses: {
    200: {
      description: 'Feedback submitted',
      content: {
        'application/json': {
          schema: SuccessResponseSchema,
        },
      },
    },
  },
});

// User Routes
registry.registerPath({
  method: 'get',
  path: '/api/me',
  tags: ['User'],
  summary: 'Get current user profile',
  description: 'Get the authenticated user\'s profile and active licenses',
  responses: {
    200: {
      description: 'User profile retrieved',
      content: {
        'application/json': {
          schema: z.object({
            user: z.object({
              id: z.string(),
              email: z.string().email(),
            }),
            licenses: z.array(LicenseSchema),
          }),
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

This API powers the VoiceLite web platform for managing Pro subscriptions, licenses, and user accounts.

## Authentication

Most endpoints require authentication via session cookies. Use the \`/api/auth/request\` and \`/api/auth/otp\` endpoints to authenticate.

## Rate Limiting

API endpoints are rate-limited using Redis (Upstash). Limits vary by endpoint:
- Auth endpoints: 5 requests per 15 minutes
- License endpoints: 10 requests per minute
- Analytics endpoints: Unlimited (opt-in only)

## Privacy

VoiceLite is privacy-first:
- Analytics are **opt-in only** and use SHA256-hashed anonymous IDs
- No IP addresses are logged
- No recording content is ever transmitted (all processing is local)
- User emails are only used for authentication and license delivery

## Desktop App Integration

The desktop app communicates with this API for:
- Pro license validation (Ed25519 cryptographic signatures)
- Certificate Revocation List (CRL) checks
- Optional privacy-first analytics (if user opts in)

All license validation happens **offline** after initial license fetch.
      `.trim(),
      contact: {
        name: 'VoiceLite Support',
        url: 'https://voicelite.app',
        email: 'BasmentHustleLLC@gmail.com',
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
        name: 'Authentication',
        description: 'Passwordless authentication via magic links and OTP codes',
      },
      {
        name: 'Payments',
        description: 'Stripe checkout sessions for Pro subscriptions',
      },
      {
        name: 'Licenses',
        description: 'License key activation and validation with Ed25519 signatures',
      },
      {
        name: 'Analytics',
        description: 'Privacy-first opt-in analytics (anonymous SHA256 IDs)',
      },
      {
        name: 'Admin',
        description: 'Admin-only endpoints for dashboard and management',
      },
      {
        name: 'Feedback',
        description: 'User feedback and bug reports',
      },
      {
        name: 'User',
        description: 'User profile and account management',
      },
      {
        name: 'Internal',
        description: 'Internal endpoints (webhooks, migrations)',
      },
    ],
  });
}

// Add remaining routes that were missing
registry.registerPath({
  method: 'get',
  path: '/api/licenses/mine',
  tags: ['Licenses'],
  summary: 'Get user licenses with activations',
  description: 'Get all licenses belonging to the authenticated user with device activation details',
  responses: {
    200: {
      description: 'Licenses retrieved',
      content: {
        'application/json': {
          schema: z.object({
            licenses: z.array(z.object({
              id: z.string(),
              licenseKey: z.string(),
              type: z.enum(['QUARTERLY', 'LIFETIME']),
              status: z.string(),
              activatedAt: z.string().nullable(),
              expiresAt: z.string().nullable(),
              activations: z.array(z.object({
                id: z.string(),
                machineId: z.string(),
                machineLabel: z.string().nullable(),
                activatedAt: z.string(),
                lastValidatedAt: z.string().nullable(),
                status: z.string(),
              })),
            })),
          }),
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

registry.registerPath({
  method: 'post',
  path: '/api/licenses/deactivate',
  tags: ['Licenses'],
  summary: 'Deactivate license on device',
  description: 'Deactivate a license from a specific device to free up an activation slot',
  request: {
    body: {
      content: {
        'application/json': {
          schema: z.object({
            activationId: z.string().describe('Activation ID to deactivate'),
          }),
        },
      },
    },
  },
  responses: {
    200: {
      description: 'License deactivated',
      content: {
        'application/json': {
          schema: SuccessResponseSchema,
        },
      },
    },
  },
});

registry.registerPath({
  method: 'post',
  path: '/api/licenses/validate',
  tags: ['Licenses'],
  summary: 'Validate license key',
  description: 'Validate a license key without activating it',
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
            license: LicenseSchema.optional(),
          }),
        },
      },
    },
  },
});

registry.registerPath({
  method: 'get',
  path: '/api/admin/stats',
  tags: ['Admin'],
  summary: 'Get admin dashboard statistics',
  description: 'Retrieve overall platform statistics (admin only)',
  responses: {
    200: {
      description: 'Statistics retrieved',
    },
    401: {
      description: 'Unauthorized',
      content: {
        'application/json': {
          schema: ErrorResponseSchema,
        },
      },
    },
  },
});

registry.registerPath({
  method: 'get',
  path: '/api/admin/feedback',
  tags: ['Admin'],
  summary: 'Get all feedback submissions',
  description: 'Retrieve all user feedback (admin only)',
  responses: {
    200: {
      description: 'Feedback retrieved',
    },
    401: {
      description: 'Unauthorized',
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
  path: '/api/billing/portal',
  tags: ['Payments'],
  summary: 'Create Stripe customer portal session',
  description: 'Create a Stripe customer portal link for managing subscriptions',
  responses: {
    200: {
      description: 'Portal session created',
      content: {
        'application/json': {
          schema: z.object({
            url: z.string().url(),
          }),
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
  description: 'Internal endpoint for processing Stripe webhook events',
  responses: {
    200: {
      description: 'Webhook processed',
    },
  },
});
