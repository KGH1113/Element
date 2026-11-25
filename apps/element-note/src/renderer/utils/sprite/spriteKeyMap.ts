import type { KeyPosition } from '@src/types/key/keys';

/**
 * Which key each key element stands for. Poses name key elements, so a pose
 * keeps its place when the key of an element is changed; the runtime looks
 * the key up here.
 */
export function spriteKeyMap(
  positions: readonly KeyPosition[],
  keys: readonly string[],
): Map<string, string> {
  const map = new Map<string, string>();
  positions.forEach((position, index) => {
    if (position.id && keys[index]) map.set(position.id, keys[index]);
  });
  return map;
}
