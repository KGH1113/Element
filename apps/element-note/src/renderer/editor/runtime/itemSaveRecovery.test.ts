import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearEditorSaveFailure, invoke } from '@api/invoke';
import { statItemsApi } from '@api/modules/itemsApi';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import type {
  StatItemPosition,
  StatItemPositions,
} from '@src/types/key/statItems';
import { persistItemPositions } from './itemPositions';
import {
  onItemSaveFailed,
  revertUnsavedItemPositions,
  startItemSaveRecovery,
} from './itemSaveRecovery';

vi.mock('@api/invoke', () => ({
  invoke: vi.fn(),
  trackEditorSave: vi.fn((request: Promise<unknown>) => request),
  trackEditorInvocation: vi.fn(),
  clearEditorSaveFailure: vi.fn(),
}));

const kps = (id: string | undefined, dx: number) =>
  ({
    statType: 'kps',
    id,
    dx,
    dy: 0,
    width: 60,
    height: 60,
    count: 0,
  }) as unknown as StatItemPosition;

const saved: StatItemPositions = { hand: [kps('a', 0)] };
// 편집기가 막 추가한 KPS 항목에는 ID가 없다.
const unsaved: StatItemPositions = { hand: [kps('a', 0), kps(undefined, 60)] };

describe('stat, graph and knob saves', () => {
  const originalApi = window.api;
  const originalState = useStatItemStore.getState();
  const getPositions = vi.fn<() => Promise<StatItemPositions>>();
  const updatePositions =
    vi.fn<(positions: StatItemPositions) => Promise<StatItemPositions>>();
  const sendTo = vi.fn();
  const failures = vi.fn();
  let stopListening: () => void;

  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(clearEditorSaveFailure).mockClear();
    vi.mocked(invoke).mockReset();
    getPositions.mockReset().mockResolvedValue(saved);
    updatePositions.mockReset();
    sendTo.mockReset();
    failures.mockReset();
    window.api = {
      statItems: { getPositions, updatePositions },
      bridge: { sendTo },
    } as unknown as typeof window.api;
    stopListening = onItemSaveFailed(failures);
    useStatItemStore.setState({ positions: unsaved });
  });

  afterEach(() => {
    stopListening();
    window.api = originalApi;
    useStatItemStore.setState(originalState, true);
    vi.restoreAllMocks();
  });

  it('shows the saved stats again and stops holding quitting back', async () => {
    await revertUnsavedItemPositions('statPositions', new Error('rejected'));

    expect(useStatItemStore.getState().positions).toBe(saved);
    expect(clearEditorSaveFailure).toHaveBeenCalledTimes(1);
    expect(clearEditorSaveFailure).toHaveBeenCalledWith('editor:statPositions');
    expect(sendTo).toHaveBeenCalledWith('overlay', 'statPositions:sync', {
      positions: saved,
    });
    expect(failures).toHaveBeenCalledWith({
      field: 'statPositions',
      restored: true,
    });
  });

  it('tells the user once when several saves fail in a row', async () => {
    await Promise.all([
      revertUnsavedItemPositions('statPositions', new Error('first')),
      revertUnsavedItemPositions('statPositions', new Error('second')),
    ]);

    expect(getPositions).toHaveBeenCalledTimes(1);
    expect(failures).toHaveBeenCalledTimes(1);
  });

  it('keeps quitting held back when the saved stats cannot be read', async () => {
    getPositions.mockRejectedValue(new Error('offline'));

    await revertUnsavedItemPositions('statPositions', new Error('rejected'));

    expect(useStatItemStore.getState().positions).toBe(unsaved);
    expect(clearEditorSaveFailure).not.toHaveBeenCalled();
    expect(failures).toHaveBeenCalledWith({
      field: 'statPositions',
      restored: false,
    });
  });

  it('puts the saved stats back when any stat save fails', async () => {
    const stopRecovery = startItemSaveRecovery();
    vi.mocked(invoke).mockRejectedValue(new Error('MISSING_ELEMENT_ID'));

    await expect(statItemsApi.updatePositions(unsaved)).rejects.toThrow();

    await vi.waitFor(() =>
      expect(useStatItemStore.getState().positions).toBe(saved),
    );
    expect(clearEditorSaveFailure).toHaveBeenCalledWith('editor:statPositions');
    stopRecovery();
  });

  it('takes the ID the backend gave a new KPS item', async () => {
    const withIds: StatItemPositions = {
      hand: [kps('a', 0), kps('b', 60)],
    };
    updatePositions.mockResolvedValue(withIds);
    const next: StatItemPositions = { hand: [kps('a', 0), kps(undefined, 60)] };

    await persistItemPositions('statPositions', next);

    expect(updatePositions).toHaveBeenCalledWith(next);
    expect(useStatItemStore.getState().positions).toBe(withIds);
    expect(useStatItemStore.getState().isLocalUpdateInProgress).toBe(false);
  });

  it('leaves an edit made while saving alone', async () => {
    const newer: StatItemPositions = { hand: [kps('a', 30)] };
    updatePositions.mockImplementation(async () => {
      useStatItemStore.setState({ positions: newer });
      return { hand: [kps('a', 0), kps('b', 60)] };
    });

    await persistItemPositions('statPositions', unsaved);

    expect(useStatItemStore.getState().positions).toBe(newer);
  });
});
