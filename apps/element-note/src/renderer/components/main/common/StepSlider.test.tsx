import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import StepSlider from './StepSlider';

const STOPS = [0, 100, 200, 300, 400];

describe('a slider over a few values', () => {
  let root: Root;
  let container: HTMLDivElement;
  const onPreview = vi.fn();
  const onCommit = vi.fn();

  const render = (value: number) =>
    act(() =>
      root.render(
        <StepSlider
          stops={STOPS}
          value={value}
          ariaLabel="Time"
          format={(ms) => `${ms / 1000}s`}
          onPreview={onPreview}
          onCommit={onCommit}
        />,
      ),
    );
  const track = () => container.querySelector<HTMLElement>('.en-slider-track')!;
  const text = () => container.querySelector('.en-slider-value')!.textContent;
  const thumbAt = () =>
    container.querySelector<HTMLElement>('.en-slider-lane')!.style.transform;
  const pointer = (type: string, clientX = 0) =>
    act(() => {
      track().dispatchEvent(
        new MouseEvent(type, { bubbles: true, clientX, button: 0 }),
      );
    });
  const key = (name: string) =>
    act(() => {
      track().dispatchEvent(
        new KeyboardEvent('keydown', { bubbles: true, key: name }),
      );
    });

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    onPreview.mockReset();
    onCommit.mockReset();
    // The rail is 100px wide and starts at the left edge.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      top: 0,
      right: 100,
      bottom: 4,
      width: 100,
      height: 4,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.restoreAllMocks();
  });

  it('reads its value in words and says it to a screen reader', () => {
    render(300);
    expect(text()).toBe('0.3s');
    expect(track().getAttribute('role')).toBe('slider');
    expect(track().getAttribute('aria-label')).toBe('Time');
    expect(track().getAttribute('aria-valuenow')).toBe('300');
    expect(track().getAttribute('aria-valuetext')).toBe('0.3s');
    expect(thumbAt()).toBe('translate3d(75%, 0, 0)');
  });

  it('shows a value between two stops where it is', () => {
    render(150);
    expect(text()).toBe('0.15s');
    expect(thumbAt()).toBe('translate3d(37.5%, 0, 0)');
  });

  it('follows a drag without saving and commits once when it is let go', () => {
    render(0);
    pointer('pointerdown', 20);
    pointer('pointermove', 48);
    pointer('pointermove', 52);
    pointer('pointermove', 81);
    // The thumb is where the pointer is; the words name the nearest stop.
    expect(thumbAt()).toBe('translate3d(81%, 0, 0)');
    expect(text()).toBe('0.3s');
    expect(onPreview.mock.calls.map(([value]) => value)).toEqual([
      100, 200, 300,
    ]);
    expect(onCommit).not.toHaveBeenCalled();

    pointer('pointerup', 81);
    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit).toHaveBeenCalledWith(300);
    // Let go, the thumb is on the value it is given again.
    expect(thumbAt()).toBe('translate3d(0%, 0, 0)');

    // A pointer that moves without holding the thumb does nothing.
    pointer('pointermove', 10);
    pointer('pointerup', 10);
    expect(onCommit).toHaveBeenCalledTimes(1);
  });

  it('does not commit a drag that ends where it began', () => {
    render(200);
    pointer('pointerdown', 49);
    pointer('pointermove', 80);
    pointer('pointermove', 51);
    pointer('pointerup', 51);
    expect(onCommit).not.toHaveBeenCalled();
  });

  it('drops a drag that is cancelled', () => {
    render(0);
    pointer('pointerdown', 90);
    pointer('pointercancel');
    expect(onCommit).not.toHaveBeenCalled();
    // What was previewed is taken back.
    expect(onPreview.mock.calls.map(([value]) => value)).toEqual([400, 0]);
    expect(text()).toBe('0s');
  });

  it('moves one stop for an arrow key and commits each move', () => {
    render(150);
    key('ArrowRight');
    expect(onCommit).toHaveBeenLastCalledWith(200);
    key('ArrowLeft');
    expect(onCommit).toHaveBeenLastCalledWith(100);
    key('End');
    expect(onCommit).toHaveBeenLastCalledWith(400);
    key('Home');
    expect(onCommit).toHaveBeenLastCalledWith(0);
    expect(onCommit).toHaveBeenCalledTimes(4);

    // At an end there is nowhere to go.
    render(400);
    key('ArrowRight');
    expect(onCommit).toHaveBeenCalledTimes(4);
  });
});
