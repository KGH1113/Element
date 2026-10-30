import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GAME_EXIT_GRACE_MS, quitWhenGameLeaves } from './gameOwnedLifetime';
import { FakeMessagePort, testScheduler } from '../__tests__/fakeMessagePort';

describe('an app opened by the game', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function watch(initial: FakeMessagePort['state']) {
    const port = new FakeMessagePort();
    port.state = initial;
    const quit = vi.fn();
    const stop = quitWhenGameLeaves(port, testScheduler, quit);
    return { port, quit, stop };
  }

  it('quits after the game has been gone for the grace period', () => {
    const { port, quit } = watch('ready');
    port.changeState('retrying');
    vi.advanceTimersByTime(GAME_EXIT_GRACE_MS - 1);
    expect(quit).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(quit).toHaveBeenCalledTimes(1);
  });

  it('stays open when the game reconnects in time', () => {
    const { port, quit } = watch('ready');
    port.changeState('retrying');
    port.changeState('connecting');
    vi.advanceTimersByTime(GAME_EXIT_GRACE_MS / 2);
    port.changeState('ready');
    vi.advanceTimersByTime(GAME_EXIT_GRACE_MS * 2);
    expect(quit).not.toHaveBeenCalled();
  });

  it('waits for a game that has not connected yet', () => {
    const { port, quit } = watch('connecting');
    port.changeState('retrying');
    vi.advanceTimersByTime(GAME_EXIT_GRACE_MS * 10);
    expect(quit).not.toHaveBeenCalled();
  });

  it('ignores its own shutdown and stops watching when disposed', () => {
    const { port, quit, stop } = watch('ready');
    port.changeState('disposed');
    vi.advanceTimersByTime(GAME_EXIT_GRACE_MS);
    expect(quit).not.toHaveBeenCalled();
    port.changeState('retrying');
    stop();
    vi.advanceTimersByTime(GAME_EXIT_GRACE_MS);
    expect(quit).not.toHaveBeenCalled();
  });
});
