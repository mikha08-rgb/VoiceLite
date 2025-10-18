import { test, expect } from '@playwright/test';

test.describe('VoiceLite Homepage - Freemium Model', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:3000');
  });

  test('should load homepage successfully', async ({ page }) => {
    await expect(page).toHaveTitle(/VoiceLite/);
    await expect(page.locator('h1')).toContainText('Stop Typing');
  });

  test('should display hero section with correct pricing', async ({ page }) => {
    // Check hero text mentions $20
    await expect(page.getByText('$20 one-time')).toBeVisible();
    await expect(page.getByText('No subscription')).toBeVisible();

    // Check primary CTA (first occurrence in hero)
    const heroCTA = page.getByRole('link', { name: /Get VoiceLite Pro - \$20/i }).first();
    await expect(heroCTA).toBeVisible();
    await expect(heroCTA).toHaveAttribute('href', '#pricing');
  });

  test('should display both pricing tiers (Free + Pro)', async ({ page }) => {
    // Scroll to pricing section
    await page.locator('#pricing').scrollIntoViewIfNeeded();

    // Check Free tier exists
    await expect(page.getByRole('heading', { name: 'Free', exact: true })).toBeVisible();
    await expect(page.getByText('$0')).toBeVisible();
    await expect(page.getByText('Tiny model (80-85% accuracy)')).toBeVisible();

    // Check Pro tier exists
    await expect(page.getByRole('heading', { name: 'Pro', exact: true })).toBeVisible();
    await expect(page.getByText('$20 USD')).toBeVisible();
    await expect(page.getByText('All 4 Pro models')).toBeVisible();
    await expect(page.getByText('90-98% accuracy')).toBeVisible();

    // Check RECOMMENDED badge on Pro tier
    await expect(page.getByText('RECOMMENDED', { exact: true })).toBeVisible();
  });

  test('should have correct download links', async ({ page }) => {
    await page.locator('#pricing').scrollIntoViewIfNeeded();

    // Free tier download button (GitHub)
    const freeDownload = page.getByRole('link', { name: 'Download Free' });
    await expect(freeDownload).toBeVisible();
    await expect(freeDownload).toHaveAttribute('href', /github\.com.*releases\/latest/);

    // Pro tier purchase button (now a button, not a link)
    const proButton = page.getByRole('button', { name: 'Get Pro - $20' });
    await expect(proButton).toBeVisible();
    // Button is verified by role, no need to check type attribute
  });

  test('should display 30-day money-back guarantee on Pro tier', async ({ page }) => {
    await page.locator('#pricing').scrollIntoViewIfNeeded();
    await expect(page.getByText('30-Day Money-Back Guarantee').first()).toBeVisible();
    await expect(page.getByText('Secure checkout via Stripe')).toBeVisible();
  });

  test('desktop navigation should work', async ({ page }) => {
    // Desktop nav only visible on larger screens
    await page.setViewportSize({ width: 1280, height: 720 });

    // Check navigation links (use first() for duplicates)
    await expect(page.getByRole('link', { name: 'Features' }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'Pricing' }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'FAQ' }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'GitHub' }).first()).toBeVisible();

    // Check "Get Pro" button in nav
    const navProButton = page.locator('nav').getByRole('link', { name: 'Get Pro' }).first();
    await expect(navProButton).toBeVisible();
    await expect(navProButton).toHaveAttribute('href', '#pricing');
  });

  test('mobile navigation should work', async ({ page }) => {
    // Mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    // Hamburger menu should be visible
    const menuButton = page.getByRole('button', { name: /toggle menu/i });
    await expect(menuButton).toBeVisible();

    // Mobile-specific Features link should not be visible initially
    // We check for the mobile menu container with specific class
    const mobileMenu = page.locator('.md\\:hidden > .container');
    await expect(mobileMenu).not.toBeVisible();

    // Click hamburger to open menu
    await menuButton.click();

    // Mobile menu should now be visible
    await expect(mobileMenu).toBeVisible();

    // Check mobile "Get Pro" button is visible
    const mobileProButton = mobileMenu.getByRole('link', { name: 'Get Pro' });
    await expect(mobileProButton).toBeVisible();

    // Click Features link in mobile menu
    await mobileMenu.getByRole('link', { name: 'Features' }).click();

    // Wait for navigation and menu close
    await page.waitForTimeout(300);

    // Mobile menu should be hidden again
    await expect(mobileMenu).not.toBeVisible();
  });

  test('FAQ accordion should work', async ({ page }) => {
    // Scroll to FAQ
    await page.locator('#faq').scrollIntoViewIfNeeded();

    // First FAQ question
    const firstQuestion = page.getByText('Does VoiceLite require an internet connection?');
    await expect(firstQuestion).toBeVisible();

    // Answer should be hidden initially
    await expect(page.getByText('No. VoiceLite runs 100% offline')).not.toBeVisible();

    // Click question to expand
    await firstQuestion.click();

    // Answer should now be visible
    await expect(page.getByText('No. VoiceLite runs 100% offline')).toBeVisible();

    // Click again to collapse
    await firstQuestion.click();

    // Answer should be hidden again
    await expect(page.getByText('No. VoiceLite runs 100% offline')).not.toBeVisible();
  });

  test('should display all 3 feature cards', async ({ page }) => {
    await expect(page.getByText('Privacy First')).toBeVisible();
    await expect(page.getByText('Lightning Fast')).toBeVisible();
    await expect(page.getByText('Works Anywhere')).toBeVisible();
  });

  test('should display founder story', async ({ page }) => {
    await expect(page.getByText('I got tired of slow, cloud-based dictation tools')).toBeVisible();
  });

  test('should display model comparison table', async ({ page }) => {
    // Check table exists with all 5 models (use first() for duplicates)
    await expect(page.getByText('Tiny').first()).toBeVisible();
    await expect(page.getByText('Swift').first()).toBeVisible();
    await expect(page.getByText('Pro ⭐')).toBeVisible();
    await expect(page.getByText('Elite').first()).toBeVisible();
    await expect(page.getByText('Ultra').first()).toBeVisible();
  });

  test('final CTA section should be correct', async ({ page }) => {
    // Scroll to bottom
    await page.locator('footer').scrollIntoViewIfNeeded();

    // Check final CTA
    await expect(page.getByText('Ready to stop typing?')).toBeVisible();

    // Final CTA button
    const finalCTA = page.getByRole('link', { name: /Get VoiceLite Pro - \$20/i }).last();
    await expect(finalCTA).toBeVisible();
    await expect(finalCTA).toHaveAttribute('href', '#pricing');

    // Check footer text mentions free tier
    await expect(page.getByText('Try free tier first')).toBeVisible();
  });

  test('should have working footer links', async ({ page }) => {
    await page.locator('footer').scrollIntoViewIfNeeded();

    // Product links (check for Download link in footer - use exact match to avoid "Download Free")
    await expect(page.locator('footer').getByRole('link', { name: 'Download', exact: true })).toBeVisible();

    // Resources
    const githubLinks = page.getByRole('link', { name: 'GitHub' });
    await expect(githubLinks.first()).toBeVisible();

    // Check copyright
    await expect(page.locator('footer').getByText('VoiceLite').first()).toBeVisible();
  });

  test('should be responsive', async ({ page }) => {
    // Test different viewport sizes
    const viewports = [
      { width: 375, height: 667, name: 'Mobile' },
      { width: 768, height: 1024, name: 'Tablet' },
      { width: 1280, height: 720, name: 'Desktop' },
    ];

    for (const viewport of viewports) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });

      // Page should load without layout issues
      await expect(page.locator('h1')).toBeVisible();
      await expect(page.locator('#pricing')).toBeVisible();
      await expect(page.locator('#faq')).toBeVisible();
    }
  });
});
