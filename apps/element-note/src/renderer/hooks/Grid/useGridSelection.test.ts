import { createElement } from 'react';
import { flushSync } from 'react-dom';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { keyPositionSchema } from '@src/types/key/keys';
import type { EditorChanges } from '@src/types/editor';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { useLayerGroupStore } from '@stores/data/useLayerGroupStore';
import { useHistoryStore } from '@stores/data/useHistoryStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { useGridSelection } from './useGridSelection';

function captureStore<T>(store: {
  getState: () => T;
  setState: (state: T, replace: true) => void;
}): () => void {
  const state = store.getState();
  return () => store.setState(state, true);
}

describe('grid edits against the v2 editor contract', () => {
  let root: Root;
  let selection: ReturnType<typeof useGridSelection>;
  let restoreStores: (() => void)[];
  const originalApi = window.api;
  const commitChanges = vi.fn(async (changes: EditorChanges) => {
    // v2 rejects a mapping list whose positions still have the old length.
    expect(changes.keys?.hand).toHaveLength(changes.keyPositions!.hand.length);
  });

  beforeEach(() => {
    restoreStores = [
      captureStore(useKeyStore),
      captureStore(useStatItemStore),
      captureStore(useGraphItemStore),
      captureStore(useKnobItemStore),
      captureStore(useLayerGroupStore),
      captureStore(useHistoryStore),
      captureStore(usePluginDisplayElementStore),
      captureStore(useGridSelectionStore),
    ];
    const position = keyPositionSchema.parse({
      id: 'original-key',
      dx: 12,
      dy: 24,
      width: 60,
      height: 60,
      count: 0,
      noteColor: '#ffffff',
      noteOpacity: 100,
      zIndex: 0,
    });
    useKeyStore.setState({
      keyMappings: { hand: ['A'] },
      positions: { hand: [position] },
      selectedKeyType: 'hand',
      isBootstrapped: false,
    });
    useStatItemStore.setState({ positions: {} });
    useGraphItemStore.setState({ positions: {} });
    useKnobItemStore.setState({ positions: {} });
    usePluginDisplayElementStore.setState({ elements: [] });
    useLayerGroupStore.setState({
      layerGroups: {},
      collapsedGroups: new Set(),
    });
    useHistoryStore.getState().clear();
    useGridSelectionStore.setState({
      selectedElements: [{ type: 'key', id: 'key-0', index: 0 }],
      selectedGroupIds: [],
      clipboard: [{ type: 'key', keyCode: 'A', position }],
      clipboardGroups: [],
    });
    commitChanges.mockClear();
    window.api = {
      editor: { commitChanges },
      keys: { update: vi.fn(), updatePositions: vi.fn() },
      statItems: { updatePositions: vi.fn().mockResolvedValue(undefined) },
      graphItems: { updatePositions: vi.fn().mockResolvedValue(undefined) },
      knobItems: { updatePositions: vi.fn().mockResolvedValue(undefined) },
      bridge: { sendTo: vi.fn() },
    } as unknown as typeof window.api;
    function Harness() {
      selection = useGridSelection({
        selectedElements: useGridSelectionStore.getState().selectedElements,
        selectedKeyType: 'hand',
        keyMappings: useKeyStore.getState().keyMappings,
        positions: useKeyStore.getState().positions,
      });
      return null;
    }
    root = createRoot(document.createElement('div'));
    flushSync(() => root.render(createElement(Harness)));
  });

  afterEach(() => {
    flushSync(() => root.unmount());
    for (const restore of restoreStores) restore();
    window.api = originalApi;
  });

  it('deletes key mappings and positions in one validated edit', async () => {
    await selection.deleteSelectedElements();
    expect(commitChanges).toHaveBeenCalledExactlyOnceWith({
      keys: { hand: [] },
      keyPositions: { hand: [] },
    });
    expect(window.api.keys.update).not.toHaveBeenCalled();
    expect(window.api.keys.updatePositions).not.toHaveBeenCalled();
    expect(useKeyStore.getState().isLocalUpdateInProgress).toBe(false);
  });

  it('pastes with a new identity and commits the final layer order once', async () => {
    await selection.pasteElements();
    expect(commitChanges).toHaveBeenCalledTimes(1);
    const changes = commitChanges.mock.calls[0][0];
    expect(changes.keys).toEqual({ hand: ['A', 'A'] });
    expect(changes.keyPositions!.hand[0].id).toBe('original-key');
    expect(changes.keyPositions!.hand[1].id).toBeUndefined();
    expect(changes.keyPositions!.hand[1].zIndex).toBeDefined();
    expect(window.api.keys.update).not.toHaveBeenCalled();
    expect(window.api.keys.updatePositions).not.toHaveBeenCalled();
    expect(useKeyStore.getState().isLocalUpdateInProgress).toBe(false);
  });
});
