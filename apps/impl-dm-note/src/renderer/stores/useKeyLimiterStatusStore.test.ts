import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { KeyLimiterSyncRequest } from '@src/renderer/ipc/keyLimiterSnapshot';
import type { KeyLimiterSyncResponse } from '@src/renderer/ipc/keyLimiterIpcCoordinator';
import { useKeyLimiterStatusStore } from './useKeyLimiterStatusStore';

function request(revision: number, enabled = true): KeyLimiterSyncRequest {
  return {
    schemaVersion: 2,
    source: 'impl-dm-note',
    clientVersion: '0.1.0',
    sessionId: 'session',
    revision,
    enabled,
    profile: { id: '4key', name: 'Four keys' },
    keys: enabled ? ['KeyA'] : [],
    excludedGhostKeys: enabled ? [] : ['KeyA'],
  };
}

function response(
  revision: number,
  overrides: Partial<KeyLimiterSyncResponse> = {},
): KeyLimiterSyncResponse {
  return {
    ok: true,
    applied: true,
    reason: 'applied',
    enabled: true,
    sessionId: 'session',
    revision,
    supportedKeys: ['A'],
    unsupportedKeys: [],
    ...overrides,
  };
}

describe('useKeyLimiterStatusStore', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-01T12:00:00Z'));
    useKeyLimiterStatusStore.getState().reset();
  });

  afterEach(() => {
    useKeyLimiterStatusStore.getState().reset();
    vi.useRealTimers();
  });

  it('shows sending, then acknowledged completion, then settles', async () => {
    const sync = request(1);
    const store = useKeyLimiterStatusStore.getState();

    store.markSyncStarted(sync);
    expect(useKeyLimiterStatusStore.getState().deliveryState).toBe('sending');

    store.markSyncResult(sync, response(1));
    expect(useKeyLimiterStatusStore.getState()).toMatchObject({
      deliveryState: 'applied',
      latestRevision: 1,
      lastAcknowledgement: {
        revision: 1,
        profile: { id: '4key', name: 'Four keys' },
        supportedKeys: ['A'],
        acknowledgedAt: Date.now(),
      },
    });

    await vi.advanceTimersByTimeAsync(1_500);
    expect(useKeyLimiterStatusStore.getState().deliveryState).toBe('idle');
  });

  it('accepts a matching duplicate acknowledgement', () => {
    const sync = request(2);
    const store = useKeyLimiterStatusStore.getState();

    store.markSyncStarted(sync);
    store.markSyncResult(
      sync,
      response(2, { applied: false, reason: 'duplicate' }),
    );

    expect(useKeyLimiterStatusStore.getState().deliveryState).toBe('applied');
  });

  it('shows release completion for a disabled limiter', () => {
    const sync = request(3, false);
    const store = useKeyLimiterStatusStore.getState();

    store.markSyncStarted(sync);
    store.markSyncResult(sync, response(3, { enabled: false }));

    expect(useKeyLimiterStatusStore.getState().deliveryState).toBe('released');
  });

  it('rejects stale or mismatched acknowledgements', () => {
    const sync = request(4);
    const store = useKeyLimiterStatusStore.getState();

    store.markSyncStarted(sync);
    store.markSyncResult(
      sync,
      response(3, { applied: false, reason: 'stale_revision' }),
    );

    expect(useKeyLimiterStatusStore.getState()).toMatchObject({
      deliveryState: 'error',
      error: 'stale_revision',
      lastAcknowledgement: null,
    });
  });

  it('does not let an older response replace the latest status', () => {
    const store = useKeyLimiterStatusStore.getState();
    const older = request(5);
    const latest = request(6);

    store.markSyncStarted(older);
    store.markSyncStarted(latest);
    store.markSyncResult(latest, response(6, { unsupportedKeys: ['FN'] }));
    store.markSyncResult(older, response(5));

    expect(useKeyLimiterStatusStore.getState()).toMatchObject({
      latestRevision: 6,
      lastAcknowledgement: {
        revision: 6,
        unsupportedKeys: ['FN'],
      },
    });
  });
});
