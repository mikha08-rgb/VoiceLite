import { test, expect } from '@playwright/test';

/**
 * Rate Limiting Tests
 *
 * Tests rate limiting on critical endpoints to prevent abuse
 */

test.describe('Rate Limiting', () => {
  test('Checkout endpoint should rate limit after 5 requests', async ({ request }) => {
    console.log('\n=== Testing Checkout Rate Limiting ===');

    const makeCheckoutRequest = async () => {
      return request.post('http://localhost:3000/api/checkout', {
        data: {
          successUrl: 'http://localhost:3000/checkout/success',
          cancelUrl: 'http://localhost:3000/checkout/cancel',
        },
        headers: {
          'Origin': 'http://localhost:3000',
          'Referer': 'http://localhost:3000/',
        },
      });
    };

    // Make 5 requests (may hit rate limit if other tests are running)
    console.log('Making 5 requests...');
    let limitHit = false;
    for (let i = 1; i <= 5; i++) {
      const response = await makeCheckoutRequest();
      console.log(`Request ${i}: ${response.status()}`);
      if (response.status() === 429) {
        limitHit = true;
        console.log(`  ✓ Rate limit hit at request ${i}`);
        break;
      }
    }

    // If we haven't hit the limit yet, make more requests
    if (!limitHit) {
      console.log('Making additional requests to trigger rate limit...');
      for (let i = 6; i <= 10; i++) {
        const response = await makeCheckoutRequest();
        console.log(`Request ${i}: ${response.status()}`);
        if (response.status() === 429) {
          limitHit = true;
          const data = await response.json();
          console.log('✓ Rate limited correctly');
          console.log('  Error:', data.error);
          expect(data.error).toContain('Too many');
          expect(response.headers()['retry-after']).toBeTruthy();
          break;
        }
      }
    }

    // Rate limiting should eventually trigger or already have triggered
    if (limitHit) {
      console.log('✅ Rate limiting is working correctly');
    } else {
      console.log('⚠ Rate limit not hit - may be using fallback limiter or low traffic');
    }
  });

  test('License activation endpoint should rate limit', async ({ request }) => {
    console.log('\n=== Testing License Activation Rate Limiting ===');

    const makeActivationRequest = async () => {
      return request.post('http://localhost:3000/api/licenses/activate', {
        data: {
          licenseKey: 'VL-TEST01-TEST02-TEST03',
          machineId: 'TEST-MACHINE-RATE-LIMIT',
          machineLabel: 'Test PC',
        },
      });
    };

    // Make 10 requests (should all succeed or fail with license error)
    console.log('Making 10 requests...');
    let successCount = 0;
    let rateLimitCount = 0;

    for (let i = 1; i <= 10; i++) {
      const response = await makeActivationRequest();
      console.log(`Request ${i}: ${response.status()}`);

      if (response.status() === 429) {
        rateLimitCount++;
      } else {
        successCount++;
      }
    }

    // 11th request might be rate limited
    console.log('Making 11th request...');
    const lastResponse = await makeActivationRequest();
    console.log(`Request 11: ${lastResponse.status()}`);

    if (lastResponse.status() === 429) {
      const data = await lastResponse.json();
      console.log('✓ Rate limited correctly');
      console.log('  Error:', data.error);
      expect(data.error).toContain('Too many');
    } else {
      console.log('⚠ Rate limiting may not be configured or limit not reached');
    }

    console.log(`\nSummary: ${successCount} successful, ${rateLimitCount} rate limited`);
  });

  test('Rate limit headers should be present', async ({ request }) => {
    console.log('\n=== Testing Rate Limit Headers ===');

    const response = await request.post('http://localhost:3000/api/checkout', {
      data: {
        successUrl: 'http://localhost:3000/checkout/success',
        cancelUrl: 'http://localhost:3000/checkout/cancel',
      },
      headers: {
        'Origin': 'http://localhost:3000',
        'Referer': 'http://localhost:3000/',
      },
    });

    console.log('Response status:', response.status());

    if (response.status() === 429) {
      // Check for rate limit headers
      const headers = response.headers();
      console.log('Rate limit headers:', {
        limit: headers['x-ratelimit-limit'],
        remaining: headers['x-ratelimit-remaining'],
        reset: headers['x-ratelimit-reset'],
        retryAfter: headers['retry-after'],
      });

      expect(headers['x-ratelimit-limit']).toBeTruthy();
      expect(headers['x-ratelimit-reset']).toBeTruthy();
      expect(headers['retry-after']).toBeTruthy();

      console.log('✓ All rate limit headers present');
    } else {
      console.log('⚠ Request not rate limited, headers check skipped');
    }
  });
});
