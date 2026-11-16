import type {
  ReactiveSpritePosition,
  SpritePose,
  SpriteTransform,
} from '@src/types/key/sprites';
import { pointsAabb, rotatePointAround } from '@utils/element/rotation';
import { resolvePoseImage } from './poseResolver';
import { anchorPx, isSameSpriteTransform } from './spriteGeometry';
import {
  isSameSpritePlacement,
  placeSpriteVisual,
  spriteIdleVisual,
  spritePoseVisual,
  type SpritePlacement,
  type SpritePlacementSource,
} from './spritePlacement';

/**
 * A look of an image: which picture is drawn, where, and how it is moved,
 * turned and sized. The resting look is the one shown while no key is
 * pressed; every other look belongs to a set of keys. The sidebar draws
 * looks small, so it needs to know how much room one takes.
 */
export interface SpriteLook {
  /** The stored image: the look's own picture, or the image's. */
  src: string | null;
  transform: SpriteTransform;
  placement: SpritePlacement;
}

type LookSource = SpritePlacementSource &
  Pick<ReactiveSpritePosition, 'idleTransform'>;

export const spriteRestLook = (sprite: LookSource): SpriteLook => ({
  src: sprite.baseImage,
  transform: sprite.idleTransform,
  placement: placeSpriteVisual(sprite, spriteIdleVisual(sprite)),
});

export const spritePoseLook = (
  sprite: LookSource,
  pose: SpritePose,
): SpriteLook => ({
  src: resolvePoseImage(pose.imageOverride, sprite.baseImage),
  transform: pose.transform,
  placement: placeSpriteVisual(sprite, spritePoseVisual(sprite, pose)),
});

export const isSameSpriteLook = (a: SpriteLook, b: SpriteLook): boolean =>
  a.src === b.src &&
  isSameSpriteTransform(a.transform, b.transform) &&
  isSameSpritePlacement(a.placement, b.placement);

/** A part of the element's own plane, in its pixels. */
export interface SpriteFrame {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** The box a look covers once it is moved, turned and sized. */
export function spriteLookBounds(look: SpriteLook): SpriteFrame {
  const { rect, pivot } = look.placement;
  const { x, y, rotation, scale } = look.transform;
  const origin = anchorPx(rect, pivot);
  const corners = [
    { x: rect.x, y: rect.y },
    { x: rect.x + rect.width, y: rect.y },
    { x: rect.x + rect.width, y: rect.y + rect.height },
    { x: rect.x, y: rect.y + rect.height },
  ].map((corner) => {
    // CSS order: sized and turned around the pivot, then moved.
    const turned = rotatePointAround(
      {
        x: origin.x + (corner.x - origin.x) * scale,
        y: origin.y + (corner.y - origin.y) * scale,
      },
      origin,
      rotation,
    );
    return { x: turned.x + x, y: turned.y + y };
  });
  const box = pointsAabb(corners);
  return {
    x: box.minX,
    y: box.minY,
    width: box.maxX - box.minX,
    height: box.maxY - box.minY,
  };
}

/** Room left around what a frame shows, as a share of the element's size. */
const FRAME_MARGIN = 0.1;

/**
 * The part of the plane that shows the element's box and all of these looks,
 * with a little room around them. Looks that stay near the box share one
 * frame, so a small change does not zoom the picture.
 */
export function frameSpriteLooks(
  sprite: Pick<ReactiveSpritePosition, 'width' | 'height'>,
  looks: readonly SpriteLook[],
): SpriteFrame {
  const margin = Math.max(sprite.width, sprite.height) * FRAME_MARGIN;
  let minX = -margin;
  let minY = -margin;
  let maxX = sprite.width + margin;
  let maxY = sprite.height + margin;
  for (const look of looks) {
    const bounds = spriteLookBounds(look);
    if (
      ![bounds.x, bounds.y, bounds.width, bounds.height].every(Number.isFinite)
    )
      continue;
    minX = Math.min(minX, bounds.x - margin / 2);
    minY = Math.min(minY, bounds.y - margin / 2);
    maxX = Math.max(maxX, bounds.x + bounds.width + margin / 2);
    maxY = Math.max(maxY, bounds.y + bounds.height + margin / 2);
  }
  return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

/** How much a frame is scaled to fit a view of this size. */
export const spriteFrameScale = (
  frame: SpriteFrame,
  view: { width: number; height: number },
): number =>
  Math.min(view.width / frame.width, view.height / frame.height) || 1;
