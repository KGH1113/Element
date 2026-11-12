import { useState, type CSSProperties, type Ref } from 'react';
import { resolveImageSource } from '@utils/media/imageSource';
import { computeSpriteImageStyle } from '@utils/sprite/spriteImageStyles';
import {
  spriteFrameScale,
  type SpriteFrame,
  type SpriteLook,
} from '@utils/sprite/spriteLooks';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';
import './sprite.css';

/** The picture is styled by this view alone: a user's CSS does not reach it. */
const OWN_STYLE = { useInlineStyles: true } as const;

function LookPicture({
  look,
  ghost = false,
  pictureRef,
}: {
  look: SpriteLook;
  ghost?: boolean;
  pictureRef?: Ref<HTMLElement>;
}) {
  const src = resolveImageSource(look.src);
  const [failed, setFailed] = useState<string | null>(null);
  const style = computeSpriteImageStyle(
    OWN_STYLE,
    look.transform,
    undefined,
    look.placement,
  );
  // An image that is not chosen yet, or cannot be read, is still a box that
  // is moved, turned and sized.
  if (!src || failed === src) {
    return (
      <span
        ref={pictureRef as Ref<HTMLSpanElement>}
        className="en-look-blank"
        data-ghost={ghost || undefined}
        style={style}
      />
    );
  }
  return (
    <img
      ref={pictureRef as Ref<HTMLImageElement>}
      className="en-look-picture"
      data-ghost={ghost || undefined}
      src={src}
      alt=""
      draggable={false}
      style={style}
      onError={() => setFailed(src)}
    />
  );
}

interface SpriteLookViewProps {
  sprite: Pick<ReactiveSpritePosition, 'width' | 'height'>;
  look: SpriteLook;
  /** Another look, drawn faintly behind: what this one is compared with. */
  ghost?: SpriteLook;
  /** The part of the element's plane that is shown. */
  frame: SpriteFrame;
  /** The size of the view on screen. */
  width: number;
  height: number;
  className?: string;
  /** The picture of `look`, for a caller that plays its motion. */
  pictureRef?: Ref<HTMLElement>;
}

/**
 * A look of an image, drawn small: the same picture, placement and
 * transform the canvas uses, scaled to fit the view. It only shows; whoever
 * uses it decides what a press on it does.
 */
export default function SpriteLookView({
  sprite,
  look,
  ghost,
  frame,
  width,
  height,
  className = '',
  pictureRef,
}: SpriteLookViewProps) {
  const scale = spriteFrameScale(frame, { width, height });
  const centerX = frame.x + frame.width / 2;
  const centerY = frame.y + frame.height / 2;
  return (
    <span
      className={`en-look ${className}`}
      style={{ width, height }}
      aria-hidden="true"
    >
      <span
        className="en-look-plane"
        style={
          {
            transform: `scale(${scale}) translate3d(${-centerX}px, ${-centerY}px, 0)`,
            '--en-look-scale': scale,
          } as CSSProperties
        }
      >
        <span
          className="en-look-box"
          style={{ width: sprite.width, height: sprite.height }}
        >
          {ghost && <LookPicture look={ghost} ghost />}
          <LookPicture look={look} pictureRef={pictureRef} />
        </span>
      </span>
    </span>
  );
}
