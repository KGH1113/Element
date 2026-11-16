import { create } from 'zustand';

/**
 * A look a key only has while it is pressed (its pressed colours) cannot be
 * seen while it is being chosen: nobody is pressing the key. So while a
 * chooser of such a look is open, the editor draws the selected keys as
 * pressed. It is the editor canvas only; nothing is stored or sent.
 */
interface PressedPreviewState {
  /** How many choosers of a pressed look are open. */
  open: number;
  /** Says one has opened; the function returned says it has closed. */
  begin: () => () => void;
}

export const usePressedPreviewStore = create<PressedPreviewState>((set) => ({
  open: 0,
  begin: () => {
    set((state) => ({ open: state.open + 1 }));
    let ended = false;
    return () => {
      if (ended) return;
      ended = true;
      set((state) => ({ open: Math.max(0, state.open - 1) }));
    };
  },
}));

/** Whether a key is drawn as pressed although nobody presses it. */
export const useShownPressed = (selected: boolean) =>
  usePressedPreviewStore((state) => selected && state.open > 0);
