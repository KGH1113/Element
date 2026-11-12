import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { CanonicalReactiveSpritePosition } from '@src/types/editor';
import { makeCanonicalSpritePosition } from '@utils/sprite/spriteFixtures';
import SpriteItem from './SpriteItem';

const IMAGE = 'data:image/png;base64,base';

describe('an image on the canvas', () => {
  let root: Root;
  let container: HTMLDivElement;
  const onClick = vi.fn();
  const onContextMenu = vi.fn();

  const render = (
    overrides: Partial<CanonicalReactiveSpritePosition>,
    isSelected = false,
  ) =>
    act(() =>
      root.render(
        <SpriteItem
          index={0}
          elementId="sprite-0"
          position={makeCanonicalSpritePosition({
            id: 'cat',
            dx: 30,
            dy: 40,
            width: 120,
            height: 80,
            ...overrides,
          })}
          keyCanonicalMap={new Map()}
          onPositionChange={vi.fn()}
          onClick={onClick}
          onContextMenu={onContextMenu}
          isSelected={isSelected}
        />,
      ),
    );
  const item = () =>
    container.querySelector<HTMLDivElement>('.dmn-sprite-item');

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    onClick.mockReset();
    onContextMenu.mockReset();
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('is a box of its size at its place, turned as the sprite is', () => {
    render({ baseImage: IMAGE, rotation: 30 });
    const box = item()!;
    expect(box.style.width).toBe('120px');
    expect(box.style.height).toBe('80px');
    expect(box.style.transform).toBe(
      'translate3d(30px, 40px, 0) rotate(30deg)',
    );
    // The image is drawn inside the box, at the box's own origin.
    const image = box.querySelector<HTMLElement>('[data-overlay-hit]')!;
    expect(image.style.transform).toBe('translate3d(0px, 0px, 0)');
    expect(box.querySelector('img')?.getAttribute('src')).toBe(IMAGE);
    expect(box.querySelector('.dmn-sprite-placeholder')).toBeNull();
  });

  it('can be seen and pressed before it has an image', () => {
    render({ baseImage: null });
    const box = item()!;
    expect(box.querySelector('.dmn-sprite-placeholder')).not.toBeNull();
    expect(box.querySelector('img')).toBeNull();

    act(() => box.click());
    expect(onClick).toHaveBeenCalledTimes(1);
    act(() => {
      box.dispatchEvent(
        new MouseEvent('contextmenu', { bubbles: true, cancelable: true }),
      );
    });
    expect(onContextMenu).toHaveBeenCalledTimes(1);
  });

  it('is not drawn while it is hidden', () => {
    render({ baseImage: IMAGE, hidden: true });
    expect(item()).toBeNull();
  });

  it('does not select again when a selected image is pressed', () => {
    render({ baseImage: IMAGE }, true);
    act(() => item()!.click());
    expect(onClick).not.toHaveBeenCalled();
  });
});
