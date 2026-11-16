/**
 * Dragging what is selected. Keys, stats, graphs, knobs and plugin elements
 * all start it the same way, so a selection moves the same whichever of its
 * elements the pointer holds.
 */

import type React from 'react';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import {
  calculateGroupBounds,
  type ElementBounds,
} from '@utils/grid/smartGuides';
import { DRAG_THRESHOLD } from './constants';
import { clearSnapGuides, dragKeysOf, moveWithSnap } from './snapSession';

/** An element of the selection, as far as moving it needs to know. */
interface SelectedElement {
  type?: string;
  id: string;
  index?: number;
}

export interface SelectionDragInput {
  /** The mouse press on an element of the selection. */
  event: React.MouseEvent;
  /** The pressed element's box, as snapping sees it. */
  elementBox: ElementBounds;
  selectedElements: SelectedElement[];
  getOtherElements: (excludeId: string) => ElementBounds[];
  zoom: number;
  /** Called once, when the pointer has really started to drag. */
  onStart?: () => void;
  /** Called with how much further the selection moved since the last call. */
  onMove?: (deltaX: number, deltaY: number) => void;
  onEnd?: () => void;
}

const snapIdOf = (selected: SelectedElement) =>
  selected.type === 'key' && selected.index !== undefined
    ? `key-${selected.index}`
    : selected.id;

export function startSelectionDrag({
  event,
  elementBox,
  selectedElements,
  getOtherElements,
  zoom,
  onStart,
  onMove,
  onEnd,
}: SelectionDragInput) {
  if (event.button !== 0) return;
  event.preventDefault();
  event.stopPropagation();
  clearSnapGuides();

  // Where everything stood when the press came. The selection snaps as one
  // box; what is not selected is what it snaps to.
  const others = getOtherElements(elementBox.id);
  const selectedIds = new Set(selectedElements.map(snapIdOf));
  const members = [
    elementBox,
    ...others.filter((other) => selectedIds.has(other.id)),
  ];
  const targets = others.filter((other) => !selectedIds.has(other.id));
  const start = calculateGroupBounds(members) ?? elementBox;

  const pressX = event.clientX;
  const pressY = event.clientY;
  let pointer: MouseEvent | null = null;
  let dragging = false;
  let movedX = 0;
  let movedY = 0;
  let frame: number | null = null;

  const follow = () => {
    frame = null;
    if (!pointer || !dragging) return;
    const landed = moveWithSnap({
      start,
      deltaX: (pointer.clientX - pressX) / zoom,
      deltaY: (pointer.clientY - pressY) / zoom,
      targets,
      zoom,
      keys: dragKeysOf(pointer),
    });
    // Whole units, so elements of a selection never drift apart.
    const totalX = Math.round(landed.left - start.left);
    const totalY = Math.round(landed.top - start.top);
    if (totalX !== movedX || totalY !== movedY) {
      onMove?.(totalX - movedX, totalY - movedY);
      movedX = totalX;
      movedY = totalY;
    }
  };

  const handleMouseMove = (moveEvent: MouseEvent) => {
    pointer = moveEvent;
    if (!dragging) {
      // A press that barely moves is a click, not a drag.
      if (
        Math.abs(moveEvent.clientX - pressX) <= DRAG_THRESHOLD &&
        Math.abs(moveEvent.clientY - pressY) <= DRAG_THRESHOLD
      )
        return;
      dragging = true;
      useGridSelectionStore.getState().setDraggingOrResizing(true);
      onStart?.();
    }
    if (frame === null) frame = requestAnimationFrame(follow);
  };

  const handleMouseUp = () => {
    document.removeEventListener('mousemove', handleMouseMove);
    document.removeEventListener('mouseup', handleMouseUp);
    window.removeEventListener('blur', handleMouseUp);
    if (frame !== null) {
      cancelAnimationFrame(frame);
      // Let go where the pointer is, not where the last frame left it.
      follow();
    }
    clearSnapGuides();
    if (!dragging) return;
    useGridSelectionStore.getState().setDraggingOrResizing(false);
    onEnd?.();
  };

  document.addEventListener('mousemove', handleMouseMove);
  document.addEventListener('mouseup', handleMouseUp);
  window.addEventListener('blur', handleMouseUp);
}
