import type { CanonicalReactiveSpritePosition } from '@src/types/editor';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';

/** A sprite the editor has given an id, which every runtime layer keys by. */
export const hasSpriteId = (
  position: ReactiveSpritePosition,
): position is CanonicalReactiveSpritePosition =>
  typeof position.id === 'string' && position.id.length > 0;
