import {
  CENTER_SPRITE_ANCHOR,
  DEFAULT_SPRITE_SIZE,
  DEFAULT_SPRITE_TRANSITION_EASING,
  IDENTITY_SPRITE_TRANSFORM,
  findDuplicateTriggerPose,
  normalizeSpriteTriggers,
  reactiveSpritePositionInputSchema,
  type ReactiveSpritePosition,
  type SpritePose,
} from '@src/types/key/sprites';
import {
  fitSpriteBoundsToNaturalSize,
  type SpriteNaturalSize,
} from '@utils/sprite/spritePlacement';

export function createSpriteDraft(
  initial: ReactiveSpritePosition | null,
  position = { dx: 0, dy: 0 },
): ReactiveSpritePosition {
  if (initial) return structuredClone(initial);
  return {
    id: crypto.randomUUID(),
    ...position,
    width: DEFAULT_SPRITE_SIZE,
    height: DEFAULT_SPRITE_SIZE,
    rotation: 0,
    hidden: false,
    zIndex: null,
    layerName: null,
    groupId: null,
    className: null,
    useInlineStyles: false,
    baseImage: null,
    pivot: { ...CENTER_SPRITE_ANCHOR },
    idleTransform: { ...IDENTITY_SPRITE_TRANSFORM },
    transitionMs: 0,
    transitionEasing: DEFAULT_SPRITE_TRANSITION_EASING,
    activation: 'whileHeld',
    pressDurationMs: 300,
    poses: [],
    referenceNaturalSize: null,
  };
}

export function createSpritePose(): SpritePose {
  return {
    poseId: crypto.randomUUID(),
    name: null,
    triggers: [],
    transform: { ...IDENTITY_SPRITE_TRANSFORM },
    imageOverride: null,
    pivot: null,
    imageOverrideMetrics: null,
  };
}

export function applySpriteImage(
  draft: ReactiveSpritePosition,
  source: string,
  naturalSize: SpriteNaturalSize,
  poseId: string | null,
): ReactiveSpritePosition {
  if (poseId === null) {
    return {
      ...draft,
      ...fitSpriteBoundsToNaturalSize(draft, draft.pivot, naturalSize),
      baseImage: source,
      referenceNaturalSize: { source, ...naturalSize },
    };
  }
  return {
    ...draft,
    referenceNaturalSize:
      !draft.baseImage && !draft.referenceNaturalSize
        ? { source: null, ...naturalSize }
        : draft.referenceNaturalSize,
    poses: draft.poses.map((pose) =>
      pose.poseId === poseId
        ? {
            ...pose,
            imageOverride: source,
            imageOverrideMetrics: { source, ...naturalSize },
          }
        : pose,
    ),
  };
}

export type SpriteDraftError =
  'range' | 'emptyTriggers' | 'duplicateTriggers' | 'easing';

export function validateSpriteDraft(
  draft: ReactiveSpritePosition,
): SpriteDraftError | null {
  if (!reactiveSpritePositionInputSchema.safeParse(draft).success)
    return 'range';
  if (draft.poses.some((pose) => pose.triggers.length === 0))
    return 'emptyTriggers';
  if (findDuplicateTriggerPose(draft.poses)) return 'duplicateTriggers';
  if (
    !draft.transitionEasing.trim() ||
    (typeof CSS !== 'undefined' &&
      typeof CSS.supports === 'function' &&
      !CSS.supports('transition-timing-function', draft.transitionEasing))
  )
    return 'easing';
  return null;
}

export function normalizeSpriteDraft(
  draft: ReactiveSpritePosition,
): ReactiveSpritePosition {
  return {
    ...structuredClone(draft),
    poses: draft.poses.map((pose) => ({
      ...structuredClone(pose),
      triggers: normalizeSpriteTriggers(pose.triggers),
    })),
  };
}
