import { describe, expect, it } from 'vitest';
import { createInvocationBarrier } from './invoke';

describe('native save barrier', () => {
  it('retains a settled save failure until that owner successfully retries', async () => {
    const barrier = createInvocationBarrier();
    const failure = new Error('unsaved plugin layout');
    barrier.setFailure('plugin:layout', failure);
    await expect(barrier.flush()).rejects.toBe(failure);
    barrier.clearFailure('editor:keyPositions');
    await expect(barrier.flush()).rejects.toBe(failure);
    barrier.clearFailure('plugin:layout');
    await expect(barrier.flush()).resolves.toBeUndefined();
  });
  it('waits for chained writes before acknowledging lifecycle', async () => {
    const barrier = createInvocationBarrier();
    let finishFirst!: () => void;
    let finishSecond!: () => void;
    let flushed = false;
    const first = barrier.track(
      new Promise<void>((resolve) => {
        finishFirst = resolve;
      }),
    );
    void first.then(() => {
      barrier.track(
        new Promise<void>((resolve) => {
          finishSecond = resolve;
        }),
      );
    });
    const flush = barrier.flush().then(() => {
      flushed = true;
    });
    finishFirst();
    await first;
    await Promise.resolve();
    expect(flushed).toBe(false);
    finishSecond();
    await flush;
    expect(flushed).toBe(true);
  });

  it('rejects a flush when an outstanding save fails', async () => {
    const barrier = createInvocationBarrier();
    let fail!: (error: Error) => void;
    const save = barrier.track(
      new Promise<void>((_resolve, reject) => {
        fail = reject;
      }),
    );
    const error = new Error('disk full');
    const flush = barrier.flush();
    await Promise.resolve();
    fail(error);
    await expect(save).rejects.toBe(error);
    await expect(flush).rejects.toBe(error);
    await expect(barrier.flush()).resolves.toBeUndefined();
  });
});
