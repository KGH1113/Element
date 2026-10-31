import { create } from 'zustand';
import type {
  KeyMappings,
  KeyPositions,
  KeyViewerTab,
  SelectedViewerTabs,
} from '@src/types/key/keys';

interface KeyStoreState {
  selectedKeyType: string;
  tabs: KeyViewerTab[];
  selectedViewerTabs: SelectedViewerTabs;
  keyMappings: KeyMappings;
  positions: KeyPositions;
  isBootstrapped: boolean;
  // 삭제 작업 중 백엔드 이벤트 무시용 플래그
  isLocalUpdateInProgress: boolean;
  setSelectedKeyType: (mode: string) => void;
  setTabs: (tabs: KeyViewerTab[]) => void;
  setSelectedViewerTabs: (value: SelectedViewerTabs) => void;
  setKeyMappings: (mappings: KeyMappings) => void;
  setPositions: (positions: KeyPositions) => void;
  setBootstrapped: (value: boolean) => void;
  setKeyMappingsAndPositions: (
    mappings: KeyMappings,
    positions: KeyPositions,
  ) => void;
  setLocalUpdateInProgress: (value: boolean) => void;
}

export const useKeyStore = create<KeyStoreState>((set, get) => ({
  selectedKeyType: 'hand-default',
  tabs: [],
  selectedViewerTabs: { hand: 'hand-default', foot: 'foot-default' },
  keyMappings: {} as KeyMappings,
  positions: {} as KeyPositions,
  isBootstrapped: false,
  isLocalUpdateInProgress: false,
  setSelectedKeyType: (mode) => {
    const tab = get().tabs.find((candidate) => candidate.id === mode);
    set((state) => ({
      selectedKeyType: mode,
      selectedViewerTabs: tab
        ? { ...state.selectedViewerTabs, [tab.viewerKind]: mode }
        : state.selectedViewerTabs,
    }));
    if (get().isBootstrapped && typeof window !== 'undefined') {
      const request = tab
        ? window.api.keys.tabs.select(tab.viewerKind, mode)
        : window.api.keys.setMode(mode);
      request.catch((error) => {
        console.error('Failed to set key mode', error);
      });
    }
  },
  setTabs: (tabs) => set({ tabs }),
  setSelectedViewerTabs: (selectedViewerTabs) => set({ selectedViewerTabs }),
  setKeyMappings: (mappings) => set({ keyMappings: mappings }),
  setPositions: (positions) => set({ positions }),
  setBootstrapped: (value) => set({ isBootstrapped: value }),
  // 일괄 업데이트 (키 삭제 등에서 atomic 업데이트 필요)
  setKeyMappingsAndPositions: (mappings, positions) =>
    set({ keyMappings: mappings, positions }),
  setLocalUpdateInProgress: (value) => set({ isLocalUpdateInProgress: value }),
}));
