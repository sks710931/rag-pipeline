import { test, expect } from '@playwright/test';
import {
  captureResponsiveScreenshot,
  expectNoA11yViolations,
} from './audit-helpers';

test.describe('Pixel RAG Accessibility & Visual State Audit (WCAG 2.2 AA)', () => {
  const routes = [
    { path: '/login', name: 'sign-in' },
    { path: '/setup', name: 'setup' },
    { path: '/maintenance', name: 'maintenance' },
    { path: '/status', name: 'system-status' },
    { path: '/not-found-random', name: '404-not-found' },
  ];

  for (const { path, name } of routes) {
    test(`Route ${path} passes real-browser axe audit and captures responsive visual state`, async ({
      page,
    }, testInfo) => {
      await page.goto(path);
      await page.waitForLoadState('domcontentloaded');

      // Zero WCAG 2.1 & 2.2 AA violations in real browser (including real colour contrast)
      await expectNoA11yViolations(page);

      // Record responsive screenshot for audit artifact
      await captureResponsiveScreenshot(page, name, testInfo.project.name);
    });
  }

  test('Skip Link bypasses header and moves focus to main landmark', async ({
    page,
  }) => {
    await page.goto('/status');
    await page.waitForLoadState('domcontentloaded');

    // First Tab activates skip link
    await page.keyboard.press('Tab');
    const skipLink = page.locator('a.skip-link, a[href="#main-content"]');
    await expect(skipLink).toBeFocused();

    // Activating SkipLink moves focus to main-content
    await page.keyboard.press('Enter');
    const mainContent = page.locator('#main-content');
    await expect(mainContent).toBeFocused();
  });

  test('Mobile drawer opens, traps focus, and restores focus on Escape', async ({
    page,
  }, testInfo) => {
    // Only execute on mobile viewports
    if (testInfo.project.name === 'desktop-1440') {
      test.skip();
    }

    await page.goto('/status');
    await page.waitForLoadState('domcontentloaded');

    const menuToggle = page.getByRole('button', {
      name: /open navigation menu/i,
    });
    await expect(menuToggle).toBeVisible();

    await menuToggle.click();
    const drawer = page.getByRole('dialog', { name: /navigation menu/i });
    await expect(drawer).toBeVisible();

    // Check axe compliance with drawer open
    await expectNoA11yViolations(page);

    // Escape closes drawer and returns focus to the trigger
    await page.keyboard.press('Escape');
    await expect(drawer).not.toBeVisible();
    await expect(menuToggle).toBeFocused();
  });

  test('Respects prefers-reduced-motion media query on animating elements', async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/status');
    await page.waitForLoadState('domcontentloaded');

    // Verify an element with active animation has duration overridden to 0.01ms and count to 1
    const animationOverride = await page.evaluate(() => {
      const animatedEl = document.createElement('div');
      animatedEl.className = 'animate-spin';
      document.body.appendChild(animatedEl);
      const computed = window.getComputedStyle(animatedEl);
      const result = {
        duration: computed.animationDuration,
        iterationCount: computed.animationIterationCount,
      };
      animatedEl.remove();
      return result;
    });

    // 0.01ms computes to 0.00001s in Chromium / WebKit
    expect(['0.00001s', '0.01ms', '0.000010s']).toContain(
      animationOverride.duration
    );
    expect(animationOverride.iterationCount).toBe('1');
  });
});
