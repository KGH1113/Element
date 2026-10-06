import { describe, expect, it } from 'vitest';
import type { KeyPositions } from '@src/types/key/keys';
import { buildKeyLimiterSyncRequest } from './keyLimiterSnapshot';

function positions(
  values: Array<{ keyLimiterExcluded?: boolean; className?: string }>,
): KeyPositions[string] {
  return values as KeyPositions[string];
}

describe('buildKeyLimiterSyncRequest', () => {
  it('filters ghost keys, keeps missing positions, and preserves stable order', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'hand-main', foot: 'foot-main' },
        tabs: [
          { id: 'hand-main', name: 'Hands', viewerKind: 'hand' },
          { id: 'foot-main', name: 'Feet', viewerKind: 'foot' },
        ],
        keyMappings: {
          'hand-main': ['KeyA', 'KeyS', 'KeyA'],
          'foot-main': ['KeyD', 'KeyS', 'KeyF'],
        },
        positions: {
          'hand-main': positions([
            { keyLimiterExcluded: true, className: 'wide selected' },
            { className: 'wide' },
            { className: 'wide' },
          ]),
          'foot-main': positions([
            { className: 'wide' },
            { className: 'wide' },
          ]),
        },
      },
      'session-1',
      7,
      '0.1.0',
    );

    expect(request).toEqual({
      schemaVersion: 2,
      source: 'impl-dm-note',
      clientVersion: '0.1.0',
      sessionId: 'session-1',
      revision: 7,
      enabled: true,
      profile: {
        id: 'dual:hand-main:foot-main',
        name: 'Hands + Feet',
      },
      keys: ['KeyS', 'KeyA', 'KeyD', 'KeyF'],
      excludedGhostKeys: [],
    });
  });

  it('reports keys that only have ghost instances across both viewers', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'hand', foot: 'foot' },
        tabs: [
          { id: 'hand', name: 'Hand', viewerKind: 'hand' },
          { id: 'foot', name: 'Foot', viewerKind: 'foot' },
        ],
        keyMappings: {
          hand: ['KeyA', 'KeyS'],
          foot: ['KeyA', 'KeyD'],
        },
        positions: {
          hand: positions([
            { keyLimiterExcluded: true },
            { className: 'ghost' },
          ]),
          foot: positions([
            { keyLimiterExcluded: true, className: 'compact' },
            {},
          ]),
        },
      },
      'session-2',
      1,
      '0.1.0',
    );

    expect(request.enabled).toBe(true);
    expect(request.profile).toEqual({
      id: 'dual:hand:foot',
      name: 'Hand + Foot',
    });
    expect(request.keys).toEqual(['KeyS', 'KeyD']);
    expect(request.excludedGhostKeys).toEqual(['KeyA']);
  });

  it('sends a disabled snapshot for an empty effective profile', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'hand', foot: 'foot' },
        tabs: [],
        keyMappings: { hand: ['KeyA'], foot: [] },
        positions: {
          hand: positions([{ keyLimiterExcluded: true }]),
          foot: positions([]),
        },
      },
      'session-3',
      2,
      '0.1.0',
    );

    expect(request.enabled).toBe(false);
    expect(request.keys).toEqual([]);
    expect(request.excludedGhostKeys).toEqual(['KeyA']);
  });

  it('does not treat CSS ghost classes as limiter metadata', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'hand', foot: 'foot' },
        tabs: [],
        keyMappings: { hand: ['KeyA'], foot: [] },
        positions: {
          hand: positions([{ className: 'ghost' }]),
          foot: positions([]),
        },
      },
      'session-4',
      3,
      '0.1.0',
    );

    expect(request.enabled).toBe(true);
    expect(request.keys).toEqual(['KeyA']);
    expect(request.excludedGhostKeys).toEqual([]);
  });

  it('keeps the limiter enabled when only one viewer has effective keys', () => {
    const request = buildKeyLimiterSyncRequest(
      {
        selectedViewerTabs: { hand: 'empty-hand', foot: 'foot' },
        tabs: [],
        keyMappings: { 'empty-hand': [], foot: ['F8'] },
        positions: { 'empty-hand': positions([]), foot: positions([{}]) },
      },
      'session-5',
      4,
      '0.1.0',
    );

    expect(request.enabled).toBe(true);
    expect(request.keys).toEqual(['F8']);
  });
});
