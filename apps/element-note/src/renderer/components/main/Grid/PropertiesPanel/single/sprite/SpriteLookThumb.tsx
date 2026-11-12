import type { SpriteFrame, SpriteLook } from '@utils/sprite/spriteLooks';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';
import SpriteLookView from './SpriteLookView';

/** The side of the picture at the start of a look's row. */
const SIDE = 24;

interface SpriteLookThumbProps {
  sprite: Pick<ReactiveSpritePosition, 'width' | 'height'>;
  look: SpriteLook;
  /**
   * The part of the plane all of the image's looks are shown in: with one
   * frame for every row, the rows differ exactly as the looks do.
   */
  frame: SpriteFrame;
}

/** A look as the picture of its row: what the image turns into. */
export default function SpriteLookThumb({
  sprite,
  look,
  frame,
}: SpriteLookThumbProps) {
  return (
    <SpriteLookView
      className="en-look-thumb"
      sprite={sprite}
      look={look}
      frame={frame}
      width={SIDE}
      height={SIDE}
    />
  );
}
