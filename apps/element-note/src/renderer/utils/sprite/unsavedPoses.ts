import { normalizeSpriteDraft } from '@components/main/Modal/content/editors/spriteEditorDraft';
import type {
  ReactiveSpritePosition,
  SpritePose,
  SpritePositions,
} from '@src/types/key/sprites';

/**
 * A pose reacts to keys, so one without a key cannot be saved yet. It stays on
 * screen, marked as needing a key, and is left out of what is saved until it
 * has one. These three functions are the whole rule: which poses wait, what is
 * saved without them, and how they survive what the backend reports.
 */

/** A pose that has no key yet. */
export const poseNeedsKey = (pose: Pick<SpritePose, 'triggers'>): boolean =>
  pose.triggers.length === 0;

/** The sprite as it is saved: without the poses that still need a key. */
export function savableSprite(
  sprite: ReactiveSpritePosition,
): ReactiveSpritePosition {
  return normalizeSpriteDraft({
    ...sprite,
    poses: sprite.poses.filter((pose) => !poseNeedsKey(pose)),
  });
}

export function savableSprites(positions: SpritePositions): SpritePositions {
  return Object.fromEntries(
    Object.entries(positions).map(([mode, sprites]) => [
      mode,
      sprites.map(savableSprite),
    ]),
  );
}

function keepSpritePoses(
  local: ReactiveSpritePosition | undefined,
  incoming: ReactiveSpritePosition,
): ReactiveSpritePosition {
  if (!local || !local.poses.some(poseNeedsKey)) return incoming;
  const known = new Set(incoming.poses.map((pose) => pose.poseId));
  const poses = [...incoming.poses];
  local.poses.forEach((pose, index) => {
    // A pose the backend has is the backend's; only what it never got stays.
    if (!poseNeedsKey(pose) || known.has(pose.poseId)) return;
    poses.splice(Math.min(index, poses.length), 0, pose);
  });
  return poses.length === incoming.poses.length
    ? incoming
    : { ...incoming, poses };
}

/**
 * What the backend reports, with the poses that are waiting for a key put
 * back where they were. Sprites the backend no longer has are gone, and so
 * are their poses.
 */
export function keepUnsavedPoses(
  local: SpritePositions,
  incoming: SpritePositions,
): SpritePositions {
  let kept: SpritePositions | null = null;
  for (const [mode, sprites] of Object.entries(incoming)) {
    const waiting = (local[mode] ?? []).filter((sprite) =>
      sprite.poses.some(poseNeedsKey),
    );
    if (waiting.length === 0) continue;
    const byId = new Map(waiting.map((sprite) => [sprite.id, sprite]));
    const merged = sprites.map((sprite) =>
      keepSpritePoses(byId.get(sprite.id), sprite),
    );
    if (merged.some((sprite, index) => sprite !== sprites[index])) {
      kept ??= { ...incoming };
      kept[mode] = merged;
    }
  }
  return kept ?? incoming;
}
