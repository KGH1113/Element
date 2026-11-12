import { invoke } from '../invoke';
import { subscribe } from './shared';
import { projectNativeKeyMappings } from './editorApi';
import type { NativeKeyMappings } from '@src/types/editor';

import type {
  PresetOperationResult,
  PresetSnapshot,
} from '@src/types/plugin/api';

export const presetsApi = {
  save: () => invoke<PresetOperationResult>('preset_save'),
  load: () => invoke<PresetOperationResult>('preset_load'),
  /** One key viewer: the one named, or the one being edited. */
  saveTab: (tabId?: string) =>
    invoke<PresetOperationResult>('preset_save_tab', { tabId }),
  loadTab: (tabId?: string) =>
    invoke<PresetOperationResult>('preset_load_tab', { tabId }),
  onSnapshot: (listener: (snapshot: PresetSnapshot) => void) =>
    subscribe<Omit<PresetSnapshot, 'keys'> & { keys: NativeKeyMappings }>(
      'preset:snapshot',
      (snapshot) =>
        listener({
          ...snapshot,
          keys: projectNativeKeyMappings(snapshot.keys),
        }),
    ),
};
