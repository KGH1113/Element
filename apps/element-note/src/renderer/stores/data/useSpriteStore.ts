import { create } from 'zustand';
import type { SpritePositions } from '@src/types/key/sprites';

interface SpriteStoreState {
  positions: SpritePositions;
  setPositions: (positions: SpritePositions) => void;
}

export const useSpriteStore = create<SpriteStoreState>((set) => ({
  positions: {},
  setPositions: (positions) => set({ positions }),
}));
