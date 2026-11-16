import { create } from 'zustand';

export const SIDEBAR_WIDTH = { min: 300, default: 336, max: 560 } as const;

const WIDTH_STORAGE_KEY = 'elementnote:sidebar-width';

const clampWidth = (width: number) =>
  Math.round(Math.min(SIDEBAR_WIDTH.max, Math.max(SIDEBAR_WIDTH.min, width)));

const readWidth = (): number => {
  try {
    const stored = Number(window.localStorage.getItem(WIDTH_STORAGE_KEY));
    if (Number.isFinite(stored) && stored > 0) return clampWidth(stored);
  } catch {
    // Storage can be unavailable; the default width still works.
  }
  return SIDEBAR_WIDTH.default;
};

interface SidebarState {
  width: number;
  /** Follows the pointer while dragging; `save` keeps it for the next run. */
  setWidth: (width: number, save?: boolean) => void;
  /** Asks the canvas to delete what is selected. */
  deleteSignal: number;
  requestDelete: () => void;
}

export const useSidebarStore = create<SidebarState>((set) => ({
  width: readWidth(),
  setWidth: (width, save = false) => {
    const next = clampWidth(width);
    if (save) {
      try {
        window.localStorage.setItem(WIDTH_STORAGE_KEY, String(next));
      } catch {
        // Not saved; the width still applies to this run.
      }
    }
    set({ width: next });
  },
  deleteSignal: 0,
  requestDelete: () =>
    set((state) => ({ deleteSignal: state.deleteSignal + 1 })),
}));
