/**
 * Snapping as the canvas uses it: the settings and held keys decide the
 * options, and what the engine found is shown as guides. Every drag on the
 * canvas goes through here, so they all behave the same.
 */

import { useSettingsStore } from '@stores/useSettingsStore';
import { useSmartGuidesStore } from '@stores/grid/useSmartGuidesStore';
import {
  calculateBounds,
  type ElementBounds,
  type GuideLine,
  type SizeMatchGuide,
  type SpacingGuide,
} from '@utils/grid/smartGuides';
import {
  snapMove,
  snapOptionsFor,
  snapResize,
  type Edges,
  type PulledSide,
  type SnapOptions,
} from '@utils/grid/snapEngine';

/** What a held key asks of a drag. */
export interface DragKeys {
  /** Alt: nothing pulls. */
  suspended: boolean;
  /** Shift while moving: only along the axis that moved more. */
  straight: boolean;
}

export const dragKeysOf = (event: {
  altKey: boolean;
  shiftKey: boolean;
}): DragKeys => ({ suspended: event.altKey, straight: event.shiftKey });

export function currentSnapOptions(
  zoom: number,
  suspended: boolean,
): SnapOptions {
  const grid = useSettingsStore.getState().gridSettings;
  return snapOptionsFor({
    zoom,
    grid: grid?.gridSnapSize,
    alignment: grid?.alignmentGuides,
    spacing: grid?.spacingGuides,
    sizeMatch: grid?.sizeMatchGuides,
    suspended,
  });
}

function showGuides(
  box: ElementBounds,
  guides: GuideLine[],
  spacing: SpacingGuide[],
  sizes: SizeMatchGuide[],
) {
  const store = useSmartGuidesStore.getState();
  if (guides.length === 0 && spacing.length === 0 && sizes.length === 0) {
    // Most frames have nothing to show; do not wake the overlay for them.
    if (store.isActive) store.clearGuides();
    return;
  }
  store.setGuides({
    draggedBounds: box,
    activeGuides: guides,
    spacingGuides: spacing,
    sizeMatchGuides: sizes,
  });
}

export const clearSnapGuides = () => {
  const store = useSmartGuidesStore.getState();
  if (store.isActive || store.draggedBounds) store.clearGuides();
};

/**
 * Moves a box by what the pointer has travelled and shows the guides.
 * `start` is the box when the drag began: one element, or the box around
 * everything selected. Returns where its top-left corner lands.
 */
export function moveWithSnap(input: {
  start: ElementBounds;
  deltaX: number;
  deltaY: number;
  targets: ElementBounds[];
  zoom: number;
  keys: DragKeys;
}): { left: number; top: number } {
  const { start, deltaX, deltaY, targets, zoom, keys } = input;
  const alongX = Math.abs(deltaX) >= Math.abs(deltaY);
  const free = keys.straight ? { x: alongX, y: !alongX } : { x: true, y: true };
  const pointed = calculateBounds(
    start.left + (free.x ? deltaX : 0),
    start.top + (free.y ? deltaY : 0),
    start.width,
    start.height,
    start.id,
  );
  const landed = snapMove(
    pointed,
    targets,
    currentSnapOptions(zoom, keys.suspended),
    free,
  );
  showGuides(
    calculateBounds(
      landed.left,
      landed.top,
      start.width,
      start.height,
      start.id,
    ),
    landed.guides,
    landed.spacingGuides,
    [],
  );
  return { left: landed.left, top: landed.top };
}

/**
 * Settles the pulled edges of a box and shows the guides. `pointed` is where
 * the pointer puts the edges; sides that are not pulled come back unchanged.
 */
export function resizeWithSnap(input: {
  pointed: Edges & { id: string };
  pulled: { x: PulledSide; y: PulledSide };
  targets: ElementBounds[];
  zoom: number;
  suspended: boolean;
  minSize: number | { x: number; y: number };
}): Edges {
  const { pointed, pulled, targets, zoom, suspended, minSize } = input;
  const landed = snapResize(
    pointed,
    pulled,
    targets,
    currentSnapOptions(zoom, suspended),
    minSize,
  );
  showGuides(
    calculateBounds(
      landed.left,
      landed.top,
      landed.right - landed.left,
      landed.bottom - landed.top,
      pointed.id,
    ),
    landed.guides,
    [],
    landed.sizeMatchGuides,
  );
  return {
    left: landed.left,
    top: landed.top,
    right: landed.right,
    bottom: landed.bottom,
  };
}
