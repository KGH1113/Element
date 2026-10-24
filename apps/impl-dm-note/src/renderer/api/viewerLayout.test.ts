import { afterEach, describe, expect, it } from 'vitest';
import {
  DEFAULT_NOTE_SETTINGS,
  FALLBACK_POSITION,
} from '@constants/overlayDefaults';
import { computeLayout } from '@hooks/shared/useLayoutComputation';
import { publishViewerLayout } from './viewerLayout';

afterEach(() => document.body.replaceChildren());

describe('embedded viewer layout', () => {
  it('publishes the full layout when switching between 2, 4 and 8 keys', () => {
    const root = document.createElement('div');
    root.dataset.dmnViewerSurface = '';
    document.body.append(root);

    for (const count of [2, 4, 8, 2]) {
      const layout = computeLayout({
        currentKeys: Array.from({ length: count }, (_, i) => String(i)),
        currentPositions: Array.from({ length: count }, (_, i) => ({
          ...FALLBACK_POSITION,
          dx: i * 38,
          dy: 0,
          width: 35,
          height: 35,
        })),
        currentStatPositions: [],
        currentGraphPositions: [],
        currentKnobPositions: [],
        trackHeight: 300,
        noteSettings: { ...DEFAULT_NOTE_SETTINGS, fadePosition: 'auto' },
        overlayPadding: 30,
      });
      const bounds = layout.bounds!;
      publishViewerLayout(
        bounds.maxX - bounds.minX + 60,
        bounds.maxY - bounds.minY + 360,
        30,
      );
      expect(root.dataset.dmnViewerWidth).toBe(String(count * 38 + 57));
      expect(root.dataset.dmnViewerHeight).toBe('395');
      expect(root.dataset.dmnViewerPadding).toBe('30');
    }
  });

  it('does not notify the host again for unchanged layout', () => {
    const root = document.createElement('div');
    root.dataset.dmnViewerSurface = '';
    document.body.append(root);
    publishViewerLayout(209, 395, 30);
    const observer = new MutationObserver(() => {});
    observer.observe(root, { attributes: true });

    publishViewerLayout(209, 395, 30);

    expect(observer.takeRecords()).toHaveLength(0);
    observer.disconnect();
  });

  it('rounds fractional bounds outward and handles an empty viewer', () => {
    const root = document.createElement('div');
    root.dataset.dmnViewerSurface = '';
    document.body.append(root);
    publishViewerLayout(209.25, 395.5, 30);
    expect(root.dataset.dmnViewerWidth).toBe('210');
    expect(root.dataset.dmnViewerHeight).toBe('396');

    publishViewerLayout(1, 1, 0);
    expect(root.dataset.dmnViewerWidth).toBe('1');
    expect(root.dataset.dmnViewerHeight).toBe('1');
    expect(root.dataset.dmnViewerPadding).toBe('0');
  });

  it('retains the last valid layout for invalid dimensions', () => {
    const root = document.createElement('div');
    root.dataset.dmnViewerSurface = '';
    document.body.append(root);
    publishViewerLayout(209, 395, 30);

    for (const size of [NaN, Infinity, 0, -1]) {
      publishViewerLayout(size, 395, 30);
      publishViewerLayout(209, size, 30);
    }
    publishViewerLayout(209, 395, -1);

    expect(root.dataset.dmnViewerWidth).toBe('209');
    expect(root.dataset.dmnViewerHeight).toBe('395');
    expect(root.dataset.dmnViewerPadding).toBe('30');
  });
});
