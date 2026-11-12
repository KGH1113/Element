import type { ReactNode, RefObject } from 'react';
import type { PreviewPart } from '@src/renderer/editor/runtime/partPreview';

/**
 * A part of an element is one row on its page. What kind of part it is says
 * where its settings live:
 *
 * - `inline`: a single value, or a few numbers. The row holds the control;
 *   there is no page for it.
 * - `chooser`: one of a set (an image, a sound). The row opens a chooser;
 *   nothing navigates.
 * - `page`: many settings. The row opens a page.
 * - `action`: something is done (one more of a thing is added). The row runs
 *   it; what it leads to, a page say, is up to the action.
 */
interface PartBase {
  id: string;
  label: string;
  icon?: ReactNode;
  /** Does not apply right now: the row stays in place, greyed out. */
  disabled?: boolean;
}

export interface InlinePart extends PartBase {
  kind: 'inline';
  /** Edits the value, in the row itself. */
  control: ReactNode;
}

/** What a chooser part is given: the row it belongs to, and how to close. */
export interface ChooserSlot {
  trigger: RefObject<HTMLButtonElement | null>;
  close: () => void;
}

export interface ChooserPart extends PartBase {
  kind: 'chooser';
  /** What is chosen now, shown on the row. */
  value?: ReactNode;
  /** The chooser, drawn while the row is open. */
  chooser: (slot: ChooserSlot) => ReactNode;
}

export interface PartToggle {
  checked: boolean;
  /** Some of the selected elements differ. */
  mixed?: boolean;
  /** What the row says while the part is on, instead of "On". */
  onLabel?: string;
  onChange: (checked: boolean) => void;
}

export interface PagePart extends PartBase {
  kind: 'page';
  /** Current values, shown on the row. */
  value?: ReactNode;
  /** Parts that can be switched off: the row says the state, the page has the switch. */
  toggle?: PartToggle;
  /** Setting groups the page shows; null shows all of them. */
  groups: readonly string[] | null;
  /**
   * A page that lists parts of its own instead of settings: the second face
   * of a key. Pages opened from it return to it.
   */
  parts?: ElementPart[][];
  /** Said under the rows of a page that lists parts. */
  hint?: string;
  /**
   * False for a page that is opened from another page (one pose, from the
   * list of poses): it has no row.
   */
  listed?: boolean;
  /**
   * The settings are shown on the element's page itself, under its rows,
   * instead of behind a row: there is no other page to tell them from.
   */
  inPlace?: boolean;
  /**
   * What the canvas shows of this part while the pointer rests on its row
   * or its page is open (editor/runtime/partPreview.ts).
   */
  preview?: PreviewPart;
}

export interface ActionPart extends PartBase {
  kind: 'action';
  /** Says what the action is for, at the right of the row. */
  value?: ReactNode;
  run: () => void;
}

export type ElementPart = InlinePart | ChooserPart | PagePart | ActionPart;

/**
 * The pages a path of part ids leads through. A page with parts of its own
 * is looked into; any other page is opened from the same list it is in. The
 * walk stops at the first id that names no page.
 */
export function resolvePartPath(
  parts: ElementPart[][],
  path: readonly string[],
): PagePart[] {
  const trail: PagePart[] = [];
  let level = parts.flat();
  for (const id of path) {
    const found = level.find(
      (part): part is PagePart => part.kind === 'page' && part.id === id,
    );
    if (!found || found.inPlace) break;
    trail.push(found);
    if (found.parts) level = found.parts.flat();
  }
  return trail;
}

/** A part that can be switched off, and is. */
export const isPartOff = (toggle: PartToggle | undefined) =>
  !!toggle && !toggle.checked && !toggle.mixed;
