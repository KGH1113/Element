import React from 'react';
import { isMac } from '@utils/core/platform';
import { useDraggable, useSmartGuidesElements } from '@hooks/Grid';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { calculateBounds } from '@utils/grid/smartGuides';
import { startSelectionDrag } from '@hooks/Grid/selectionDrag';
import { resolveImageSource } from '@utils/core/imageSource';
import {
  computeKeyElementStyles,
  type KeyElementPosition,
} from '@hooks/overlay/useKeyElementStyles';

interface KnobPosition extends Partial<KeyElementPosition> {
  hidden?: boolean;
  dx?: number;
  dy?: number;
  width?: number;
  height?: number;
  className?: string;
  backgroundColor?: string;
  borderColor?: string;
  borderWidth?: number;
  borderRadius?: number;
  inactiveImage?: string;
  activeImage?: string;
  idleImageFit?: string;
  imageFit?: string;
  zIndex?: number;
}

interface SelectedElement {
  id: string;
  type?: string;
  index?: number;
}

interface KnobItemProps {
  index: number;
  elementId?: string;
  position: KnobPosition;
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

const KnobItem = ({
  index,
  elementId,
  position,
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
}: KnobItemProps) => {
  const macOS = isMac();
  const {
    dx = 0,
    dy = 0,
    width = 60,
    height = 60,
    className,
    backgroundColor,
    borderColor,
    borderWidth,
    borderRadius,
    inactiveImage,
    activeImage,
    idleImageFit,
    imageFit,
  } = position ?? ({} as Partial<KnobPosition>);

  const { getOtherElements } = useSmartGuidesElements();
  const isDraggingOrResizing = useGridSelectionStore(
    (state: { isDraggingOrResizing: boolean }) => state.isDraggingOrResizing,
  );

  const isSelectionMode = isSelected;
  const effectiveElementId = elementId || `knob-${index}`;

  const imageSrc =
    resolveImageSource(inactiveImage) ||
    resolveImageSource(activeImage) ||
    null;
  const resolvedFit = (idleImageFit ||
    imageFit ||
    'cover') as React.CSSProperties['objectFit'];

  const draggable = useDraggable({
    initialX: dx,
    initialY: dy,
    onPositionChange: (newDx: number, newDy: number) => {
      if (!isSelectionMode) {
        onPositionChange(index, newDx, newDy);
      }
    },
    zoom,
    panX,
    panY,
    elementId: effectiveElementId,
    elementWidth: width || 60,
    elementHeight: height || 60,
    getOtherElements,
    disabled: isSelectionMode,
  });

  if (position?.hidden) return null;

  const handleSelectionDragMouseDown = (e: React.MouseEvent) => {
    if (!isSelectionMode) return;
    startSelectionDrag({
      event: e,
      elementBox: calculateBounds(
        dx,
        dy,
        width || 60,
        height || 60,
        effectiveElementId,
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
    const isShiftPressed = e.shiftKey;

    if (isSelectionMode && isPrimaryModifierPressed && onCtrlClick) {
      e.stopPropagation();
      onCtrlClick(e);
      return;
    }

    if (isSelectionMode) {
      e.stopPropagation();
      return;
    }

    if (!draggable.wasMoved) {
      if (isShiftPressed && onShiftClick) {
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
    }
  };

  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onContextMenu?.(e);
  };

  const attachRef = (node: HTMLElement | null) => {
    if (!isSelectionMode) {
      draggable.ref(node);
    }
    if (typeof setReferenceRef === 'function') {
      setReferenceRef(node);
    }
  };

  const transform = `translate3d(calc(${draggable.dx}px + var(--key-offset-x, 0px)), calc(${draggable.dy}px + var(--key-offset-y, 0px)), 0)`;
  const v2 = computeKeyElementStyles({
    position: { ...position, dx, dy, width, height },
    active: false,
    label: '',
  });

  return (
    <div
      ref={attachRef}
      className={`absolute select-none ${className || ''}`}
      style={{
        width: `${width}px`,
        height: `${height}px`,
        transform: position.rotation
          ? `${transform} rotate(${position.rotation}deg)`
          : transform,
        zIndex: position.zIndex ?? zIndex,
        cursor: 'pointer',
        willChange:
          isDraggingOrResizing || isViewportTransforming ? 'transform' : 'auto',
        contain: 'layout style paint',
      }}
      data-editing={isDraggingOrResizing ? 'true' : undefined}
      onClick={handleClick}
      onMouseDown={isSelectionMode ? handleSelectionDragMouseDown : undefined}
      onContextMenu={handleContextMenu}
      onDragStart={(e: React.DragEvent) => e.preventDefault()}
    >
      <div
        style={{
          width: '100%',
          height: '100%',
          // 모서리 반경 미지정 시 원형 유지 (px 지정 시 키와 동일한 px 단위)
          borderRadius: borderRadius != null ? `${borderRadius}px` : '50%',
          overflow: 'hidden',
          position: 'relative',
          background: backgroundColor || 'rgba(46, 46, 47, 0.9)',
          border:
            borderWidth && borderWidth > 0
              ? `${borderWidth}px solid ${
                  borderColor || 'rgba(113, 113, 113, 0.9)'
                }`
              : `1px solid ${borderColor || 'rgba(113, 113, 113, 0.9)'}`,
          boxSizing: 'border-box',
          ...(v2.keyStyle.backgroundImage
            ? { background: v2.keyStyle.backgroundImage }
            : {}),
          ...(v2.borderRingStyle
            ? { border: 'none', padding: v2.keyStyle.padding }
            : {}),
          boxShadow: v2.keyStyle.boxShadow,
        }}
      >
        {v2.borderRingStyle && (
          <span aria-hidden="true" style={v2.borderRingStyle} />
        )}
        {imageSrc ? (
          <img
            src={imageSrc}
            alt=""
            draggable={false}
            style={{
              width: '100%',
              height: '100%',
              objectFit: resolvedFit,
              transform: v2.imageStyle.transform,
              pointerEvents: 'none',
              userSelect: 'none',
            }}
          />
        ) : (
          <div
            style={{
              position: 'absolute',
              top: '12%',
              left: '50%',
              width: '8%',
              height: '76%',
              transform: 'translateX(-50%)',
              background: borderColor || 'rgba(113, 113, 113, 0.9)',
              borderRadius: '4px',
            }}
          />
        )}
      </div>
    </div>
  );
};

export default KnobItem;
