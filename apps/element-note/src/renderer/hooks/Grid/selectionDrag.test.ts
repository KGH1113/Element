import type React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useSmartGuidesStore } from '@stores/grid/useSmartGuidesStore';
import { calculateBounds } from '@utils/grid/smartGuides';
import { startSelectionDrag } from './selectionDrag';

const press = (clientX: number, clientY: number) =>
  ({
    button: 0,
    clientX,
    clientY,
    preventDefault: () => undefined,
    stopPropagation: () => undefined,
  }) as unknown as React.MouseEvent;

const move = (clientX: number, clientY: number, keys: MouseEventInit = {}) =>
  document.dispatchEvent(
    new MouseEvent('mousemove', { clientX, clientY, ...keys }),
  );
const release = () => document.dispatchEvent(new MouseEvent('mouseup'));

describe('dragging the selection', () => {
  const frames: FrameRequestCallback[] = [];
  const runFrames = () => frames.splice(0).forEach((frame) => frame(0));

  beforeEach(() => {
    frames.length = 0;
    vi.stubGlobal('requestAnimationFrame', (frame: FrameRequestCallback) => {
      frames.push(frame);
      return frames.length;
    });
    vi.stubGlobal('cancelAnimationFrame', () => {
      frames.length = 0;
    });
    useSettingsStore.setState({
      gridSettings: {
        ...useSettingsStore.getState().gridSettings,
        gridSnapSize: 5,
        alignmentGuides: true,
        spacingGuides: true,
        sizeMatchGuides: true,
      },
    });
    useSmartGuidesStore.getState().clearGuides();
  });
  afterEach(() => {
    release();
    vi.unstubAllGlobals();
  });

  const drag = (others = [calculateBounds(300, 300, 60, 60, 'key-9')]) => {
    const moved = { x: 0, y: 0 };
    const onStart = vi.fn();
    const onEnd = vi.fn();
    startSelectionDrag({
      event: press(100, 100),
      elementBox: calculateBounds(12, 8, 60, 60, 'key-0'),
      selectedElements: [{ type: 'key', id: 'key-0', index: 0 }],
      getOtherElements: () => others,
      zoom: 1,
      onStart,
      onMove: (deltaX, deltaY) => {
        moved.x += deltaX;
        moved.y += deltaY;
      },
      onEnd,
    });
    return { moved, onStart, onEnd };
  };

  it('does not move anything for a click, even off the grid', () => {
    const { moved, onStart, onEnd } = drag();
    move(102, 101);
    runFrames();
    release();
    expect(moved).toEqual({ x: 0, y: 0 });
    // No history entry and no save for a click.
    expect(onStart).not.toHaveBeenCalled();
    expect(onEnd).not.toHaveBeenCalled();
  });

  it('lands on the grid and reports whole steps', () => {
    const { moved, onStart, onEnd } = drag();
    move(121, 100); // pointer went 21: 12 + 21 = 33 -> 35
    runFrames();
    expect(onStart).toHaveBeenCalledTimes(1);
    expect(moved).toEqual({ x: 23, y: 2 }); // to (35, 10)
    release();
    expect(onEnd).toHaveBeenCalledTimes(1);
  });

  it('uses the last pointer position when let go between frames', () => {
    const { moved } = drag();
    move(121, 100);
    runFrames();
    move(141, 100); // no frame has run for this one yet
    release();
    expect(moved.x).toBe(43); // 12 + 41 = 53 -> 55
  });

  it('lines up with another element and shows the guide', () => {
    const { moved } = drag([calculateBounds(52, 300, 60, 60, 'key-9')]);
    move(138, 100); // 12 + 38 = 50: 2 away from 52
    runFrames();
    expect(moved.x).toBe(40); // to 52
    expect(
      useSmartGuidesStore
        .getState()
        .activeGuides.some((g) => g.type === 'vertical' && g.position === 52),
    ).toBe(true);
    release();
    expect(useSmartGuidesStore.getState().isActive).toBe(false);
  });

  it('pulls nothing while Alt is held, and moves straight with Shift', () => {
    const alt = drag([calculateBounds(52, 300, 60, 60, 'key-9')]);
    move(138, 103, { altKey: true });
    runFrames();
    expect(alt.moved).toEqual({ x: 38, y: 3 });
    release();

    const shift = drag();
    move(140, 109, { shiftKey: true });
    runFrames();
    expect(shift.moved.y).toBe(0);
    expect(shift.moved.x).toBe(38); // 12 + 40 = 52 -> 50
  });

  it('moves a selection of several as one box', () => {
    const moved = { x: 0, y: 0 };
    startSelectionDrag({
      event: press(100, 100),
      elementBox: calculateBounds(100, 0, 60, 60, 'key-1'),
      selectedElements: [
        { type: 'key', id: 'key-0', index: 0 },
        { type: 'key', id: 'key-1', index: 1 },
      ],
      getOtherElements: () => [
        calculateBounds(0, 0, 60, 60, 'key-0'), // selected: part of the box
        calculateBounds(3, 300, 60, 60, 'key-9'), // left edge 3
      ],
      zoom: 1,
      onMove: (deltaX, deltaY) => {
        moved.x += deltaX;
        moved.y += deltaY;
      },
    });
    // The box around both starts at 0. Dragged by 6 it is 3 away from the
    // other element's left edge, so the whole selection goes to 3.
    move(106, 100);
    runFrames();
    expect(moved).toEqual({ x: 3, y: 0 });
  });
});
