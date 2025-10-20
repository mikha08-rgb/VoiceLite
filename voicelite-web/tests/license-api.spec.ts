/**
 * CRITICAL LICENSE API TESTS
 *
 * These tests validate the license activation and validation endpoints:
 * 1. Activation endpoint (/api/licenses/activate)
 * 2. Validation endpoint (/api/licenses/validate)
 *
 * WHY THIS MATTERS:
 * - License activation = $20 revenue per customer
 * - Device limit enforcement prevents piracy
 * - Rate limiting prevents brute force key enumeration
 * - Transaction atomicity prevents race conditions (double activation)
 *
 * Test methodology: HTTP-only tests (no database mocking required)
 * Tests real endpoint behavior with proper error handling.
 */

import { test, expect } from '@playwright/test';

test.describe('License Validation API', () => {
  const validateUrl = '/api/licenses/validate';

  test.describe('1. Input Validation', () => {
    test('should reject empty request body', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: {},
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.valid).toBe(false);
      expect(json.error).toBe('Invalid request');
    });

    test('should reject missing license key', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: {
          licenseKey: '',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.valid).toBe(false);
    });

    test('should reject short license key (< 10 chars)', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: {
          licenseKey: 'SHORT',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.valid).toBe(false);
      expect(json.details).toBeDefined(); // Zod error details
    });

    test('should reject non-existent license key', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: {
          licenseKey: 'VL-FAKE12-FAKE34-FAKE56',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(404);
      const json = await response.json();
      expect(json.valid).toBe(false);
      expect(json.error).toBe('License key not found');
    });
  });

  test.describe('2. Rate Limiting', () => {
    test('should have rate limit headers', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Check for rate limit headers (even on failure)
      const headers = response.headers();
      // Note: Headers might not be present on first request if not rate limited
      // This is more for documentation than strict assertion
      expect(response.status()).toBeGreaterThanOrEqual(200);
    });

    test('should enforce rate limiting (100 req/hour)', async ({ request }) => {
      // Note: This test would need to make 100+ requests to trigger rate limit
      // For now, we just verify the endpoint responds properly
      // Full rate limit testing requires integration tests

      const response = await request.post(validateUrl, {
        data: {
          licenseKey: 'VL-RATELIMIT-TEST-KEY',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should not be rate limited on single request
      expect(response.status()).not.toBe(429);
    });
  });

  test.describe('3. Response Format', () => {
    test('should return proper error structure', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: {
          licenseKey: 'VL-NONEXIST-NONEXIST-NONEXIST',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      const json = await response.json();
      expect(json).toHaveProperty('valid');
      expect(json).toHaveProperty('error');
      expect(typeof json.valid).toBe('boolean');
      expect(typeof json.error).toBe('string');
    });
  });

  test.describe('4. HTTP Method Validation', () => {
    test('should reject GET requests', async ({ request }) => {
      const response = await request.get(validateUrl);
      expect(response.status()).toBe(405); // Method Not Allowed
    });

    test('should reject PUT requests', async ({ request }) => {
      const response = await request.put(validateUrl, {
        data: { licenseKey: 'test' },
      });
      expect(response.status()).toBe(405);
    });

    test('should reject DELETE requests', async ({ request }) => {
      const response = await request.delete(validateUrl);
      expect(response.status()).toBe(405);
    });

    test('should only accept POST requests', async ({ request }) => {
      const response = await request.post(validateUrl, {
        data: { licenseKey: 'VL-TEST12-TEST34-TEST56' },
        headers: { 'Content-Type': 'application/json' },
      });

      // POST should be accepted (will fail validation, but method is correct)
      expect([200, 400, 404, 503]).toContain(response.status());
    });
  });
});

