import React, { useState } from 'react';
import { isMac } from '@utils/core/platform';
import { useDraggable, useSmartGuidesElements } from '@hooks/Grid';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { calculateBounds } from '@utils/grid/smartGuides';
import { startSelectionDrag } from '@hooks/Grid/selectionDrag';
import GraphPanel from '@components/shared/GraphPanel';
import { resolveImageSource } from '@utils/core/imageSource';

interface GraphPosition {
  hidden?: boolean;
  dx?: number;
  dy?: number;
  width?: number;
  height?: number;
  className?: string;
  graphType?: string;
  graphColor?: string;
  showAvgLine?: boolean;
  graphAnimationEnabled?: boolean;
  backgroundColor?: string;
  borderColor?: string;
  borderWidth?: number;
  borderRadius?: number;
  inactiveImage?: string;
  activeImage?: string;
  idleImageFit?: string;
  imageFit?: string;
  useInlineStyles?: boolean;
  zIndex?: number;
}

interface SelectedElement {
  id: string;
  type?: string;
  index?: number;
}

interface GraphItemProps {
  index: number;
  elementId?: string;
  position: GraphPosition;
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

const PREVIEW_HISTORY_BASE: number[] = [
  8, 10, 11, 13, 12, 14, 15, 16, 14, 13, 12, 14, 15, 14, 14,
];
const PREVIEW_AVG = 12;
const PREVIEW_MAX = 18;

const GraphItem = ({
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
}: GraphItemProps) => {
  const macOS = isMac();
  const {
    dx = 0,
    dy = 0,
    width = 200,
    height = 100,
    className,
    graphType = 'line',
    graphColor = '#86EFAC',
    showAvgLine = true,
    graphAnimationEnabled = true,
    backgroundColor,
    borderColor,
    borderWidth,
    borderRadius,
    inactiveImage,
    activeImage,
    idleImageFit,
    imageFit,
    useInlineStyles = false,
  } = position ?? ({} as Partial<GraphPosition>);

  const { getOtherElements } = useSmartGuidesElements();
  const isDraggingOrResizing = useGridSelectionStore(
    (state: { isDraggingOrResizing: boolean }) => state.isDraggingOrResizing,
  );

  const isSelectionMode = isSelected;
  const effectiveElementId = elementId || `graph-${index}`;

  const previewHistory = [...PREVIEW_HISTORY_BASE];
  const previewImageSrc =
    resolveImageSource(inactiveImage) ||
    resolveImageSource(activeImage) ||
    null;
  const previewImageFit = idleImageFit || imageFit || 'cover';

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
    elementWidth: width || 200,
    elementHeight: height || 100,
    getOtherElements,
    disabled: isSelectionMode,
  });
  const [uid] = useState(
    () => `graph-preview-${Math.random().toString(36).slice(2, 11)}`,
  );

  if (position?.hidden) return null;

  const handleSelectionDragMouseDown = (e: React.MouseEvent) => {
    if (!isSelectionMode) return;
    startSelectionDrag({
      event: e,
      elementBox: calculateBounds(
        dx,
        dy,
        width || 200,
        height || 100,
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

  return (
    <GraphPanel
      visualPosition={position}
      ref={attachRef}
      dx={draggable.dx}
      dy={draggable.dy}
      width={width}
      height={height}
      zIndex={position.zIndex ?? zIndex}
      className={className}
      graphType={graphType as 'line' | 'bar'}
      graphColor={graphColor || '#86EFAC'}
      showAvgLine={showAvgLine}
      animationEnabled={graphAnimationEnabled ?? true}
      backgroundColor={backgroundColor}
      borderColor={borderColor}
      borderWidth={borderWidth}
      borderRadius={borderRadius}
      imageSrc={previewImageSrc}
      imageFit={previewImageFit}
      useInlineStyles={useInlineStyles}
      history={previewHistory}
      avg={PREVIEW_AVG}
      maxval={PREVIEW_MAX}
      uid={uid}
      withOffsetVars={true}
      interactive={true}
      dataEditing={isDraggingOrResizing}
      isViewportTransforming={isViewportTransforming}
      onClick={handleClick}
      onMouseDown={isSelectionMode ? handleSelectionDragMouseDown : undefined}
      onContextMenu={handleContextMenu}
      onDragStart={(e: React.DragEvent) => e.preventDefault()}
    />
  );
};

export default GraphItem;
