import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FakeMessagePort, testScheduler } from '../__tests__/fakeMessagePort';
import { KeyLimiterIpcCoordinator } from './keyLimiterIpcCoordinator';
import type { KeyLimiterSyncRequest } from './keyLimiterSnapshot';
const request = (revision: number): KeyLimiterSyncRequest => ({
  schemaVersion: 2,
  source: 'element-note',
  clientVersion: '0.1.0',
  sessionId: 'session',
  revision,
  enabled: true,
  profile: { id: 'profile', name: 'Hand + foot' },
  keys: [`Key${revision}`],
  excludedGhostKeys: [],
});
const response = (revision: number) => ({
  ok: true,
  applied: true,
  reason: 'applied',
  enabled: true,
  sessionId: 'session',
  revision,
  supportedKeys: [`Key${revision}`],
  unsupportedKeys: [],
});
describe('profile event publisher', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());
  it('coalesces edits, permits one unacknowledged profile, then sends the latest edit', async () => {
    const messages = new FakeMessagePort();
    const results = vi.fn();
    const coordinator = new KeyLimiterIpcCoordinator({
      messages,
      scheduler: testScheduler,
      onSyncResult: results,
    });
    coordinator.enqueue(request(1));
    coordinator.enqueue(request(2));
    await vi.advanceTimersByTimeAsync(75);
    expect(messages.sent).toHaveLength(1);
    expect(messages.sent[0]).toMatchObject({
      name: 'key-limiter.profile.set',
      payload: request(2),
    });
    coordinator.enqueue(request(3));
    coordinator.enqueue(request(4));
    await vi.advanceTimersByTimeAsync(75);
    expect(messages.sent).toHaveLength(1);
    messages.receive(
      'key-limiter.profile.applied',
      response(2),
      messages.sent[0].id,
    );
    expect(results).toHaveBeenCalledWith(request(2), response(2));
    expect(messages.sent[1].payload).toEqual(request(4));
    coordinator.dispose();
  });
  it('discards stale replies and resends the latest snapshot on reconnect without health polling', async () => {
    const messages = new FakeMessagePort();
    const results = vi.fn();
    const coordinator = new KeyLimiterIpcCoordinator({
      messages,
      scheduler: testScheduler,
      onSyncResult: results,
    });
    coordinator.enqueue(request(5));
    await coordinator.flushNow();
    const stale = messages.sent[0].id;
    messages.changeState('retrying');
    coordinator.enqueue(request(6));
    await vi.advanceTimersByTimeAsync(75);
    expect(messages.sent).toHaveLength(1);
    messages.changeState('ready');
    expect(messages.sent[1].payload).toEqual(request(6));
    messages.receive('key-limiter.profile.applied', response(5), stale);
    expect(results).not.toHaveBeenCalled();
    messages.receive(
      'key-limiter.profile.applied',
      response(6),
      messages.sent[1].id,
    );
    await vi.advanceTimersByTimeAsync(60000);
    expect(messages.sent).toHaveLength(2);
    coordinator.dispose();
  });
  it('reports named rejection and a missing acknowledgement without retaining every edit', async () => {
    const messages = new FakeMessagePort();
    const errors = vi.fn();
    const coordinator = new KeyLimiterIpcCoordinator({
      messages,
      scheduler: testScheduler,
      onSyncError: errors,
    });
    coordinator.enqueue(request(1));
    await coordinator.flushNow();
    messages.receive(
      'command.rejected',
      { code: 'not_ready' },
      messages.sent[0].id,
    );
    expect(errors).toHaveBeenCalledTimes(1);
    coordinator.enqueue(request(2));
    await coordinator.flushNow();
    await vi.advanceTimersByTimeAsync(5000);
    expect(errors).toHaveBeenCalledTimes(2);
    coordinator.dispose();
  });
  it('resends a profile the game did not confirm until it answers', async () => {
    const messages = new FakeMessagePort();
    const starts = vi.fn();
    const errors = vi.fn();
    const results = vi.fn();
    const coordinator = new KeyLimiterIpcCoordinator({
      messages,
      scheduler: testScheduler,
      onSyncStart: starts,
      onSyncError: errors,
      onSyncResult: results,
    });
    coordinator.enqueue(request(1));
    await coordinator.flushNow();
    await vi.advanceTimersByTimeAsync(5000);
    expect(errors).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(5000);
    expect(messages.sent).toHaveLength(2);
    expect(messages.sent[1].payload).toEqual(request(1));
    // The resend keeps the error visible instead of showing "sending" again.
    expect(starts).toHaveBeenCalledTimes(1);
    messages.receive(
      'key-limiter.profile.applied',
      { ...response(1), applied: false, reason: 'duplicate' },
      messages.sent[1].id,
    );
    expect(results).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(60000);
    expect(messages.sent).toHaveLength(2);
    coordinator.dispose();
  });
  it('sends a newer edit instead of resending an unconfirmed one', async () => {
    const messages = new FakeMessagePort();
    const coordinator = new KeyLimiterIpcCoordinator({
      messages,
      scheduler: testScheduler,
    });
    coordinator.enqueue(request(1));
    await coordinator.flushNow();
    coordinator.enqueue(request(2));
    await vi.advanceTimersByTimeAsync(5000);
    expect(messages.sent.map((sent) => sent.payload)).toEqual([
      request(1),
      request(2),
    ]);
    coordinator.dispose();
  });
  it('unsubscribes and cancels pending timers when disposed', async () => {
    const messages = new FakeMessagePort();
    const coordinator = new KeyLimiterIpcCoordinator({
      messages,
      scheduler: testScheduler,
    });
    coordinator.enqueue(request(1));
    coordinator.dispose();
    messages.changeState('ready');
    await vi.runAllTimersAsync();
    expect(messages.sent).toHaveLength(0);
    expect(coordinator.connectionState).toBe('disposed');
  });
});