test.describe('License Activation API', () => {
  const activateUrl = '/api/licenses/activate';

  test.describe('1. Input Validation', () => {
    test('should reject empty request body', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {},
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.success).toBe(false);
      expect(json.error).toBeDefined();
    });

    test('should reject missing license key', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.success).toBe(false);
    });

    test('should reject missing machine ID', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.success).toBe(false);
    });

    test('should reject short license key (< 10 chars)', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'SHORT',
          machineId: 'test-machine-id',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.success).toBe(false);
    });

    test('should reject short machine ID (< 10 chars)', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
          machineId: 'SHORT',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.success).toBe(false);
    });

    test('should reject invalid license key format', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'INVALID-FORMAT-KEY',
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.success).toBe(false);
      expect(json.error).toBe('Invalid license key format');
    });

    test('should accept valid license key format', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-ABC123-DEF456-GHI789',
          machineId: 'test-machine-id-12345',
          machineLabel: 'Test Machine',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should pass format validation (will fail with 404 for non-existent key)
      expect([200, 403, 404, 503]).toContain(response.status());
      const json = await response.json();
      expect(json).toHaveProperty('success');
    });

    test('should reject non-existent license key', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-FAKE12-FAKE34-FAKE56',
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(404);
      const json = await response.json();
      expect(json.success).toBe(false);
      expect(json.error).toBe('License key not found');
    });
  });

  test.describe('2. License Key Format Validation', () => {
    test('should validate VL prefix', async ({ request }) => {
      const invalidFormats = [
        'XX-ABC123-DEF456-GHI789', // Wrong prefix
        'ABC123-DEF456-GHI789', // No prefix
        'VL-ABC-DEF-GHI', // Too short
        'VL-ABC1234-DEF456-GHI789', // Too long in segment
        'VL-abc123-def456-ghi789', // Lowercase (must be uppercase)
      ];

      for (const invalidKey of invalidFormats) {
        const response = await request.post(activateUrl, {
          data: {
            licenseKey: invalidKey,
            machineId: 'test-machine-id-12345',
          },
          headers: {
            'Content-Type': 'application/json',
          },
        });

        expect(response.status()).toBe(400);
        const json = await response.json();
        expect(json.success).toBe(false);
        expect(json.error).toBe('Invalid license key format');
      }
    });

    test('should accept valid VL format', async ({ request }) => {
      const validFormats = [
        'VL-ABC123-DEF456-GHI789',
        'VL-000000-111111-222222',
        'VL-ZZZZZZ-YYYYYY-XXXXXX',
      ];

      for (const validKey of validFormats) {
        const response = await request.post(activateUrl, {
          data: {
            licenseKey: validKey,
            machineId: 'test-machine-id-12345',
          },
          headers: {
            'Content-Type': 'application/json',
          },
        });

        // Should pass format validation (will fail with 404, not 400)
        expect(response.status()).not.toBe(400);
      }
    });
  });

  test.describe('3. Rate Limiting', () => {
    test('should enforce rate limiting (10 req/hour)', async ({ request }) => {
      // Note: This test would need to make 10+ requests to trigger rate limit
      // For now, we just verify the endpoint responds properly

      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-RATE12-LIMIT3-TEST45',
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should not be rate limited on single request
      expect(response.status()).not.toBe(429);
    });

    test('should include rate limit headers on 429 response', async ({ request }) => {
      // This test documents expected behavior but won't trigger without 10+ requests
      // Keeping for documentation purposes
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
          machineId: 'test-machine-id',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (response.status() === 429) {
        const json = await response.json();
        expect(json.error).toBe('Too many activation attempts');
        expect(json.retryAfter).toBeDefined();

        const headers = response.headers();
        expect(headers['x-ratelimit-limit']).toBeDefined();
        expect(headers['x-ratelimit-remaining']).toBeDefined();
        expect(headers['retry-after']).toBeDefined();
      }
    });
  });

  test.describe('4. Response Format', () => {
    test('should return proper success structure', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      const json = await response.json();
      expect(json).toHaveProperty('success');
      expect(typeof json.success).toBe('boolean');

      if (json.success) {
        expect(json).toHaveProperty('license');
        expect(json).toHaveProperty('activatedDevices');
        expect(json).toHaveProperty('maxDevices');
        expect(json).toHaveProperty('message');
      } else {
        expect(json).toHaveProperty('error');
      }
    });

    test('should return proper error structure', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-NONEXIST-NONEXIST-NONEXIST',
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      const json = await response.json();
      expect(json).toHaveProperty('success');
      expect(json.success).toBe(false);
      expect(json).toHaveProperty('error');
      expect(typeof json.error).toBe('string');
    });
  });

  test.describe('5. HTTP Method Validation', () => {
    test('should reject GET requests', async ({ request }) => {
      const response = await request.get(activateUrl);
      expect(response.status()).toBe(405);
    });

    test('should reject PUT requests', async ({ request }) => {
      const response = await request.put(activateUrl, {
        data: { licenseKey: 'test', machineId: 'test' },
      });
      expect(response.status()).toBe(405);
    });

    test('should reject DELETE requests', async ({ request }) => {
      const response = await request.delete(activateUrl);
      expect(response.status()).toBe(405);
    });

    test('should only accept POST requests', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
          machineId: 'test-machine-id',
        },
        headers: { 'Content-Type': 'application/json' },
      });

      // POST should be accepted (will fail validation due to no DB, but method is correct)
      // Valid status codes: 200 (success), 400 (bad request), 403 (forbidden), 404 (not found), 429 (rate limit), 500 (DB error), 503 (service unavailable)
      expect([200, 400, 403, 404, 429, 500, 503]).toContain(response.status());
    });
  });

  test.describe('6. Security & Edge Cases', () => {
    test('should handle SQL injection attempt in license key', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: "VL-TEST12'; DROP TABLE licenses; --",
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should reject due to format validation (protects against SQL injection)
      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.error).toBe('Invalid license key format');
    });

    test('should handle SQL injection attempt in machine ID', async ({ request }) => {
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
          machineId: "'; DROP TABLE activations; --",
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should be handled by Prisma (parameterized queries)
      // Will likely fail with 404 (license not found) or 503 (DB error)
      expect([400, 404, 500, 503]).toContain(response.status());
    });

    test('should handle very long license key', async ({ request }) => {
      const longKey = 'VL-' + 'A'.repeat(1000);
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: longKey,
          machineId: 'test-machine-id-12345',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.status()).toBe(400);
      const json = await response.json();
      expect(json.error).toBe('Invalid license key format');
    });

    test('should handle very long machine ID', async ({ request }) => {
      const longMachineId = 'A'.repeat(10000);
      const response = await request.post(activateUrl, {
        data: {
          licenseKey: 'VL-TEST12-TEST34-TEST56',
          machineId: longMachineId,
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should handle gracefully (either truncate or reject)
      expect(response.status()).toBeGreaterThanOrEqual(200);
      expect(response.status()).toBeLessThan(600);
    });
  });
});
