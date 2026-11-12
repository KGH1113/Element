import { invoke } from '../invoke';
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
  // 키와 배치의 저장은 편집 커밋으로 보낸다: 실행 취소 한 단계로 기록된다.
  // 저장하며 정해지는 값(새 요소의 ID)은 keys:changed·positions:changed로 받는다.
  update: (mappings: KeyMappings) =>
    editorApi.commitChanges({ keys: mappings }).then(() => mappings),
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
    editorApi.replaceFields({ keyPositions: positions }).then(() => positions),
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
  // One key's count: set to a number, or reset by setting it to 0.
  setSingleCounter: (mode: string, key: string, value: number) =>
    invoke<KeyCounters>('keys_set_single_counter', { mode, key, value }),
  resetSingleCounter: (mode: string, key: string) =>
    invoke<KeyCounters>('keys_set_single_counter', { mode, key, value: 0 }),
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
