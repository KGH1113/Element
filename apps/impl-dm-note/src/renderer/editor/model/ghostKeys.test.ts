import { describe, expect, it } from 'vitest';
import type { KeyPosition } from '@src/types/key/keys';
import { buildKeyLimiterSyncRequest } from '@src/renderer/ipc/keyLimiterSnapshot';
import { isKeyMappedToViewer } from '@src/renderer/windows/overlay/keyStateRouting';
import {
  createDefaultGhostSettings,
  expandGhostKeys,
  ghostKeyLabels,
  mergeGhostSettings,
  toTransparentColor,
} from './ghostKeys';

function owner(overrides: Partial<KeyPosition> = {}): KeyPosition {
  return {
    dx: 493,
    dy: 90,
    width: 65,
    height: 65,
    count: 0,
    noteColor: '#BA712B',
    noteOpacity: 80,
    backgroundColor: 'rgba(186, 113, 43, 0.5)',
    activeBackgroundColor: 'rgba(186, 113, 43, 0.9)',
    fontColor: '#FFFFFF',
    activeFontColor: '#FFFFFF',
    zIndex: 6,
    ...overrides,
  } as KeyPosition;
}

function withGhost(key: string | null, enabled = true): KeyPosition {
  const base = owner();
  return {
    ...base,
    ghost: { enabled, key, settings: createDefaultGhostSettings(base) },
  };
}

describe('createDefaultGhostSettings', () => {
  it('hides the ghost while idle and keeps the owner pressed look', () => {
    const settings = createDefaultGhostSettings(owner());

    expect(settings.idleTransparent).toBe(true);
    expect(settings.backgroundColor).toBe('rgba(186, 113, 43, 0)');
    expect(settings.fontColor).toBe('rgba(255, 255, 255, 0)');
    expect(settings.activeBackgroundColor).toBe('rgba(186, 113, 43, 0.9)');
    expect(settings.activeFontColor).toBe('#FFFFFF');
  });

  it('draws an outline-only note in the owner note color', () => {
    const settings = createDefaultGhostSettings(owner());

    expect(settings.noteColor).toBe('#BA712B');
    expect(settings.noteOpacity).toBe(0);
    expect(settings.noteBorderColor).toBe('#BA712B');
    expect(settings.noteBorderWidth).toBe(3);
    expect(settings.counter.enabled).toBe(false);
  });

  it('never nests a ghost', () => {
    const settings = createDefaultGhostSettings(withGhost('R'));

    expect('ghost' in settings).toBe(false);
  });
});

describe('expandGhostKeys', () => {
  it('returns the same arrays when no ghost is bound', () => {
    const keys = ['E'];
    const positions = [owner()];

    const expanded = expandGhostKeys(keys, positions);

    expect(expanded.keys).toBe(keys);
    expect(expanded.positions).toBe(positions);
  });

  it('appends a ghost that follows the owner geometry and label', () => {
    const moved = { ...withGhost('R'), dx: 600, width: 80 };

    const expanded = expandGhostKeys(['E'], [moved], () => 'E');

    expect(expanded.keys).toEqual(['E', 'R']);
    expect(expanded.positions[1]).toMatchObject({
      dx: 600,
      width: 80,
      zIndex: 6,
      displayText: 'E',
      keyLimiterExcluded: true,
    });
  });

  it('keeps ghosts aligned when a key has no stored position', () => {
    const expanded = expandGhostKeys(['E', 'F'], [withGhost('R')]);

    expect(expanded.keys).toEqual(['E', 'F', 'R']);
    expect(expanded.positions).toHaveLength(3);
    expect(expanded.positions[1]).toBeUndefined();
    expect(expanded.positions[2]).toMatchObject({ dx: 493 });
  });

  it('skips disabled, unbound and self-bound ghosts', () => {
    const keys = ['E', 'F', 'G'];
    const positions = [withGhost('R', false), withGhost(null), withGhost('G')];

    expect(expandGhostKeys(keys, positions).keys).toBe(keys);
    expect(ghostKeyLabels(keys, positions)).toEqual([]);
  });
});

describe('mergeGhostSettings', () => {
  it('keeps the binding and earlier edits', () => {
    const ghost = mergeGhostSettings(withGhost('R'), { fontSize: 30 });
    const next = mergeGhostSettings({ ...owner(), ghost }, { noteOpacity: 40 });

    expect(next.key).toBe('R');
    expect(next.settings.fontSize).toBe(30);
    expect(next.settings.noteOpacity).toBe(40);
  });
});

describe('ghost keys across viewers and the limiter', () => {
  it('routes ghost input to the viewer that owns it', () => {
    const keyMappings = { hand: ['E'] };
    const positions = { hand: [withGhost('R')] };

    expect(isKeyMappedToViewer('R', 'hand', keyMappings, positions)).toBe(true);
    expect(isKeyMappedToViewer('R', 'hand', keyMappings)).toBe(false);
  });

  it('keeps ghost keys out of the limited keys', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'hand', foot: 'foot' },
        tabs: [],
        keyMappings: { hand: ['E'], foot: [] },
        positions: { hand: [withGhost('R')], foot: [] },
      },
      'session',
      1,
      '0.1.0',
    );

    expect(request.keys).toEqual(['E']);
    expect(request.excludedGhostKeys).toEqual(['R']);
  });

  it('turns the limiter off when the toggle is off', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'hand', foot: 'foot' },
        tabs: [],
        keyMappings: { hand: ['E'], foot: [] },
        positions: { hand: [owner()], foot: [] },
        keyLimiterEnabled: false,
      },
      'session',
      1,
      '0.1.0',
    );

    expect(request.enabled).toBe(false);
  });
});

describe('toTransparentColor', () => {
  it('keeps the hue of hex and rgb colors', () => {
    expect(toTransparentColor('#2E2E2F')).toBe('rgba(46, 46, 47, 0)');
    expect(toTransparentColor('rgb(1, 2, 3)')).toBe('rgba(1, 2, 3, 0)');
    expect(toTransparentColor(undefined)).toBe('rgba(0, 0, 0, 0)');
  });
});
