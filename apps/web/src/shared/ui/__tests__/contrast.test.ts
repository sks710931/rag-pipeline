import { describe, expect, it } from 'vitest';

/**
 * Standard W3C / WCAG 2.1 & 2.2 Relative Luminance and Contrast Ratio calculation
 * Spec: https://www.w3.org/WAI/GL/wiki/Relative_luminance
 * Spec: https://www.w3.org/WAI/GL/wiki/Contrast_ratio
 */
export function hexToRgb(hex: string): [number, number, number] {
  const cleanHex = hex.replace('#', '');
  const r = parseInt(cleanHex.substring(0, 2), 16);
  const g = parseInt(cleanHex.substring(2, 4), 16);
  const b = parseInt(cleanHex.substring(4, 6), 16);
  return [r, g, b];
}

function toLinear(c: number): number {
  const v = c / 255;
  return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
}

export function relativeLuminance([r, g, b]: [number, number, number]): number {
  const sR = toLinear(r);
  const sG = toLinear(g);
  const sB = toLinear(b);
  return 0.2126 * sR + 0.7152 * sG + 0.0722 * sB;
}

export function calculateContrastRatio(fgHex: string, bgHex: string): number {
  const l1 = relativeLuminance(hexToRgb(fgHex));
  const l2 = relativeLuminance(hexToRgb(bgHex));
  const lighter = Math.max(l1, l2);
  const darker = Math.min(l1, l2);
  const ratio = (lighter + 0.05) / (darker + 0.05);
  return Math.round(ratio * 10) / 10;
}

describe('Design System Color Contrast Compliance (WCAG 2.2 AA)', () => {
  const WHITE = '#ffffff';

  describe('Semantic Tokens vs Designated Backgrounds (SC 1.4.3: Contrast Minimum >= 4.5:1)', () => {
    it('Neutral: neutral-900 on white and neutral-50', () => {
      const neutral900 = '#0f172a';
      const neutral50 = '#f8fafc';

      expect(calculateContrastRatio(neutral900, WHITE)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(
        calculateContrastRatio(neutral900, neutral50)
      ).toBeGreaterThanOrEqual(4.5);
      expect(calculateContrastRatio(neutral900, WHITE)).toBe(17.9);
    });

    it('Neutral: secondary text (neutral-600 and neutral-700) on white', () => {
      const neutral700 = '#334155';
      const neutral600 = '#475569';

      expect(calculateContrastRatio(neutral700, WHITE)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(calculateContrastRatio(neutral600, WHITE)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(calculateContrastRatio(neutral600, WHITE)).toBe(7.6);
    });

    it('Brand: White text on brand-600 primary buttons', () => {
      const brand600 = '#2563eb';
      const ratio = calculateContrastRatio(WHITE, brand600);

      expect(ratio).toBeGreaterThanOrEqual(4.5);
      expect(ratio).toBe(5.2);
    });

    it('Success: success-800 and success-900 on success-50 container', () => {
      const success800 = '#166534';
      const success900 = '#14532d';
      const success50 = '#f0fdf4';

      expect(
        calculateContrastRatio(success800, success50)
      ).toBeGreaterThanOrEqual(4.5);
      expect(
        calculateContrastRatio(success900, success50)
      ).toBeGreaterThanOrEqual(4.5);
      expect(calculateContrastRatio(success800, success50)).toBe(6.8);
    });

    it('Warning: warning-900 and warning-950 on warning-50 container', () => {
      const warning900 = '#78350f';
      const warning950 = '#451a03';
      const warning50 = '#fffbeb';

      expect(
        calculateContrastRatio(warning900, warning50)
      ).toBeGreaterThanOrEqual(4.5);
      expect(
        calculateContrastRatio(warning950, warning50)
      ).toBeGreaterThanOrEqual(4.5);
      expect(calculateContrastRatio(warning900, warning50)).toBe(8.7);
    });

    it('Error: error-800 and error-900 on error-50 container', () => {
      const error800 = '#991b1b';
      const error900 = '#7f1d1d';
      const error50 = '#fef2f2';

      expect(calculateContrastRatio(error800, error50)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(calculateContrastRatio(error900, error50)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(calculateContrastRatio(error800, error50)).toBe(7.6);
    });

    it('Error Button: White text on error-600 danger buttons', () => {
      const error600 = '#dc2626';
      const ratio = calculateContrastRatio(WHITE, error600);

      expect(ratio).toBeGreaterThanOrEqual(4.5);
      expect(ratio).toBe(4.8);
    });

    it('Info: info-800 and info-900 on info-50 container', () => {
      const info800 = '#075985';
      const info900 = '#0c4a6e';
      const info50 = '#f0f9ff';

      expect(calculateContrastRatio(info800, info50)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(calculateContrastRatio(info900, info50)).toBeGreaterThanOrEqual(
        4.5
      );
      expect(calculateContrastRatio(info800, info50)).toBe(7.1);
    });
  });

  describe('Non-Text Contrast (SC 1.4.11: Non-text Contrast >= 3.0:1)', () => {
    it('Focus Ring: focus-ring token (#2563eb) against white and neutral-50', () => {
      const focusRing = '#2563eb';
      const neutral50 = '#f8fafc';

      const whiteRatio = calculateContrastRatio(focusRing, WHITE);
      const neutral50Ratio = calculateContrastRatio(focusRing, neutral50);

      expect(whiteRatio).toBeGreaterThanOrEqual(3.0);
      expect(neutral50Ratio).toBeGreaterThanOrEqual(3.0);
      expect(whiteRatio).toBe(5.2);
      expect(neutral50Ratio).toBe(4.9);
    });

    it('Disallowed Tokens: neutral-400 is strictly under 4.5:1 on white and must not be used for text', () => {
      const neutral400 = '#94a3b8';
      const ratio = calculateContrastRatio(neutral400, WHITE);
      // Confirms why design-system.md prohibits neutral-400 for text
      expect(ratio).toBeLessThan(4.5);
    });
  });
});
