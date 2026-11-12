import { create } from 'zustand';
import type { KeyPosition } from '@src/types/key/keys';

/**
 * While the pointer rests on one of a few looks (PictureSegments), the
 * editor canvas draws the selected element as if that look were chosen:
 * where its notes rise, where its count sits. Moving off puts it back;
 * pressing is what chooses.
 *
 * It is drawn over the element, never written into it: no store changes,
 * nothing is saved, nothing goes into undo, and the overlay and the game
 * never hear of it.
 */
export type ChoiceTarget = { type: 'key' | 'stat'; index: number };
export type ChoiceChanges = Partial<KeyPosition>;

interface ChoicePreviewState {
  target: ChoiceTarget | null;
  changes: ChoiceChanges | null;
  /** Draws `changes` over the target, or stops drawing them (null). */
  show: (target: ChoiceTarget, changes: ChoiceChanges | null) => void;
}

export const useChoicePreviewStore = create<ChoicePreviewState>((set) => ({
  target: null,
  changes: null,
  show: (target, changes) =>
    set(changes ? { target, changes } : { target: null, changes: null }),
}));

/** What the canvas draws over this element right now, if anything. */
export const useChoiceChanges = (
  type: ChoiceTarget['type'],
  index: number,
): ChoiceChanges | null =>
  useChoicePreviewStore((state) =>
    state.target?.type === type && state.target.index === index
      ? state.changes
      : null,
  );

/** The element as the canvas draws it: what is stored, with any changes. */
export function useShownPosition<T extends object>(
  type: ChoiceTarget['type'],
  index: number,
  position: T,
): T {
  const changes = useChoiceChanges(type, index);
  return changes ? { ...position, ...changes } : position;
}
