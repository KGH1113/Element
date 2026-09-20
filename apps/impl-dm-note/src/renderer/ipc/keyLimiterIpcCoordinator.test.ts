import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IpcVersionMismatchError } from '@adofai-ipc/client';
import type { KeyLimiterSyncRequest } from './keyLimiterSnapshot';
import {
  KeyLimiterIpcCoordinator,
  type KeyLimiterIpcClient,
} from './keyLimiterIpcCoordinator';

function request(revision: number): KeyLimiterSyncRequest {
  return {
    schemaVersion: 1,
    source: 'impl-dm-note',
    clientVersion: '0.1.0',
    sessionId: 'session',
    revision,
    enabled: true,
    profile: { id: '4key', name: '4key' },
    keys: [`Key${revision}`],
    excludedGhostKeys: [],
  };
}

function readyClient(call = vi.fn().mockResolvedValue({ applied: true })) {
  const client: KeyLimiterIpcClient = {
    health: vi.fn().mockResolvedValue({ ok: true }),
    waitForNamespace: vi.fn().mockResolvedValue({
      namespace: 'impl-resourcepack',
      displayName: 'ImplResourcePack',
      version: '0.1.0',
      status: 'ready',
      methods: ['key-limiter.sync'],
    }),
    call,
  };
  return client;
}

function response(revision: number, overrides = {}) {
  return {
    ok: true,
    applied: true,
    reason: 'applied',
    enabled: true,
    sessionId: 'session',
    revision,
    supportedKeys: [`Key${revision}`],
    unsupportedKeys: [],
    ...overrides,
  };
}

describe('KeyLimiterIpcCoordinator', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('debounces changes and sends only the latest snapshot', async () => {
    const call = vi.fn().mockResolvedValue({ applied: true });
    const client = readyClient(call);
    const coordinator = new KeyLimiterIpcCoordinator({
      connect: vi.fn().mockResolvedValue(client),
    });

    coordinator.enqueue(request(1));
    coordinator.enqueue(request(2));
    await vi.advanceTimersByTimeAsync(75);

    expect(client.waitForNamespace).toHaveBeenCalledWith('impl-resourcepack', {
      status: 'ready',
      timeoutMs: 10_000,
      pollIntervalMs: 100,
    });
    expect(call).toHaveBeenCalledTimes(1);
    expect(call).toHaveBeenCalledWith({
      namespace: 'impl-resourcepack',
      method: 'key-limiter.sync',
      params: request(2),
    });
    expect(coordinator.connectionState).toBe('ready');
  });

  it('retries with the latest pending snapshot after a connection failure', async () => {
    const call = vi.fn().mockResolvedValue({ applied: true });
    const client = readyClient(call);
    const connect = vi
      .fn()
      .mockRejectedValueOnce(new Error('not running'))
      .mockResolvedValue(client);
    const coordinator = new KeyLimiterIpcCoordinator({ connect });

    coordinator.enqueue(request(1));
    await coordinator.flushNow();
    coordinator.enqueue(request(2));
    await vi.advanceTimersByTimeAsync(500);

    expect(connect).toHaveBeenCalledTimes(2);
    expect(call).toHaveBeenCalledTimes(1);
    expect(call.mock.calls[0][0].params.revision).toBe(2);
  });

  it('records version mismatch as a terminal state', async () => {
    const mismatch = new IpcVersionMismatchError({
      clientVersion: '0.4.0',
      serverVersion: '0.2.0',
      direction: 'server_outdated',
      protocolVersion: 1,
    });
    const connect = vi.fn().mockRejectedValue(mismatch);
    const coordinator = new KeyLimiterIpcCoordinator({ connect });

    coordinator.enqueue(request(1));
    await coordinator.flushNow();
    coordinator.enqueue(request(2));
    await vi.runAllTimersAsync();

    expect(coordinator.connectionState).toBe('version-mismatch');
    expect(connect).toHaveBeenCalledTimes(1);
  });

  it('reports request start and the exact server acknowledgement', async () => {
    const result = response(3);
    const call = vi.fn().mockResolvedValue(result);
    const onSyncStart = vi.fn();
    const onSyncResult = vi.fn();
    const coordinator = new KeyLimiterIpcCoordinator({
      connect: vi.fn().mockResolvedValue(readyClient(call)),
      onSyncStart,
      onSyncResult,
    });

    coordinator.enqueue(request(3));
    await coordinator.flushNow();

    expect(onSyncStart).toHaveBeenCalledWith(request(3));
    expect(onSyncResult).toHaveBeenCalledWith(request(3), result);
  });

  it('reports a failed attempt and retries the same revision', async () => {
    const error = new Error('game closed');
    const call = vi
      .fn()
      .mockRejectedValueOnce(error)
      .mockResolvedValue(response(4, { reason: 'duplicate', applied: false }));
    const client = readyClient(call);
    const onSyncStart = vi.fn();
    const onSyncError = vi.fn();
    const coordinator = new KeyLimiterIpcCoordinator({
      connect: vi.fn().mockResolvedValue(client),
      onSyncStart,
      onSyncError,
    });

    coordinator.enqueue(request(4));
    await coordinator.flushNow();
    await vi.advanceTimersByTimeAsync(500);

    expect(onSyncError).toHaveBeenCalledWith(request(4), error);
    expect(onSyncStart).toHaveBeenCalledTimes(2);
    expect(call.mock.calls[1][0].params.revision).toBe(4);
  });

  it('detects a stopped game and resends the latest revision after reconnecting', async () => {
    const firstClient = readyClient(vi.fn().mockResolvedValue(response(5)));
    firstClient.health = vi.fn().mockRejectedValue(new Error('game closed'));
    const secondCall = vi
      .fn()
      .mockResolvedValue(response(5, { applied: false, reason: 'duplicate' }));
    const secondClient = readyClient(secondCall);
    const connect = vi
      .fn()
      .mockResolvedValueOnce(firstClient)
      .mockResolvedValueOnce(secondClient);
    const states: string[] = [];
    const coordinator = new KeyLimiterIpcCoordinator({
      connect,
      connectionCheckMs: 1_000,
      onStateChange: (state) => states.push(state),
    });

    coordinator.enqueue(request(5));
    await coordinator.flushNow();
    await vi.advanceTimersByTimeAsync(1_000);

    expect(coordinator.connectionState).toBe('retrying');
    expect(states).toContain('retrying');

    await vi.advanceTimersByTimeAsync(500);

    expect(connect).toHaveBeenCalledTimes(2);
    expect(secondCall).toHaveBeenCalledWith({
      namespace: 'impl-resourcepack',
      method: 'key-limiter.sync',
      params: request(5),
    });
    expect(coordinator.connectionState).toBe('ready');
  });

  it('keeps a healthy connection ready without resending the snapshot', async () => {
    const call = vi.fn().mockResolvedValue(response(6));
    const client = readyClient(call);
    const coordinator = new KeyLimiterIpcCoordinator({
      connect: vi.fn().mockResolvedValue(client),
      connectionCheckMs: 1_000,
    });

    coordinator.enqueue(request(6));
    await coordinator.flushNow();
    await vi.advanceTimersByTimeAsync(3_000);

    expect(client.health).toHaveBeenCalledTimes(3);
    expect(call).toHaveBeenCalledTimes(1);
    expect(coordinator.connectionState).toBe('ready');
  });
});
