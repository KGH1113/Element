import { createElement } from 'react';
import { flushSync } from 'react-dom';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorChanges } from '@src/types/editor';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import {
  useGridSelectionStore,
  type SelectedElement,
} from '@stores/grid/useGridSelectionStore';
import { makeSpritePosition } from '@utils/sprite/spriteFixtures';
import { useGridMarquee } from './useGridMarquee';
import { useGridResize } from './useGridResize';
import { useSmartGuidesElements } from './useSmartGuidesElements';

const SPRITE: SelectedElement = { type: 'sprite', id: 'sprite-0', index: 0 };

describe('an image among the elements of the canvas', () => {
  let root: Root;
  const originalApi = window.api;
  const commitChanges = vi.fn(async (_changes: EditorChanges) => undefined);
  let guides: ReturnType<typeof useSmartGuidesElements>;
  let marquee: ReturnType<typeof useGridMarquee>;
  let resize: ReturnType<typeof useGridResize>;

  const render = () => {
    function Harness() {
      const spritePositions = useSpriteStore((state) => state.positions);
      guides = useSmartGuidesElements();
      marquee = useGridMarquee({
        positions: {},
        statPositions: {},
        graphPositions: {},
        knobPositions: {},
        spritePositions,
        selectedKeyType: 'hand',
        pluginElements: [],
        clientToGridCoords: (x, y) => ({ x, y }),
      });
      resize = useGridResize({
        selectedElements: [SPRITE],
        selectedKeyType: 'hand',
        getOtherElements: guides.getOtherElements,
      });
      return null;
    }
    flushSync(() => root.render(createElement(Harness)));
  };

  beforeEach(() => {
    commitChanges.mockReset();
    commitChanges.mockResolvedValue(undefined);
    window.api = {
      editor: { commitChanges },
    } as unknown as typeof window.api;
    useKeyStore.setState({
      selectedKeyType: 'hand',
      keyMappings: {},
      positions: {},
    });
    useSpriteStore.setState({
      positions: {
        hand: [
          makeSpritePosition({
            id: 'cat',
            dx: 100,
            dy: 100,
            width: 200,
            height: 100,
            rotation: 90,
            zIndex: 0,
          }),
          makeSpritePosition({ id: 'hidden', hidden: true, zIndex: 1 }),
        ],
      },
      isLocalUpdateInProgress: false,
    });
    useGridSelectionStore.setState({
      selectedElements: [],
      isMarqueeSelecting: false,
      marqueeStart: null,
      marqueeEnd: null,
      isDraggingOrResizing: false,
    });
    root = createRoot(document.createElement('div'));
    render();
  });
  afterEach(() => {
    flushSync(() => root.unmount());
    window.api = originalApi;
    useGridSelectionStore.setState({ selectedElements: [] });
  });

  it('is something to line up with, by the box it is seen in when turned', () => {
    const others = guides.getOtherElements('');
    // 200 by 100 turned a quarter is seen 100 wide and 200 high, same centre.
    expect(others.map((other) => other.id)).toEqual(['sprite-0']);
    const [box] = others;
    expect(box.left).toBeCloseTo(150);
    expect(box.top).toBeCloseTo(50);
    expect(box.width).toBeCloseTo(100);
    expect(box.height).toBeCloseTo(200);
    expect(guides.getOtherElements('sprite-0')).toEqual([]);
  });

  it('is picked by dragging a box over it, unless it is hidden', () => {
    flushSync(() =>
      useGridSelectionStore.setState({
        isMarqueeSelecting: true,
        marqueeStart: { x: 0, y: 0 },
        marqueeEnd: { x: 500, y: 500 },
      }),
    );
    flushSync(() => marquee.handleMarqueeMouseUp());
    expect(useGridSelectionStore.getState().selectedElements).toEqual([SPRITE]);
  });

  it('takes the size the handles leave it at and is saved once', () => {
    resize.handleResizeStart();
    flushSync(() =>
      resize.handleResize({ x: 90, y: 80, width: 240, height: 120 }),
    );
    flushSync(() =>
      resize.handleResize({ x: 80, y: 80, width: 260, height: 130 }),
    );
    // Only a preview so far.
    expect(useSpriteStore.getState().positions.hand[0].width).toBe(200);
    expect(commitChanges).not.toHaveBeenCalled();

    flushSync(() => resize.handleResizeComplete());
    expect(useSpriteStore.getState().positions.hand[0]).toMatchObject({
      dx: 80,
      dy: 80,
      width: 260,
      height: 130,
    });
    expect(commitChanges).toHaveBeenCalledTimes(1);
    expect(
      commitChanges.mock.calls[0][0].spritePositions!.hand[0],
    ).toMatchObject({ id: 'cat', width: 260, height: 130 });
  });
});
