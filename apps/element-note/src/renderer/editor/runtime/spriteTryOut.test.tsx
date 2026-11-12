import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import SpriteItem from '@components/main/Grid/layers/SpriteItem';
import type { EditorChanges } from '@src/types/editor';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import {
  getKeySignal,
  resetAllKeySignals,
  setKeyActive,
} from '@stores/signals/keySignals';
import {
  makeCanonicalSpritePosition,
  makeSpritePose,
} from '@utils/sprite/spriteFixtures';
import {
  SPRITE_TRY_KEY,
  armSpriteTryOut,
  pressSpriteTryOut,
  releaseSpriteTryOut,
  useSpriteTryOutStore,
  withTryOutKey,
  withTryOutLook,
} from './spriteTryOut';

const BASE = 'data:image/png;base64,base';
const PAW = 'data:image/png;base64,paw';

const cat = makeCanonicalSpritePosition({
  id: 'cat',
  width: 120,
  height: 80,
  baseImage: BASE,
  poses: [
    makeSpritePose({
      poseId: 'tap',
      triggers: ['key-a'],
      transform: { x: 4, y: 8, rotation: -20, scale: 1 },
      imageOverride: PAW,
    }),
    // A look that has no key yet can be tried out all the same.
    makeSpritePose({
      poseId: 'new',
      triggers: [],
      transform: { x: 0, y: 0, rotation: 45, scale: 1 },
    }),
  ],
});
const dog = makeCanonicalSpritePosition({ id: 'dog', baseImage: BASE });

describe('trying a look out', () => {
  let root: Root;
  let container: HTMLDivElement;
  const originalApi = window.api;
  const commitChanges = vi.fn(async (_changes: EditorChanges) => undefined);
  const positions = { hand: [cat, dog] };

  const tryOut = () => useSpriteTryOutStore.getState();
  const image = (id: string) =>
    container.querySelector<HTMLImageElement>(
      `[data-sprite-item="${id}"] img`,
    )!;
  const transformOf = (id: string) =>
    image(id).style.getPropertyValue('--dmn-sprite-transform-default');

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    commitChanges.mockReset();
    window.api = {
      editor: { commitChanges },
    } as unknown as typeof window.api;
    useSpriteStore.setState({ positions, isLocalUpdateInProgress: false });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    act(() =>
      root.render(
        <>
          {positions.hand.map((position, index) => (
            <SpriteItem
              key={position.id}
              index={index}
              elementId={position.id}
              position={position}
              keyCanonicalMap={new Map([['key-a', 'A']])}
              onPositionChange={vi.fn()}
            />
          ))}
        </>,
      ),
    );
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    window.api = originalApi;
    act(() => {
      releaseSpriteTryOut();
      useSpriteTryOutStore.setState({ target: null, held: false });
      resetAllKeySignals();
    });
  });

  it('shows the look on the canvas while its button is held, and the resting look again after', () => {
    expect(transformOf('cat')).toContain('rotate(0deg)');
    let disarm!: () => void;
    act(() => {
      disarm = armSpriteTryOut('cat', 'tap');
    });
    // Standing by changes nothing that is seen.
    expect(image('cat').getAttribute('src')).toBe(BASE);
    expect(transformOf('cat')).toContain('rotate(0deg)');

    act(() => pressSpriteTryOut());
    expect(tryOut().held).toBe(true);
    expect(getKeySignal(SPRITE_TRY_KEY).value).toBe(true);
    expect(image('cat').getAttribute('src')).toBe(PAW);
    expect(transformOf('cat')).toBe(
      'translate(4px, 8px) rotate(-20deg) scale(1)',
    );
    // No other image, and no real key, is touched.
    expect(transformOf('dog')).toContain('rotate(0deg)');
    expect(getKeySignal('A').value).toBe(false);

    act(() => releaseSpriteTryOut());
    expect(tryOut().held).toBe(false);
    expect(getKeySignal(SPRITE_TRY_KEY).value).toBe(false);
    expect(image('cat').getAttribute('src')).toBe(BASE);
    expect(transformOf('cat')).toContain('rotate(0deg)');

    act(() => disarm());
    expect(tryOut().target).toBeNull();
  });

  it('tries out a look that has no key yet', () => {
    act(() => {
      armSpriteTryOut('cat', 'new');
      pressSpriteTryOut();
    });
    expect(transformOf('cat')).toBe(
      'translate(0px, 0px) rotate(45deg) scale(1)',
    );
  });

  it('lets go by itself when the look is given up or another one takes its place', () => {
    let disarm!: () => void;
    act(() => {
      disarm = armSpriteTryOut('cat', 'tap');
      pressSpriteTryOut();
    });
    act(() => disarm());
    expect(tryOut()).toEqual({ target: null, held: false });
    expect(getKeySignal(SPRITE_TRY_KEY).value).toBe(false);
    expect(image('cat').getAttribute('src')).toBe(BASE);

    act(() => {
      disarm = armSpriteTryOut('cat', 'tap');
      pressSpriteTryOut();
    });
    act(() => {
      armSpriteTryOut('cat', 'new');
    });
    expect(tryOut().held).toBe(false);
    expect(getKeySignal(SPRITE_TRY_KEY).value).toBe(false);
    // Giving up the look that was replaced does not undo the new one.
    act(() => disarm());
    expect(tryOut().target).toEqual({ spriteId: 'cat', poseId: 'new' });
  });

  it('does nothing while no look stands by', () => {
    act(() => pressSpriteTryOut());
    expect(tryOut().held).toBe(false);
    expect(getKeySignal(SPRITE_TRY_KEY).value).toBe(false);
  });

  it('writes nothing: the stored image is the same and nothing is saved', () => {
    act(() => {
      armSpriteTryOut('cat', 'tap');
      pressSpriteTryOut();
    });
    act(() => releaseSpriteTryOut());
    expect(useSpriteStore.getState().positions).toBe(positions);
    expect(useSpriteStore.getState().isLocalUpdateInProgress).toBe(false);
    expect(commitChanges).not.toHaveBeenCalled();
  });

  it('leaves the looks an image has as they are, so real keys still work', () => {
    expect(withTryOutLook(cat, null)).toBe(cat);
    expect(withTryOutLook(cat, 'gone')).toBe(cat);
    const tried = withTryOutLook(cat, 'tap');
    expect(tried.poses.slice(0, 2)).toEqual(cat.poses);
    expect(tried.poses).toHaveLength(3);
    const keys = withTryOutKey(new Map([['key-a', 'A']]));
    expect(keys.get('key-a')).toBe('A');
    expect(keys.get(tried.poses[2].triggers[0])).toBe(SPRITE_TRY_KEY);

    act(() => {
      armSpriteTryOut('cat', 'new');
    });
    act(() => setKeyActive('A', true));
    expect(image('cat').getAttribute('src')).toBe(PAW);
    expect(transformOf('cat')).toContain('rotate(-20deg)');
  });
});
