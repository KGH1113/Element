import type { CSSProperties } from 'react';
import type { SpriteActivation } from '@src/types/key/sprites';
import './sprite.css';

/**
 * Small looping pictures that show what a choice does (see sprite.css). They
 * are drawn with CSS alone and stand still when motion is reduced.
 */

/**
 * A key going down and a picture reacting to it. Held: the picture stays
 * changed until the key comes up. Once: it is back while the key is still
 * down.
 */
export function ReactionPicture({ mode }: { mode: SpriteActivation }) {
  return (
    <span className="en-react" data-mode={mode}>
      <span className="en-react-key">
        <span className="en-react-cap" />
      </span>
      <span className="en-react-figure">
        <span className="en-react-mark" />
      </span>
    </span>
  );
}

/** A block that crosses its track and comes back, moving with this easing. */
export function FeelPicture({ easing }: { easing: string }) {
  return (
    <span
      className="en-feel"
      style={{ '--en-feel-easing': easing } as CSSProperties}
    >
      <span className="en-feel-lane">
        <span className="en-feel-block" />
      </span>
    </span>
  );
}
