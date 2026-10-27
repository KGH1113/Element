import type { KeyMappings, KeyPositions } from '@src/types/key/keys';
import { ghostKeyLabels } from '@src/renderer/editor/model/ghostKeys';

/**
 * A dedicated viewer is authoritative for its own selected tab. The global
 * editor mode may point at the other viewer and must not suppress its input.
 * Ghost keys of that tab belong to the viewer too.
 */
export function isKeyMappedToViewer(
  key: string,
  selectedKeyType: string,
  keyMappings: KeyMappings,
  positions?: KeyPositions,
): boolean {
  const keys = keyMappings[selectedKeyType] ?? [];
  if (keys.includes(key)) return true;
  return positions
    ? ghostKeyLabels(keys, positions[selectedKeyType] ?? []).includes(key)
    : false;
}
