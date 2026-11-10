import { beforeEach, describe, expect, it, vi } from 'vitest';

const invoke = vi.hoisted(() => vi.fn());
vi.mock('../invoke', () => ({ invoke }));
vi.mock('./shared', () => ({ subscribe: () => () => {} }));

import { historyApi } from './historyApi';

beforeEach(() => {
  invoke.mockReset();
});

describe('native history authority', () => {
  it('asks native undo to merge live counters and then resyncs the UI', async () => {
    const status = {
      historyRevision: 2,
      historyEpoch: 1,
      statusSeq: 4,
      canUndo: false,
      canRedo: true,
      busy: false,
    };
    invoke.mockResolvedValue(status);
    const restored = vi.fn();
    const unsubscribe = historyApi.onRestored(restored);
    await expect(historyApi.undo()).resolves.toEqual(status);
    expect(invoke).toHaveBeenCalledExactlyOnceWith('history_undo', {
      operationId: expect.stringMatching(/^[0-9a-f-]{36}$/),
    });
    expect(restored).toHaveBeenCalledOnce();
    unsubscribe();
  });

  it('does not replay local document snapshots when native redo fails', async () => {
    const failure = new Error('HISTORY_FRONTEND_FLUSH_DROPPED');
    invoke.mockRejectedValue(failure);
    const restored = vi.fn();
    const unsubscribe = historyApi.onRestored(restored);
    await expect(historyApi.redo()).rejects.toBe(failure);
    expect(invoke).toHaveBeenCalledOnce();
    expect(invoke.mock.calls[0][0]).toBe('history_redo');
    expect(restored).not.toHaveBeenCalled();
    unsubscribe();
  });
});
