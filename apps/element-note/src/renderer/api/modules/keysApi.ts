import { invoke, trackEditorSave } from '../invoke';
import { subscribe } from './shared';
import { editorApi, projectNativeKeyMappings } from './editorApi';
import { rawKeyEventBus } from '@utils/core/rawKeyEventBus';

import type {
  KeyCounterUpdate,
  KeysModeResponse,
  KeysResetAllResponse,
  Unsubscribe,
  ModeChangePayload,
  TabsChangePayload,
  KeyStatePayload,
  KeysResetPayload,
  TabResult,
  TabDeleteResult,
  TabMutationResult,
  RawInputPayload,
} from '@src/types/plugin/api';
import type {
  KeyViewerKind,
  KeyViewerTab,
  SelectedViewerTabs,
  KeyMappings,
  KeyPositions,
  KeyCounters,
} from '@src/types/key/keys';

export const keysApi = {
  get: () => invoke<KeyMappings>('keys_get'),
  getCounters: () => invoke<KeyCounters>('keys_get_counters'),
  update: (mappings: KeyMappings) =>
    trackEditorSave(invoke<KeyMappings>('keys_update', { mappings }), [
      'editor:keys',
    ]),
  updateWithPositions: async (
    mappings: KeyMappings,
    positions: KeyPositions,
  ) => {
    await editorApi.commitChanges({ keys: mappings, keyPositions: positions });
    const snapshot = await editorApi.get();
    return {
      keys: projectNativeKeyMappings(snapshot.document.keys),
      positions: snapshot.document.keyPositions as KeyPositions,
    };
  },
  getPositions: () => invoke<KeyPositions>('positions_get'),
  updatePositions: (positions: KeyPositions) =>
    trackEditorSave(invoke<KeyPositions>('positions_update', { positions }), [
      'editor:keyPositions',
    ]),
  setMode: (mode: string) =>
    invoke<KeysModeResponse>('keys_set_mode', { mode }),
  resetAll: () => invoke<KeysResetAllResponse>('keys_reset_all'),
  resetMode: (mode: string) =>
    invoke<KeysModeResponse>('keys_reset_mode', { mode }),
  setCounters: (counters: KeyCounters) =>
    invoke<KeyCounters>('keys_set_counters', { counters }),
  resetCounters: () => invoke<KeyCounters>('keys_reset_counters'),
  resetCountersMode: (mode: string) =>
    invoke<KeyCounters>('keys_reset_counters_mode', { mode }),
  resetSingleCounter: (mode: string, key: string) =>
    invoke<KeyCounters>('keys_reset_single_counter', { mode, key }),
  onChanged: (listener: (keys: KeyMappings) => void) =>
    subscribe<KeyMappings>('keys:changed', listener),
  onPositionsChanged: (listener: (positions: KeyPositions) => void) =>
    subscribe<KeyPositions>('positions:changed', listener),
  onModeChanged: (listener: (payload: ModeChangePayload) => void) =>
    subscribe<ModeChangePayload>('keys:mode-changed', listener),
  onKeyState: (listener: (payload: KeyStatePayload) => void) =>
    subscribe<KeyStatePayload>('keys:state', listener),
  onKeysReset: (listener: (payload: KeysResetPayload) => void) =>
    subscribe<KeysResetPayload>('keys:reset', listener),
  onRawInput: (listener: (payload: RawInputPayload) => void): Unsubscribe =>
    rawKeyEventBus.subscribe(listener),
  onCounterChanged: (listener: (payload: KeyCounterUpdate) => void) =>
    subscribe<KeyCounterUpdate>('keys:counter', listener),
  onCountersChanged: (listener: (payload: KeyCounters) => void) =>
    subscribe<KeyCounters>('keys:counters', listener),
  tabs: {
    list: (viewerKind: KeyViewerKind) =>
      invoke<KeyViewerTab[]>('tabs_list', { viewerKind }),
    create: (viewerKind: KeyViewerKind, name: string) =>
      invoke<TabResult>('tabs_create', { viewerKind, name }),
    rename: (viewerKind: KeyViewerKind, id: string, name: string) =>
      invoke<TabMutationResult>('tabs_rename', { viewerKind, id, name }),
    reorder: (viewerKind: KeyViewerKind, orderedIds: string[]) =>
      invoke<TabMutationResult>('tabs_reorder', { viewerKind, orderedIds }),
    delete: (id: string) => invoke<TabDeleteResult>('tabs_delete', { id }),
    select: (viewerKind: KeyViewerKind, id: string) =>
      invoke<TabDeleteResult>('tabs_select', { viewerKind, id }),
    restore: (
      tabs: KeyViewerTab[],
      selectedKeyType: string,
      selectedViewerTabs: SelectedViewerTabs,
    ) =>
      invoke<void>('tabs_restore', {
        tabs,
        selectedKeyType,
        selectedViewerTabs,
      }),
    onChanged: (listener: (payload: TabsChangePayload) => void) =>
      subscribe<TabsChangePayload>('tabs:changed', listener),
  },
};
