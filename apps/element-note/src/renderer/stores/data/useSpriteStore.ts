import { create } from 'zustand';
import type { SpritePositions } from '@src/types/key/sprites';
import { keepUnsavedPoses } from '@utils/sprite/unsavedPoses';

interface SpriteStoreState {
  positions: SpritePositions;
  /**
   * The editor has a change the backend has not answered yet: a drag, or a
   * save on its way. What the backend reports meanwhile is older than what is
   * on screen.
   */
  isLocalUpdateInProgress: boolean;
  /**
   * What the backend reports. Ignored while a local change is in progress;
   * poses that could not be saved yet stay on the sprites that still exist.
   */
  setPositions: (positions: SpritePositions) => void;
  /** What the editor just did. Always shown. */
  setLocalPositions: (positions: SpritePositions) => void;
  setLocalUpdateInProgress: (value: boolean) => void;
}

export const useSpriteStore = create<SpriteStoreState>((set, get) => ({
  positions: {},
  isLocalUpdateInProgress: false,
  setPositions: (positions) => {
    if (get().isLocalUpdateInProgress) return;
    set({ positions: keepUnsavedPoses(get().positions, positions) });
  },
  setLocalPositions: (positions) => set({ positions }),
  setLocalUpdateInProgress: (value) => set({ isLocalUpdateInProgress: value }),
}));
