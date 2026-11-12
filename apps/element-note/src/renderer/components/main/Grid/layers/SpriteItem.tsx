import React, { useMemo } from 'react';
import { isMac } from '@utils/core/platform';
import { useDraggable, useSmartGuidesElements } from '@hooks/Grid';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { calculateRotatedBounds } from '@utils/grid/smartGuides';
import { startSelectionDrag } from '@hooks/Grid/selectionDrag';
import { elementRotationTransform } from '@utils/element/rotation';
import { isRenderableImageRef } from '@utils/media/imageSource';
import OverlaySpriteItem from '@components/overlay/counters/OverlaySpriteItem';
import { SHELF_ICONS } from '@components/main/Sidebar/sidebarIcons';
import {
  useSpriteTryOutStore,
  withTryOutKey,
  withTryOutLook,
} from '@src/renderer/editor/runtime/spriteTryOut';
import type { CanonicalReactiveSpritePosition } from '@src/types/editor';
import '@styles/sprite-runtime.css';

interface SelectedElement {
  id: string;
  type?: string;
  index?: number;
}

interface SpriteItemProps {
  index: number;
  elementId: string;
  position: CanonicalReactiveSpritePosition;
  /** Which key each key element stands for; poses react to these. */
  keyCanonicalMap: ReadonlyMap<string, string>;
  onPositionChange: (index: number, dx: number, dy: number) => void;
  onClick?: (e: React.MouseEvent) => void;
  onCtrlClick?: (e: React.MouseEvent) => void;
  onShiftClick?: (e: React.MouseEvent) => void;
  isSelected?: boolean;
  selectedElements?: SelectedElement[];
  onMultiDrag?: (dx: number, dy: number) => void;
  onMultiDragStart?: () => void;
  onMultiDragEnd?: () => void;
  onContextMenu?: (e: React.MouseEvent) => void;
  setReferenceRef?: (node: HTMLElement | null) => void;
  zoom?: number;
  panX?: number;
  panY?: number;
  zIndex?: number;
  isViewportTransforming?: boolean;
}

/**
 * An image on the canvas. The box is what is pressed, dragged and resized;
 * inside it the image is drawn by the same code the overlay uses, so it
 * reacts to keys here as it will there.
 */
const SpriteItem = ({
  index,
  elementId,
  position,
  keyCanonicalMap,
  onPositionChange,
  onClick,
  onCtrlClick,
  onShiftClick,
  isSelected = false,
  selectedElements = [],
  onMultiDrag,
  onMultiDragStart,
  onMultiDragEnd,
  onContextMenu,
  setReferenceRef,
  zoom = 1,
  panX = 0,
  panY = 0,
  zIndex = 0,
  isViewportTransforming = false,
}: SpriteItemProps) => {
  const macOS = isMac();
  const { dx, dy, width, height, rotation } = position;

  const { getOtherElements } = useSmartGuidesElements();
  const isDraggingOrResizing = useGridSelectionStore(
    (state) => state.isDraggingOrResizing,
  );

  const draggable = useDraggable({
    initialX: dx,
    initialY: dy,
    onPositionChange: (newDx: number, newDy: number) => {
      if (!isSelected) onPositionChange(index, newDx, newDy);
    },
    zoom,
    panX,
    panY,
    elementId,
    elementWidth: width,
    elementHeight: height,
    elementRotation: rotation,
    getOtherElements,
    disabled: isSelected,
  });

  // The look of this image whose page is open in the sidebar can be tried
  // out from there: a copy of it answers to a key that only the editor holds
  // (see spriteTryOut.ts). Nothing of it is stored.
  const triedLook = useSpriteTryOutStore((state) =>
    state.target?.spriteId === position.id ? state.target.poseId : null,
  );

  // The box places and turns the image, so the image itself is drawn at the
  // box's origin. Kept until the sprite changes: a drag moves only the box.
  const image = useMemo(
    () =>
      withTryOutLook(
        { ...position, dx: 0, dy: 0, rotation: 0, hidden: false },
        triedLook,
      ),
    [position, triedLook],
  );
  const keys = useMemo(
    () => (triedLook ? withTryOutKey(keyCanonicalMap) : keyCanonicalMap),
    [keyCanonicalMap, triedLook],
  );

  if (position.hidden) return null;

  const handleSelectionDragMouseDown = (e: React.MouseEvent) => {
    if (!isSelected) return;
    startSelectionDrag({
      event: e,
      elementBox: calculateRotatedBounds(
        dx,
        dy,
        width,
        height,
        rotation,
        elementId,
      ),
      selectedElements,
      getOtherElements,
      zoom,
      onStart: onMultiDragStart,
      onMove: onMultiDrag,
      onEnd: onMultiDragEnd,
    });
  };

  const handleClick = (e: React.MouseEvent) => {
    const isPrimaryModifierPressed = macOS ? e.metaKey : e.ctrlKey;

    if (isSelected && isPrimaryModifierPressed && onCtrlClick) {
      e.stopPropagation();
      onCtrlClick(e);
      return;
    }
    if (isSelected) {
      e.stopPropagation();
      return;
    }
    if (draggable.wasMoved) return;
    if (e.shiftKey && onShiftClick) {
      e.stopPropagation();
      onShiftClick(e);
      return;
    }
    if (isPrimaryModifierPressed && onCtrlClick) {
      e.stopPropagation();
      onCtrlClick(e);
      return;
    }
    onClick?.(e);
  };

  const attachRef = (node: HTMLElement | null) => {
    if (!isSelected) draggable.ref(node);
    setReferenceRef?.(node);
  };

  return (
    <div
      ref={attachRef}
      className="dmn-sprite-item absolute select-none"
      data-sprite-item={elementId}
      style={{
        left: 0,
        top: 0,
        width: `${width}px`,
        height: `${height}px`,
        transform: `translate3d(${draggable.dx}px, ${
          draggable.dy
        }px, 0)${elementRotationTransform(rotation)}`,
        transformOrigin: '50% 50%',
        zIndex: position.zIndex ?? zIndex,
        cursor: 'pointer',
        willChange:
          isDraggingOrResizing || isViewportTransforming ? 'transform' : 'auto',
      }}
      data-editing={isDraggingOrResizing ? 'true' : undefined}
      onClick={handleClick}
      onMouseDown={isSelected ? handleSelectionDragMouseDown : undefined}
      onContextMenu={(e) => {
        e.preventDefault();
        e.stopPropagation();
        onContextMenu?.(e);
      }}
      onDragStart={(e: React.DragEvent) => e.preventDefault()}
    >
      <OverlaySpriteItem position={image} keyCanonicalMap={keys} />
      {!isRenderableImageRef(position.baseImage) && (
        // Only the editor draws this: an image that is not chosen yet still
        // has to be seen and pressed here. The overlay shows nothing.
        <div
          className="dmn-sprite-placeholder"
          aria-hidden="true"
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxSizing: 'border-box',
            border: '1px dashed var(--ui-fg-muted)',
            borderRadius: '6px',
            background: 'var(--ui-fill-faint)',
            color: 'var(--ui-fg-muted)',
            pointerEvents: 'none',
          }}
        >
          {SHELF_ICONS.sprite}
        </div>
      )}
    </div>
  );
};

export default SpriteItem;
