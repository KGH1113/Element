import { beforeEach, describe, expect, it } from 'vitest';
import { useHistoryStatusStore } from './useHistoryStatusStore';

beforeEach(() => {
  useHistoryStatusStore.setState({ initialized: false, status: null });
});

describe('native history status', () => {
  it('keeps the latest native status when a delayed response arrives', () => {
    const status = {
      historyRevision: 3,
      historyEpoch: 1,
      statusSeq: 5,
      canUndo: true,
      canRedo: false,
      busy: false,
    };
    useHistoryStatusStore.getState().applyStatus(status);
    useHistoryStatusStore
      .getState()
      .applyStatus({ ...status, statusSeq: 4, canUndo: false, busy: true });
    expect(useHistoryStatusStore.getState()).toMatchObject({
      initialized: true,
      status,
    });
  });
});
