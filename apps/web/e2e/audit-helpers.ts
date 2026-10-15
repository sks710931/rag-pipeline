import { expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import * as fs from 'fs';
import * as path from 'path';

export interface A11yAuditOptions {
  include?: string[];
  exclude?: string[];
  disabledRules?: string[];
}

/**
 * Executes a full WCAG 2.1 & 2.2 AA accessibility audit using axe-core
 */
export async function runAxeAudit(page: Page, options: A11yAuditOptions = {}) {
  let builder = new AxeBuilder({ page }).withTags([
    'wcag2a',
    'wcag2aa',
    'wcag21a',
    'wcag21aa',
    'wcag22aa',
  ]);

  if (options.include && options.include.length > 0) {
    for (const selector of options.include) {
      builder = builder.include(selector);
    }
  }

  if (options.exclude && options.exclude.length > 0) {
    for (const selector of options.exclude) {
      builder = builder.exclude(selector);
    }
  }

  if (options.disabledRules && options.disabledRules.length > 0) {
    builder = builder.disableRules(options.disabledRules);
  }

  const results = await builder.analyze();
  return results;
}

/**
 * Asserts zero WCAG accessibility violations on the given page
 */
export async function expectNoA11yViolations(
  page: Page,
  options: A11yAuditOptions = {}
) {
  const results = await runAxeAudit(page, options);
  expect(
    results.violations,
    `Found ${results.violations.length} accessibility violations:\n${JSON.stringify(
      results.violations.map((v) => ({
        id: v.id,
        impact: v.impact,
        description: v.description,
        nodes: v.nodes.map((n) => n.html),
      })),
      null,
      2
    )}`
  ).toEqual([]);
}

/**
 * Captures responsive audit screenshots to apps/web/e2e/screenshots/
 */
export async function captureResponsiveScreenshot(
  page: Page,
  name: string,
  viewportName: string
) {
  const screenshotsDir = path.resolve(__dirname, 'screenshots', viewportName);
  if (!fs.existsSync(screenshotsDir)) {
    fs.mkdirSync(screenshotsDir, { recursive: true });
  }

  const screenshotPath = path.join(screenshotsDir, `${name}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });
}

/**
 * Tests focus visibility and ring presence on the currently focused element
 */
export async function expectVisibleFocusRing(page: Page, selector: string) {
  const element = page.locator(selector);
  await element.focus();

  const outline = await element.evaluate((el) => {
    const computed = window.getComputedStyle(el);
    return {
      outlineStyle: computed.outlineStyle,
      outlineWidth: computed.outlineWidth,
      boxShadow: computed.boxShadow,
    };
  });

  const hasRing =
    outline.outlineStyle !== 'none' ||
    outline.boxShadow.includes('rgb') ||
    outline.boxShadow.includes('var(');
  expect(hasRing).toBe(true);
}
