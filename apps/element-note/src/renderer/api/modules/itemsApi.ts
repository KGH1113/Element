import { invoke, trackEditorSave } from '../invoke';
import { subscribe } from './shared';

import type { StatItemPositions } from '@src/types/key/statItems';
import type { GraphItemPositions } from '@src/types/key/graphItems';
import type { KnobItemPositions } from '@src/types/key/knobs';
import type { LayerGroups } from '@src/types/layerGroups';

/** KPS·그래프·노브 배치를 저장하는 필드 */
export type ItemPositionsField =
  'statPositions' | 'graphPositions' | 'knobPositions';

type ItemSaveFailureListener = (
  field: ItemPositionsField,
  error: unknown,
) => void;

const itemSaveFailureListeners = new Set<ItemSaveFailureListener>();

/** KPS·그래프·노브 배치 저장이 실패하면 알린다. 화면을 되돌리는 일은 듣는 쪽이 맡는다. */
export function onItemPositionsSaveFailed(
  listener: ItemSaveFailureListener,
): () => void {
  itemSaveFailureListeners.add(listener);
  return () => {
    itemSaveFailureListeners.delete(listener);
  };
}

function saveItemPositions<T>(
  command: string,
  field: ItemPositionsField,
  positions: T,
): Promise<T> {
  const saving = trackEditorSave(invoke<T>(command, { positions }), [
    `editor:${field}`,
  ]);
  saving.catch((error: unknown) => {
    itemSaveFailureListeners.forEach((listener) => listener(field, error));
  });
  return saving;
}

export const statItemsApi = {
  getPositions: () => invoke<StatItemPositions>('stat_positions_get'),
  updatePositions: (positions: StatItemPositions) =>
    saveItemPositions('stat_positions_update', 'statPositions', positions),
  onPositionsChanged: (listener: (positions: StatItemPositions) => void) =>
    subscribe<StatItemPositions>('statPositions:changed', listener),
};

export const graphItemsApi = {
  getPositions: () => invoke<GraphItemPositions>('graph_positions_get'),
  updatePositions: (positions: GraphItemPositions) =>
    saveItemPositions('graph_positions_update', 'graphPositions', positions),
  onPositionsChanged: (listener: (positions: GraphItemPositions) => void) =>
    subscribe<GraphItemPositions>('graphPositions:changed', listener),
};

export const knobItemsApi = {
  getPositions: () => invoke<KnobItemPositions>('knob_positions_get'),
  updatePositions: (positions: KnobItemPositions) =>
    saveItemPositions('knob_positions_update', 'knobPositions', positions),
  onPositionsChanged: (listener: (positions: KnobItemPositions) => void) =>
    subscribe<KnobItemPositions>('knobPositions:changed', listener),
};

export const layerGroupsApi = {
  get: () => invoke<LayerGroups>('layer_groups_get'),
  update: (groups: LayerGroups) =>
    trackEditorSave(invoke<LayerGroups>('layer_groups_update', { groups }), [
      'editor:layerGroups',
    ]),
  onChanged: (listener: (groups: LayerGroups) => void) =>
    subscribe<LayerGroups>('layerGroups:changed', listener),
};
