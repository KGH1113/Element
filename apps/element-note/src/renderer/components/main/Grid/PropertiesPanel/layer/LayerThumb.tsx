import { memo } from 'react';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';
import { frameSpriteLooks, spriteRestLook } from '@utils/sprite/spriteLooks';
import {
  GraphThumb,
  KeyThumb,
  KnobThumb,
} from '@components/main/Sidebar/PartThumbs';
import type { KeyPosition } from '@src/types/key/keys';
import SpriteLookThumb from '../single/sprite/SpriteLookThumb';
import { GRAPH_COLORS, KEY_FACE_COLORS } from '../partDefaults';
import type { LayerItem } from '../types';
import { PluginIcon } from './LayerIcons';

/** What a stat says when it has no words of its own, as the overlay says it. */
const STAT_LABELS: Record<string, string> = {
  kps: 'KPS',
  kpsAvg: 'AVG',
  kpsMax: 'MAX',
  total: 'Total',
};

interface LayerThumbProps {
  item: LayerItem;
  /** The key viewer the list is of. */
  viewerId: string;
}

/**
 * The picture at the start of a row of the element list: the element as it
 * looks on the canvas, small, so a row is found by what it is, not by its name.
 */
function LayerThumb({ item, viewerId }: LayerThumbProps) {
  const index = item.index ?? -1;
  const key = useKeyStore((state) =>
    item.type === 'key' ? state.positions[viewerId]?.[index] : undefined,
  );
  const keyName = useKeyStore((state) =>
    item.type === 'key' ? state.keyMappings[viewerId]?.[index] : undefined,
  );
  const stat = useStatItemStore((state) =>
    item.type === 'stat' ? state.positions[viewerId]?.[index] : undefined,
  );
  const graph = useGraphItemStore((state) =>
    item.type === 'graph' ? state.positions[viewerId]?.[index] : undefined,
  );
  const knob = useKnobItemStore((state) =>
    item.type === 'knob' ? state.positions[viewerId]?.[index] : undefined,
  );
  const sprite = useSpriteStore((state) =>
    item.type === 'sprite' ? state.positions[viewerId]?.[index] : undefined,
  );

  let picture = <PluginIcon />;
  if (key) {
    picture = (
      <KeyThumb
        position={key}
        label={getKeyInfoByGlobalKey(keyName ?? '').displayName}
      />
    );
  } else if (stat) {
    const statType = stat.statType ?? 'kps';
    picture = (
      <KeyThumb
        position={stat as unknown as KeyPosition}
        label={
          (stat.displayText ?? '').trim() || STAT_LABELS[statType] || 'KPS'
        }
      />
    );
  } else if (graph) {
    picture = (
      <GraphThumb
        background={graph.backgroundColor || GRAPH_COLORS.background}
        border={graph.borderColor || GRAPH_COLORS.border}
        line={graph.graphColor || GRAPH_COLORS.line}
      />
    );
  } else if (knob) {
    picture = (
      <KnobThumb
        background={knob.backgroundColor || KEY_FACE_COLORS.background}
        border={knob.borderColor || KEY_FACE_COLORS.border}
      />
    );
  } else if (sprite) {
    const rest = spriteRestLook(sprite);
    picture = (
      <SpriteLookThumb
        sprite={sprite}
        look={rest}
        frame={frameSpriteLooks(sprite, [rest])}
      />
    );
  }

  return (
    <span className="en-layer-thumb" aria-hidden="true">
      {picture}
    </span>
  );
}

export default memo(LayerThumb);
