import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PictureSegments from './PictureSegments';

describe('picture segments', () => {
  let root: Root;
  let container: HTMLDivElement;
  const onChoose = vi.fn();

  const draw = (value: 'left' | 'center' | 'right', disabled = false) =>
    act(() =>
      root.render(
        <PictureSegments
          label="Where above the key"
          choices={(['left', 'center', 'right'] as const).map((id) => ({
            id,
            label: id,
            picture: <svg data-picture={id} />,
          }))}
          value={value}
          disabled={disabled}
          onChoose={onChoose}
        />,
      ),
    );
  const options = () =>
    Array.from(container.querySelectorAll<HTMLButtonElement>('[role=radio]'));

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    onChoose.mockReset();
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('shows every choice at once, each as its picture and its name', () => {
    draw('center');
    expect(
      container.querySelector('[role=radiogroup]')?.getAttribute('aria-label'),
    ).toBe('Where above the key');
    expect(
      options().map((option) => [
        option.textContent,
        option.querySelector('[data-picture]')?.getAttribute('data-picture'),
        option.getAttribute('aria-checked'),
      ]),
    ).toEqual([
      ['left', 'left', 'false'],
      ['center', 'center', 'true'],
      ['right', 'right', 'false'],
    ]);
  });

  it('chooses another look when it is pressed, and nothing for the chosen one', () => {
    draw('center');
    act(() => options()[1].click());
    expect(onChoose).not.toHaveBeenCalled();
    act(() => options()[2].click());
    expect(onChoose).toHaveBeenCalledWith('right');
  });

  it('says which look the pointer rests on, and when it has left them all', () => {
    const onPreview = vi.fn();
    act(() =>
      root.render(
        <PictureSegments
          label="Where above the key"
          choices={(['left', 'right'] as const).map((id) => ({
            id,
            label: id,
            picture: null,
          }))}
          value="left"
          onChoose={onChoose}
          onPreview={onPreview}
        />,
      ),
    );
    const enter = (target: Element) =>
      act(() => {
        target.dispatchEvent(
          new PointerEvent('pointerover', { bubbles: true }),
        );
      });
    enter(options()[1]);
    expect(onPreview).toHaveBeenLastCalledWith('right');
    act(() => {
      container
        .querySelector('[role=radiogroup]')!
        .dispatchEvent(new PointerEvent('pointerout', { bubbles: true }));
    });
    expect(onPreview).toHaveBeenLastCalledWith(null);
    // Leaving the page ends it too.
    enter(options()[1]);
    act(() => root.render(null));
    expect(onPreview).toHaveBeenLastCalledWith(null);
    // Nothing is chosen by pointing.
    expect(onChoose).not.toHaveBeenCalled();
  });

  it('chooses nothing while the setting does not apply', () => {
    draw('center', true);
    act(() => options()[0].click());
    expect(onChoose).not.toHaveBeenCalled();
  });
});
