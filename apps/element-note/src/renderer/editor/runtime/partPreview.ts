import { create } from 'zustand';

/**
 * While the sidebar is on a part of the selected key (the pointer rests on
 * its row, or its page is open), the editor canvas shows that part working:
 * the key goes down now and then, and with it its notes rise or its count
 * goes up. A part is read by what it does, not by its name.
 *
 * It is the editor canvas only. Nothing is stored or sent: the overlay and
 * the game never hear of it, and no real key is pressed.
 */
export type PreviewPart = 'look' | 'note' | 'counter';

interface PartPreviewState {
  /** What is shown; the last one asked for wins. Null: nothing is. */
  part: PreviewPart | null;
  /** The key is down in the preview. */
  pressed: boolean;
  /** Presses since the preview began, for the count. */
  presses: number;
  /** Says a part is shown; the function returned says it no longer is. */
  show: (part: PreviewPart) => () => void;
}

/** One press every so often, held for a length that changes a little. */
const BEAT_MS = 620;
const HOLD_MS = { min: 90, max: 330 } as const;
const SHORT_HOLD_MS = 130;

let nextId = 0;
const shown: { id: number; part: PreviewPart }[] = [];
let beat: ReturnType<typeof setTimeout> | null = null;
let release: ReturnType<typeof setTimeout> | null = null;

export const usePartPreviewStore = create<PartPreviewState>((set, get) => {
  const stop = () => {
    if (beat) clearTimeout(beat);
    if (release) clearTimeout(release);
    beat = release = null;
  };

  const press = () => {
    const part = get().part;
    if (!part) return;
    // Notes are as long as the key is held, so their length varies.
    const hold =
      part === 'note'
        ? HOLD_MS.min + Math.random() * (HOLD_MS.max - HOLD_MS.min)
        : SHORT_HOLD_MS;
    set((state) => ({ pressed: true, presses: state.presses + 1 }));
    release = setTimeout(() => set({ pressed: false }), hold);
    beat = setTimeout(press, BEAT_MS);
  };

  const settle = () => {
    const part = shown.at(-1)?.part ?? null;
    if (part === get().part) return;
    stop();
    set({ part, pressed: false, presses: 0 });
    // The first press comes a moment after, not on the same frame the
    // pointer arrives: passing over a row does not set the key off.
    if (part) beat = setTimeout(press, 180);
  };

  return {
    part: null,
    pressed: false,
    presses: 0,
    show: (part) => {
      const id = ++nextId;
      shown.push({ id, part });
      settle();
      return () => {
        const at = shown.findIndex((entry) => entry.id === id);
        if (at < 0) return;
        shown.splice(at, 1);
        settle();
      };
    },
  };
});

/** Whether a key is down in the preview; only a selected key ever is. */
export const usePreviewPressed = (selected: boolean) =>
  usePartPreviewStore(
    (state) => selected && state.part !== null && state.pressed,
  );

/** The count a selected key shows while its count is previewed, or null. */
export const usePreviewCount = (selected: boolean) =>
  usePartPreviewStore((state) =>
    selected && state.part === 'counter' ? state.presses : null,
  );
