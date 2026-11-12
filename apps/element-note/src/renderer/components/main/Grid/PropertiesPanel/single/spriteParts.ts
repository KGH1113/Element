import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';
import { poseNeedsKey } from '@utils/sprite/unsavedPoses';
import type { KeyPosition } from '@src/types/key/keys';
import type { SpritePose } from '@src/types/key/sprites';

/**
 * The parts of an image (sprite) in the sidebar. An image has a resting look
 * and one look for each set of keys ("pose" in the stored data). The looks
 * are listed on the image's own page and each has a page; everything else is
 * chosen or set in a row.
 */
export const SPRITE_PARTS = {
  image: 'sp-image',
  rest: 'sp-rest',
  addLook: 'sp-add-look',
  reaction: 'sp-reaction',
  feel: 'sp-feel',
  changeTime: 'sp-change-time',
  returnTime: 'sp-return-time',
  css: 'sp-css',
} as const;

const LOOK_PART_PREFIX = 'sp-look:';

export const spriteLookPart = (poseId: string) =>
  `${LOOK_PART_PREFIX}${poseId}`;

/** The look a part id names, or null for any other part. */
export const lookIdOfPart = (part: string | null | undefined) =>
  part?.startsWith(LOOK_PART_PREFIX)
    ? part.slice(LOOK_PART_PREFIX.length)
    : null;

type Translate = (key: string, options?: Record<string, unknown>) => string;

export const spriteLookLabel = (
  pose: Pick<SpritePose, 'name'>,
  index: number,
  t: Translate,
) => pose.name?.trim() || t('sprite.lookNumber', { number: index + 1 });

export interface SpriteKeyOption {
  /** The key element a look names. */
  id: string;
  label: string;
}

/** The keys of a tab a look can react to, named as the canvas shows them. */
export function spriteKeyOptions(
  positions: readonly KeyPosition[],
  mappings: readonly string[],
): SpriteKeyOption[] {
  return positions.flatMap((position, index) => {
    if (!position.id) return [];
    const mapping = mappings[index] || '';
    return [
      {
        id: position.id,
        label:
          position.displayText ||
          (mapping ? getKeyInfoByGlobalKey(mapping).displayName : '') ||
          mapping ||
          `Key ${index + 1}`,
      },
    ];
  });
}

/** When a look is shown, as its row says it: its keys, or that it needs one. */
export function lookWhenText(
  pose: Pick<SpritePose, 'triggers'>,
  keys: readonly SpriteKeyOption[],
  t: Translate,
): string {
  if (poseNeedsKey(pose)) return t('sprite.needsKey');
  const labels = keys
    .filter((key) => pose.triggers.includes(key.id))
    .map((key) => key.label);
  return labels.length > 0
    ? t('sprite.lookWhen', { keys: labels.join(' + ') })
    : t('sprite.keysGone');
}

/** The file name of a stored image, for a row that names it. */
export const imageFileName = (path: string | null | undefined) =>
  !path || path.startsWith('data:') ? '' : path.split(/[\\/]/).pop() || '';
