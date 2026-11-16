import { useMemo } from 'react';
import SpriteItem from '@components/main/Grid/layers/SpriteItem';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { hasSpriteId } from '@utils/sprite/spriteIdentity';
import { spriteKeyMap } from '@utils/sprite/spriteKeyMap';

/**
 * The images of the open tab, drawn on the shell fixture's canvas by the
 * component the real canvas uses. What the sidebar does to an image (a look
 * that is tried out, a shift that is dragged) can be seen here.
 */
export default function ShellSprites() {
  const viewer = useKeyStore((state) => state.selectedKeyType);
  const sprites = useSpriteStore((state) => state.positions[viewer]);
  const positions = useKeyStore((state) => state.positions[viewer]);
  const keys = useKeyStore((state) => state.keyMappings[viewer]);
  const selected = useGridSelectionStore((state) => state.selectedElements);
  const keyMap = useMemo(
    () => spriteKeyMap(positions ?? [], keys ?? []),
    [positions, keys],
  );
  return (sprites ?? []).map((sprite, index) =>
    hasSpriteId(sprite) ? (
      <SpriteItem
        key={sprite.id}
        index={index}
        elementId={`sprite-${index}`}
        position={sprite}
        keyCanonicalMap={keyMap}
        onPositionChange={() => undefined}
        isSelected={selected.some(
          (element) => element.type === 'sprite' && element.index === index,
        )}
      />
    ) : null,
  );
}
