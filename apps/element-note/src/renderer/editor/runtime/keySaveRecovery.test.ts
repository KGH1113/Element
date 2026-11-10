import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearEditorSaveFailure } from '@api/invoke';
import { keyPositionSchema } from '@src/types/key/keys';
import type { EditorSnapshot } from '@src/types/editor';
import { useKeyStore } from '@stores/data/useKeyStore';
import { onKeySaveFailed, revertUnsavedKeyChange } from './keySaveRecovery';

vi.mock('@api/invoke', () => ({
  invoke: vi.fn(),
  trackEditorSave: vi.fn(),
  trackEditorInvocation: vi.fn(),
  clearEditorSaveFailure: vi.fn(),
}));

const position = (id: string, dx: number) =>
  keyPositionSchema.parse({
    id,
    dx,
    dy: 0,
    width: 60,
    height: 60,
    count: 0,
    noteColor: '#ffffff',
    noteOpacity: 100,
    zIndex: 0,
  });

const saved = {
  keys: { hand: ['A', { keys: ['LEFT CTRL', 'B'], match: 'all' }] },
  keyPositions: { hand: [position('a', 0), position('b', 60)] },
};

describe('a key change that could not be saved', () => {
  const originalApi = window.api;
  const originalState = useKeyStore.getState();
  const get = vi.fn<() => Promise<EditorSnapshot>>();
  const failures = vi.fn();
  let stopListening: () => void;

  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(clearEditorSaveFailure).mockClear();
    // Only the key fields matter here.
    get.mockReset().mockResolvedValue({
      revision: 3,
      document: saved,
    } as unknown as EditorSnapshot);
    failures.mockReset();
    window.api = { editor: { get } } as unknown as typeof window.api;
    stopListening = onKeySaveFailed(failures);
    // The screen shows a rebinding the save rejected.
    useKeyStore.setState({
      keyMappings: { hand: ['A', 'Q'] },
      positions: { hand: [position('a', 0), position('b', 60)] },
    });
  });

  afterEach(() => {
    stopListening();
    window.api = originalApi;
    useKeyStore.setState(originalState, true);
    vi.restoreAllMocks();
  });

  it('shows the saved keys again and stops holding quitting back', async () => {
    const unsavedPositions = useKeyStore.getState().positions;

    await revertUnsavedKeyChange(new Error('rejected'), ['keys']);

    expect(useKeyStore.getState().keyMappings).toEqual({
      hand: ['A', 'LEFT CTRL+B'],
    });
    expect(useKeyStore.getState().positions).toBe(unsavedPositions);
    expect(clearEditorSaveFailure).toHaveBeenCalledTimes(1);
    expect(clearEditorSaveFailure).toHaveBeenCalledWith('editor:keys');
    expect(failures).toHaveBeenCalledWith({ restored: true });
  });

  it('puts keys and their positions back together after a key was added or removed', async () => {
    useKeyStore.setState({
      keyMappings: { hand: ['A'] },
      positions: { hand: [position('a', 0)] },
    });
    const setKeyMappings = vi.spyOn(useKeyStore.getState(), 'setKeyMappings');

    await revertUnsavedKeyChange(new Error('rejected'), [
      'keys',
      'keyPositions',
    ]);

    expect(setKeyMappings).not.toHaveBeenCalled();
    expect(useKeyStore.getState().keyMappings.hand).toHaveLength(2);
    expect(useKeyStore.getState().positions).toEqual(saved.keyPositions);
    expect(clearEditorSaveFailure).toHaveBeenCalledWith('editor:keys');
    expect(clearEditorSaveFailure).toHaveBeenCalledWith('editor:keyPositions');
  });

  it('keeps quitting held back when the saved keys cannot be read', async () => {
    get.mockRejectedValue(new Error('offline'));

    await revertUnsavedKeyChange(new Error('rejected'), ['keys']);

    expect(useKeyStore.getState().keyMappings).toEqual({ hand: ['A', 'Q'] });
    expect(clearEditorSaveFailure).not.toHaveBeenCalled();
    expect(failures).toHaveBeenCalledWith({ restored: false });
  });
});
