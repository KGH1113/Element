import { invoke, trackEditorSave } from '../invoke';
import { subscribe } from './shared';

import type { StatItemPositions } from '@src/types/key/statItems';
import type { GraphItemPositions } from '@src/types/key/graphItems';
import type { KnobItemPositions } from '@src/types/key/knobs';
import type { LayerGroups } from '@src/types/layerGroups';

export const statItemsApi = {
  getPositions: () => invoke<StatItemPositions>('stat_positions_get'),
  updatePositions: (positions: StatItemPositions) =>
    trackEditorSave(
      invoke<StatItemPositions>('stat_positions_update', { positions }),
      ['editor:statPositions'],
    ),
  onPositionsChanged: (listener: (positions: StatItemPositions) => void) =>
    subscribe<StatItemPositions>('statPositions:changed', listener),
};

export const graphItemsApi = {
  getPositions: () => invoke<GraphItemPositions>('graph_positions_get'),
  updatePositions: (positions: GraphItemPositions) =>
    trackEditorSave(
      invoke<GraphItemPositions>('graph_positions_update', { positions }),
      ['editor:graphPositions'],
    ),
  onPositionsChanged: (listener: (positions: GraphItemPositions) => void) =>
    subscribe<GraphItemPositions>('graphPositions:changed', listener),
};

export const knobItemsApi = {
  getPositions: () => invoke<KnobItemPositions>('knob_positions_get'),
  updatePositions: (positions: KnobItemPositions) =>
    trackEditorSave(
      invoke<KnobItemPositions>('knob_positions_update', { positions }),
      ['editor:knobPositions'],
    ),
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
