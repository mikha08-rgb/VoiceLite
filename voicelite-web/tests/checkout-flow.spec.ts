import { test, expect } from '@playwright/test';
import { prisma } from '../lib/prisma';

/**
 * VoiceLite Stripe Checkout Flow Test
 *
 * Tests the complete payment flow:
 * 1. Create checkout session
 * 2. Complete Stripe payment with test card
 * 3. Verify webhook processing
 * 4. Verify license generation
 * 5. Test license activation API
 *
 * IMPORTANT: Uses Stripe TEST mode keys
 */

test.describe('VoiceLite Checkout Flow', () => {
  const TEST_EMAIL = `test-${Date.now()}@example.com`;
  let licenseKey: string | null = null;
  let checkoutSessionId: string | null = null;

  test('should complete full checkout and activation flow', async ({ page, request }) => {
    // ====================================================================
    // STEP 1: Create Checkout Session via API
    // ====================================================================
    console.log('Step 1: Creating checkout session...');

    const checkoutResponse = await request.post('http://localhost:3000/api/checkout', {
      data: {
        successUrl: 'http://localhost:3000/checkout/success',
        cancelUrl: 'http://localhost:3000/checkout/cancel',
      },
      headers: {
        'Origin': 'http://localhost:3000',
        'Referer': 'http://localhost:3000/',
      },
    });

    expect(checkoutResponse.ok()).toBeTruthy();
    const checkoutData = await checkoutResponse.json();
    expect(checkoutData.url).toBeTruthy();
    console.log('✓ Checkout session created:', checkoutData.url);

    // Extract session ID from URL
    const urlMatch = checkoutData.url.match(/checkout\.stripe\.com\/.*\/pay\/(cs_[^#?]+)/);
    checkoutSessionId = urlMatch ? urlMatch[1] : null;
    console.log('  Session ID:', checkoutSessionId);

    // ====================================================================
    // STEP 2: Navigate to Stripe Checkout and Fill Payment Form
    // ====================================================================
    console.log('\nStep 2: Navigating to Stripe checkout...');
    await page.goto(checkoutData.url);

    // Wait for Stripe checkout to load
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000); // Give Stripe iframe time to render

    console.log('  Filling out Stripe checkout form...');

    // Fill email (Stripe test mode)
    try {
      const emailInput = page.locator('input[name="email"], input[type="email"]').first();
      await emailInput.waitFor({ state: 'visible', timeout: 10000 });
      await emailInput.fill(TEST_EMAIL);
      console.log('  ✓ Email filled:', TEST_EMAIL);
    } catch (error) {
      console.log('  ⚠ Email field not found or already filled');
    }

    // Fill card number (Stripe test card: 4242 4242 4242 4242)
    const cardNumberFrame = page.frameLocator('iframe[name*="cardNumber"]').first();
    const cardNumberInput = cardNumberFrame.locator('input[name="cardnumber"], input[placeholder*="Card number"]').first();
    await cardNumberInput.waitFor({ state: 'visible', timeout: 10000 });
    await cardNumberInput.fill('4242424242424242');
    console.log('  ✓ Card number filled');

    // Fill expiry date
    const expiryFrame = page.frameLocator('iframe[name*="cardExpiry"]').first();
    const expiryInput = expiryFrame.locator('input[name="exp-date"], input[placeholder*="MM"]').first();
    await expiryInput.fill('1225'); // 12/25
    console.log('  ✓ Expiry filled: 12/25');

    // Fill CVC
    const cvcFrame = page.frameLocator('iframe[name*="cardCvc"]').first();
    const cvcInput = cvcFrame.locator('input[name="cvc"], input[placeholder*="CVC"]').first();
    await cvcInput.fill('123');
    console.log('  ✓ CVC filled: 123');

    // Fill billing details if required
    try {
      const nameInput = page.locator('input[name="name"], input[id*="name"]').first();
      if (await nameInput.isVisible({ timeout: 2000 })) {
        await nameInput.fill('Test User');
        console.log('  ✓ Name filled');
      }
    } catch {
      console.log('  ⚠ Name field not required');
    }

    try {
      const zipInput = page.locator('input[name="postalCode"], input[name="zip"], input[placeholder*="ZIP"]').first();
      if (await zipInput.isVisible({ timeout: 2000 })) {
        await zipInput.fill('12345');
        console.log('  ✓ ZIP filled: 12345');
      }
    } catch {
      console.log('  ⚠ ZIP field not required');
    }

    // ====================================================================
    // STEP 3: Submit Payment
    // ====================================================================
    console.log('\nStep 3: Submitting payment...');

    const submitButton = page.locator('button[type="submit"]').first();
    await submitButton.click();
    console.log('  ✓ Payment submitted');

    // Wait for redirect to success page
    await page.waitForURL('**/checkout/success**', { timeout: 30000 });
    console.log('  ✓ Redirected to success page');

    // Verify success page content
    await expect(page.locator('h1, h2')).toContainText(/success|thank|complete/i);
    console.log('  ✓ Success message visible');

    // ====================================================================
    // STEP 4: Wait for Webhook Processing
    // ====================================================================
    console.log('\nStep 4: Waiting for webhook processing...');

    // Wait for webhook to process (Stripe sends webhook after payment)
    await page.waitForTimeout(5000); // Give webhook time to process

    // ====================================================================
    // STEP 5: Verify License was Generated in Database
    // ====================================================================
    console.log('\nStep 5: Verifying license generation...');

    const license = await prisma.license.findFirst({
      where: { email: TEST_EMAIL },
      orderBy: { createdAt: 'desc' },
    });

    expect(license).toBeTruthy();
    expect(license!.email).toBe(TEST_EMAIL);
    expect(license!.status).toBe('ACTIVE');
    expect(license!.licenseKey).toMatch(/^VL-[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{6}$/);
    licenseKey = license!.licenseKey;

    console.log('  ✓ License found in database');
    console.log('    License Key:', licenseKey);
    console.log('    Email:', license!.email);
    console.log('    Status:', license!.status);
    console.log('    Max Devices:', license!.maxDevices);
    console.log('    Type:', license!.type);

    // ====================================================================
    // STEP 6: Test License Activation API (Device 1)
    // ====================================================================
    console.log('\nStep 6: Testing license activation - Device 1...');

    const activation1 = await request.post('http://localhost:3000/api/licenses/activate', {
      data: {
        licenseKey: licenseKey,
        machineId: 'TEST-MACHINE-001',
        machineLabel: 'Test PC 1',
      },
    });

    expect(activation1.ok()).toBeTruthy();
    const activation1Data = await activation1.json();
    expect(activation1Data.success).toBe(true);
    expect(activation1Data.activatedDevices).toBe(1);
    expect(activation1Data.maxDevices).toBe(1);
    console.log('  ✓ Device 1 activated successfully');
    console.log('    Activated Devices:', activation1Data.activatedDevices);
    console.log('    Max Devices:', activation1Data.maxDevices);

    // ====================================================================
    // STEP 7: Test License Activation API (Device 1 Again - Should Succeed)
    // ====================================================================
    console.log('\nStep 7: Testing re-activation on same device...');

    const reactivation = await request.post('http://localhost:3000/api/licenses/activate', {
      data: {
        licenseKey: licenseKey,
        machineId: 'TEST-MACHINE-001',
        machineLabel: 'Test PC 1',
      },
    });

    expect(reactivation.ok()).toBeTruthy();
    const reactivationData = await reactivation.json();
    expect(reactivationData.success).toBe(true);
    expect(reactivationData.message).toContain('already activated');
    console.log('  ✓ Re-activation successful (idempotent)');

    // ====================================================================
    // STEP 8: Test License Activation API (Device 2 - Should Fail)
    // ====================================================================
    console.log('\nStep 8: Testing activation on 2nd device (should fail)...');

    const activation2 = await request.post('http://localhost:3000/api/licenses/activate', {
      data: {
        licenseKey: licenseKey,
        machineId: 'TEST-MACHINE-002',
        machineLabel: 'Test PC 2',
      },
    });

    expect(activation2.status()).toBe(403);
    const activation2Data = await activation2.json();
    expect(activation2Data.success).toBe(false);
    expect(activation2Data.error).toContain('already activated');
    expect(activation2Data.activatedDevices).toBe(1);
    expect(activation2Data.maxDevices).toBe(1);
    console.log('  ✓ Device limit enforced correctly');
    console.log('    Error:', activation2Data.error);

    // ====================================================================
    // STEP 9: Test License Validation API
    // ====================================================================
    console.log('\nStep 9: Testing license validation...');

    const validation = await request.post('http://localhost:3000/api/licenses/validate', {
      data: {
        licenseKey: licenseKey,
        machineId: 'TEST-MACHINE-001',
      },
    });

    expect(validation.ok()).toBeTruthy();
    const validationData = await validation.json();
    expect(validationData.valid).toBe(true);
    expect(validationData.status).toBe('ACTIVE');
    console.log('  ✓ License validation successful');

    // ====================================================================
    // STEP 10: Cleanup Test Data
    // ====================================================================
    console.log('\nStep 10: Cleaning up test data...');

    if (license) {
      // Delete activations first (foreign key constraint)
      await prisma.licenseActivation.deleteMany({
        where: { licenseId: license.id },
      });

      // Delete license
      await prisma.license.delete({
        where: { id: license.id },
      });

      console.log('  ✓ Test license and activations deleted');
    }

    // ====================================================================
    // TEST COMPLETE
    // ====================================================================
    console.log('\n✅ ALL TESTS PASSED!');
    console.log('='.repeat(70));
  });

  test('should handle invalid license key', async ({ request }) => {
    console.log('\nTesting invalid license key...');

    const response = await request.post('http://localhost:3000/api/licenses/activate', {
      data: {
        licenseKey: 'VL-INVALI-DINVAL-IDTEST',
        machineId: 'TEST-MACHINE-999',
        machineLabel: 'Test PC',
      },
    });

    expect(response.status()).toBe(404);
    const data = await response.json();
    expect(data.success).toBe(false);
    expect(data.error).toContain('not found');
    console.log('  ✓ Invalid license key rejected correctly');
  });

  test('should handle malformed license key', async ({ request }) => {
    console.log('\nTesting malformed license key...');

    const response = await request.post('http://localhost:3000/api/licenses/activate', {
      data: {
        licenseKey: 'invalid-format',
        machineId: 'TEST-MACHINE-999',
        machineLabel: 'Test PC',
      },
    });

    expect(response.status()).toBe(400);
    const data = await response.json();
    expect(data.success).toBe(false);
    expect(data.error).toContain('Invalid license key format');
    console.log('  ✓ Malformed license key rejected correctly');
  });
});
