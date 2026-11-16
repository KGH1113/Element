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
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { useLayerGroupStore } from '@stores/data/useLayerGroupStore';
import { useHistoryStore } from '@stores/data/useHistoryStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import {
  useGridSelectionStore,
  type SelectedElement,
} from '@stores/grid/useGridSelectionStore';
import {
  makeSpritePose,
  makeSpritePosition,
} from '@utils/sprite/spriteFixtures';
import {
  groupSelectedElements,
  ungroupSelectedElements,
} from '@utils/grid/groupActions';
import {
  applyZIndexToLayerOrder,
  buildLayerItemsForMode,
} from '@utils/layerGroupUtils';
import { PASTE_OFFSET } from './constants';
import { useGridSelection } from './useGridSelection';

function captureStore<T>(store: {
  getState: () => T;
  setState: (state: T, replace: true) => void;
}): () => void {
  const state = store.getState();
  return () => store.setState(state, true);
}

const SPRITE: SelectedElement = { type: 'sprite', id: 'sprite-0', index: 0 };
const KEY: SelectedElement = { type: 'key', id: 'key-0', index: 0 };

describe('an image as part of the selection', () => {
  let root: Root;
  let selection: ReturnType<typeof useGridSelection>;
  let restoreStores: (() => void)[];
  const originalApi = window.api;
  const commitChanges = vi.fn(async (_changes: EditorChanges) => undefined);
  const spriteSaves = () =>
    commitChanges.mock.calls
      .map(([changes]) => changes.spritePositions)
      .filter((saved) => saved !== undefined);
  const sprites = () => useSpriteStore.getState().positions.hand;

  /** The hook, as the grid holds it for the given selection. */
  const select = (selectedElements: SelectedElement[]) => {
    useGridSelectionStore.setState({ selectedElements });
    function Harness() {
      selection = useGridSelection({
        selectedElements,
        selectedKeyType: 'hand',
        keyMappings: useKeyStore.getState().keyMappings,
        positions: useKeyStore.getState().positions,
      });
      return null;
    }
    flushSync(() => root.render(createElement(Harness)));
  };

  beforeEach(() => {
    restoreStores = [
      captureStore(useKeyStore),
      captureStore(useStatItemStore),
      captureStore(useGraphItemStore),
      captureStore(useKnobItemStore),
      captureStore(useSpriteStore),
      captureStore(useLayerGroupStore),
      captureStore(useHistoryStore),
      captureStore(usePluginDisplayElementStore),
      captureStore(useGridSelectionStore),
    ];
    const key = keyPositionSchema.parse({
      id: 'key-a',
      dx: 0,
      dy: 0,
      width: 60,
      height: 60,
      count: 0,
      noteColor: '#ffffff',
      noteOpacity: 100,
      zIndex: 0,
    });
    useKeyStore.setState({
      keyMappings: { hand: ['A'] },
      positions: { hand: [key] },
      selectedKeyType: 'hand',
      isBootstrapped: false,
    });
    useStatItemStore.setState({ positions: {} });
    useGraphItemStore.setState({ positions: {} });
    useKnobItemStore.setState({ positions: {} });
    useSpriteStore.setState({
      positions: {
        hand: [
          makeSpritePosition({
            id: 'cat',
            dx: 100,
            dy: 50,
            zIndex: 1,
            poses: [makeSpritePose({ poseId: 'tap', triggers: ['key-a'] })],
          }),
          makeSpritePosition({ id: 'dog', dx: 400, dy: 50, zIndex: 2 }),
        ],
      },
      isLocalUpdateInProgress: false,
    });
    usePluginDisplayElementStore.setState({ elements: [] });
    useLayerGroupStore.setState({
      layerGroups: {},
      collapsedGroups: new Set(),
    });
    useHistoryStore.getState().clear();
    useGridSelectionStore.setState({
      selectedGroupIds: [],
      clipboard: [],
      clipboardGroups: [],
    });
    commitChanges.mockClear();
    const saved = vi.fn().mockResolvedValue(undefined);
    window.api = {
      editor: { commitChanges },
      keys: { update: saved, updatePositions: saved },
      statItems: { updatePositions: saved },
      graphItems: { updatePositions: saved },
      knobItems: { updatePositions: saved },
      layerGroups: { update: saved },
      bridge: { sendTo: vi.fn() },
    } as unknown as typeof window.api;
    root = createRoot(document.createElement('div'));
    select([SPRITE]);
  });

  afterEach(() => {
    flushSync(() => root.unmount());
    for (const restore of restoreStores) restore();
    window.api = originalApi;
  });

  it('follows a drag on screen and is saved once when the drag ends', async () => {
    for (let frame = 0; frame < 3; frame += 1) {
      selection.moveSelectedElements(5, -2, false, false);
    }
    expect(sprites()[0]).toMatchObject({ dx: 115, dy: 44 });
    expect(sprites()[1].dx).toBe(400);
    expect(spriteSaves()).toHaveLength(0);
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(true);

    selection.syncSelectedElementsToOverlay();
    await Promise.resolve();
    await Promise.resolve();
    expect(spriteSaves()).toHaveLength(1);
    expect(spriteSaves()[0]!.hand[0]).toMatchObject({ dx: 115, dy: 44 });
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);
  });

  it('is saved at once when it is moved with the arrow keys', () => {
    selection.moveSelectedElements(1, 0);
    expect(sprites()[0].dx).toBe(101);
    expect(spriteSaves()).toHaveLength(1);
  });

  it('is removed with the selection', async () => {
    await selection.deleteSelectedElements();
    expect(sprites().map((sprite) => sprite.id)).toEqual(['dog']);
    expect(
      spriteSaves()
        .at(-1)!
        .hand.map((sprite) => sprite.id),
    ).toEqual(['dog']);
    expect(useGridSelectionStore.getState().selectedElements).toEqual([]);
  });

  it('is pasted as an image of its own, on top and selected', async () => {
    selection.copySelectedElements();
    await selection.pasteElements();

    const [cat, dog, copy] = sprites();
    expect([cat.id, dog.id]).toEqual(['cat', 'dog']);
    expect(copy.id).not.toBe('cat');
    expect(copy).toMatchObject({
      dx: 100 + PASTE_OFFSET,
      dy: 50 + PASTE_OFFSET,
    });
    // Its poses are its own too, and still react to the same keys.
    expect(copy.poses[0].poseId).not.toBe('tap');
    expect(copy.poses[0].triggers).toEqual(['key-a']);
    expect(useGridSelectionStore.getState().selectedElements).toEqual([
      { type: 'sprite', id: 'sprite-2', index: 2 },
    ]);
    expect(spriteSaves().at(-1)!.hand).toHaveLength(3);
    // Pasted just above what was selected.
    expect(copy.zIndex).toBeGreaterThan(cat.zIndex!);
  });

  it('is grouped and ungrouped with the other elements', async () => {
    select([KEY, SPRITE]);
    await groupSelectedElements('hand', [KEY, SPRITE], 'Group');
    const [group] = useLayerGroupStore.getState().layerGroups.hand;
    expect(sprites()[0].groupId).toBe(group.id);
    expect(useKeyStore.getState().positions.hand[0].groupId).toBe(group.id);
    expect(sprites()[1].groupId ?? null).toBeNull();
    expect(spriteSaves().at(-1)!.hand[0].groupId).toBe(group.id);

    await ungroupSelectedElements('hand', [KEY, SPRITE]);
    expect(sprites()[0].groupId ?? null).toBeNull();
    expect(useLayerGroupStore.getState().layerGroups.hand).toEqual([]);
  });

  it('has its place in the order of layers', () => {
    const keys = useKeyStore.getState().positions;
    const positions = useSpriteStore.getState().positions;
    const order = buildLayerItemsForMode(
      'hand',
      keys,
      {},
      {},
      {},
      positions,
      [],
    );
    expect(order.map((item) => item.id)).toEqual([
      'sprite-1',
      'sprite-0',
      'key-0',
    ]);

    // The same order again: nothing changed, so no sprite is a new object.
    const same = applyZIndexToLayerOrder(
      order,
      'hand',
      keys,
      {},
      {},
      {},
      {
        hand: [
          { ...positions.hand[0], zIndex: 1 },
          { ...positions.hand[1], zIndex: 2 },
        ],
      },
    );
    expect(same.spritePositions.hand.map((sprite) => sprite.zIndex)).toEqual([
      1, 2,
    ]);

    const reversed = applyZIndexToLayerOrder(
      [...order].reverse(),
      'hand',
      keys,
      {},
      {},
      {},
      positions,
    );
    expect(reversed.spritePositions.hand.map((s) => s.zIndex)).toEqual([1, 0]);
    expect(reversed.keyPositions.hand[0].zIndex).toBe(2);
    expect(reversed.spritePositions.hand[0]).toBe(positions.hand[0]);
  });
});
