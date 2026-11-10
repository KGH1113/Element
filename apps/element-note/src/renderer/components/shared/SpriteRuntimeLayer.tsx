import { useMemo } from 'react';
import OverlaySpriteItem from '@components/overlay/counters/OverlaySpriteItem';
import type { KeyPosition } from '@src/types/key/keys';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';
import { hasSpriteId } from '@utils/sprite/spriteIdentity';
import '@styles/sprite-runtime.css';

export default function SpriteRuntimeLayer({
  sprites,
  positions,
  keys,
  mode,
}: {
  sprites: ReactiveSpritePosition[];
  positions: KeyPosition[];
  keys: string[];
  mode: string;
}) {
  const keyCanonicalMap = useMemo(() => {
    const map = new Map<string, string>();
    positions.forEach((position, index) => {
      if (position.id && keys[index]) map.set(position.id, keys[index]);
    });
    return map;
  }, [positions, keys]);
  return sprites
    .filter(hasSpriteId)
    .map((position) => (
      <OverlaySpriteItem
        key={`${mode}-${position.id}`}
        position={position}
        keyCanonicalMap={keyCanonicalMap}
      />
    ));
}
