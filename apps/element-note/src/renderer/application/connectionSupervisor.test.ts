import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConnectionSupervisor } from './connectionSupervisor';
import { testScheduler } from '../__tests__/fakeMessagePort';
describe('initial connection discovery', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());
  it('waits for a game started after the app with bounded retry delays', async () => {
    const connect = vi
      .fn()
      .mockRejectedValueOnce(new Error('game closed'))
      .mockResolvedValue({ connected: true });
    const supervisor = new ConnectionSupervisor(
      { connect, isIncompatible: () => false },
      testScheduler,
    );
    const states: string[] = [];
    supervisor.onState((state) => states.push(state));
    const pending = supervisor.start();
    await vi.advanceTimersByTimeAsync(500);
    expect(await pending).toEqual({ connected: true });
    expect(connect).toHaveBeenCalledTimes(2);
    expect(states).toEqual(['connecting', 'retrying', 'connecting']);
  });
  it('does not retry an incompatible server', async () => {
    const mismatch = new Error('update IPC');
    const connect = vi.fn().mockRejectedValue(mismatch);
    const supervisor = new ConnectionSupervisor(
      { connect, isIncompatible: () => true },
      testScheduler,
    );
    await expect(supervisor.start()).rejects.toBe(mismatch);
    await vi.runAllTimersAsync();
    expect(connect).toHaveBeenCalledTimes(1);
  });
});
