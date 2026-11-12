/**
 * Trying a look out. A look is shown while its keys are held; here the editor
 * holds them for the user, without knowing which keys they are and without a
 * key being pressed anywhere.
 *
 * There is one key that no keyboard has. The look whose page is open is
 * given that key as one more way to reach it, on the editor's canvas only
 * (see SpriteItem), and the button on the page presses and releases it. The
 * image then does what it does for a real key: it changes and stays, or
 * plays once.
 *
 * Nothing is written: the stored sprite is not changed, nothing is saved,
 * and the overlay never hears of the key.
 */

import { create } from 'zustand';
import { applyEventKeyState } from '@stores/signals/keySignals';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';

/** The key the editor holds. No key of a keyboard has this name. */
export const SPRITE_TRY_KEY = 'element:try-look';
/** Stands for that key in the look that is tried out. */
const SPRITE_TRY_TRIGGER = 'element:try-look';

interface SpriteTryOutState {
  /** The look the key stands for: the one whose page is open. */
  target: { spriteId: string; poseId: string } | null;
  /** The key is down. */
  held: boolean;
}

export const useSpriteTryOutStore = create<SpriteTryOutState>(() => ({
  target: null,
  held: false,
}));

/** Lets the key go. Nothing to do while it is up. */
export function releaseSpriteTryOut(): void {
  if (!useSpriteTryOutStore.getState().held) return;
  useSpriteTryOutStore.setState({ held: false });
  applyEventKeyState(SPRITE_TRY_KEY, false);
}

/** Holds the key down. Nothing to do while no look stands for it. */
export function pressSpriteTryOut(): void {
  const { target, held } = useSpriteTryOutStore.getState();
  if (!target || held) return;
  useSpriteTryOutStore.setState({ held: true });
  applyEventKeyState(SPRITE_TRY_KEY, true);
}

/**
 * Makes the key stand for a look, until what is returned is called. The key
 * is let go when the look changes and when it is given up.
 */
export function armSpriteTryOut(spriteId: string, poseId: string): () => void {
  releaseSpriteTryOut();
  const target = { spriteId, poseId };
  useSpriteTryOutStore.setState({ target });
  return () => {
    if (useSpriteTryOutStore.getState().target !== target) return;
    releaseSpriteTryOut();
    useSpriteTryOutStore.setState({ target: null });
  };
}

/**
 * The sprite as the canvas draws it while one of its looks can be tried out:
 * a copy of that look answers to the key. The looks the sprite has are left
 * as they are, so real keys still work.
 */
export function withTryOutLook<
  Sprite extends Pick<ReactiveSpritePosition, 'poses'>,
>(sprite: Sprite, poseId: string | null): Sprite {
  const tried = poseId
    ? sprite.poses.find((pose) => pose.poseId === poseId)
    : undefined;
  if (!tried) return sprite;
  return {
    ...sprite,
    poses: [
      ...sprite.poses,
      {
        ...tried,
        poseId: `${tried.poseId}:try`,
        triggers: [SPRITE_TRY_TRIGGER],
      },
    ],
  };
}

/** The keys of a tab, and the one the editor holds. */
export const withTryOutKey = (
  keys: ReadonlyMap<string, string>,
): Map<string, string> =>
  new Map([...keys, [SPRITE_TRY_TRIGGER, SPRITE_TRY_KEY]]);
