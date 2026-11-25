import { createContext, useContext } from 'react';

/**
 * Choosers (color, font, image, sound, animation) opened from a control
 * inside the sidebar float beside the sidebar and point at the row that
 * opened them. The sidebar's rows stay in sight and in place, and the chooser
 * never covers the control it belongs to.
 */
export interface ChooserScope {
  /** The sidebar's area: a chooser whose trigger is inside it opens beside it. */
  element: HTMLElement | null;
  setElement: (element: HTMLElement | null) => void;
}

export const ChooserScopeContext = createContext<ChooserScope | null>(null);

export const useChooserScope = () => useContext(ChooserScopeContext);
