import { useGraphItemStore } from './useGraphItemStore';
import { useHistoryStore, type PushHistoryInput } from './useHistoryStore';
import { useKeyStore } from './useKeyStore';
import { useKnobItemStore } from './useKnobItemStore';
import { useLayerGroupStore } from './useLayerGroupStore';
import { useStatItemStore } from './useStatItemStore';
import { usePluginDisplayElementStore } from '../plugin/usePluginDisplayElementStore';

export const captureEditorHistorySnapshot = (): PushHistoryInput => {
  const keyState = useKeyStore.getState();
  return {
    keyMappings: keyState.keyMappings,
    positions: keyState.positions,
    statPositions: useStatItemStore.getState().positions,
    graphPositions: useGraphItemStore.getState().positions,
    knobPositions: useKnobItemStore.getState().positions,
    pluginElements: usePluginDisplayElementStore.getState().elements,
    layerGroups: useLayerGroupStore.getState().layerGroups,
    tabs: keyState.tabs,
    selectedViewerTabs: keyState.selectedViewerTabs,
    selectedKeyType: keyState.selectedKeyType,
  };
};

export const pushEditorHistorySnapshot = (snapshot: PushHistoryInput) => {
  useHistoryStore.getState().pushState(snapshot);
};
