import { act, createRef } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import GridMinimap from './GridMinimap';

const setPan = vi.fn();
vi.mock('@stores/grid/useGridViewStore', () => ({
  useGridViewStore: () => ({ setPan }),
}));

const pointer = (type: string, clientX: number, clientY: number) => {
  const Event = window.PointerEvent ?? MouseEvent;
  return new Event(type, {
    bubbles: true,
    button: 0,
    clientX,
    clientY,
    pointerId: 1,
  } as PointerEventInit);
};

describe('minimap', () => {
  let root: Root;
  let container: HTMLDivElement;
  const capture = HTMLElement.prototype.setPointerCapture;

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
    HTMLElement.prototype.setPointerCapture = vi.fn();
    setPan.mockReset();
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    HTMLElement.prototype.setPointerCapture = capture;
    vi.unstubAllGlobals();
  });

  it('moves the view by the distance dragged and never jumps to the pointer', () => {
    const grid = createRef<HTMLDivElement>() as React.RefObject<HTMLDivElement>;
    act(() =>
      root.render(
        <>
          <div ref={grid} />
          <GridMinimap
            positions={[{ dx: 0, dy: 0, width: 60, height: 60 }]}
            zoom={2}
            panX={30}
            panY={-10}
            containerRef={grid}
            mode="hand"
            visible
            onZoomIn={vi.fn()}
            onZoomOut={vi.fn()}
            onResetZoom={vi.fn()}
          />
        </>,
      ),
    );
    const map = container.querySelector<HTMLDivElement>('.dmn-minimap-canvas')!;

    // Pressing far from the view rectangle does not move anything.
    act(() => void map.dispatchEvent(pointer('pointerdown', 110, 70)));
    expect(setPan).not.toHaveBeenCalled();

    // The jsdom grid measures 0x0, so the map shows the 60x60 key at 1:1.
    // 10px right and 5px down on the map is the same on the canvas; the pan
    // moves the other way, times the zoom.
    act(() => void map.dispatchEvent(pointer('pointermove', 120, 75)));
    expect(setPan).toHaveBeenLastCalledWith('hand', 30 - 10 * 2, -10 - 5 * 2);

    act(() => void map.dispatchEvent(pointer('pointerup', 120, 75)));
    act(() => void map.dispatchEvent(pointer('pointermove', 200, 200)));
    expect(setPan).toHaveBeenCalledTimes(1);
  });
});
