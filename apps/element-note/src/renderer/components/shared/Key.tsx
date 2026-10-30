import React, { useRef, useEffect } from 'react';
import { getKeyCounterSignal } from '@stores/signals/keyCounterSignals';
import { useSignals } from '@preact/signals-react/runtime';
import { isMac } from '@utils/core/platform';
import { useDraggable } from '@hooks/Grid';
import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';
import {
  createDefaultCounterSettings,
  normalizeCounterSettings,
  type KeyCounterSettings,
} from '@src/types/key/keys';
import { useSmartGuidesElements } from '@hooks/Grid';
import { useSmartGuidesStore } from '@stores/grid/useSmartGuidesStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { warmupImageSource } from '@utils/core/imageWarmup';
import {
  computeKeyElementStyles,
  type KeyElementPosition,
} from '@hooks/overlay/useKeyElementStyles';
import { useKeyActive } from '@hooks/overlay/useKeyActive';
import { getKeyPressOrder } from '@stores/signals/keySignals';
import { isCoveredByPair } from '@src/renderer/editor/model/ghostKeys';
import InsideCounterLayout from '@components/overlay/counters/InsideCounterLayout';
import CountDisplay from '@components/overlay/counters/CountDisplay';
import {
  calculateBounds,
  calculateSnapPoints,
  calculateGroupBounds,
  type ElementBounds,
} from '@utils/grid/smartGuides';

// DraggableKey에서 counter가 KeyCounterSettings 타입인 확장 position
interface KeyPosition extends KeyElementPosition {
  counter?: KeyCounterSettings;
}

interface SelectedElement {
  id: string;
  type?: string;
  index?: number;
}

interface DraggableKeyProps {
  index: number;
  elementId?: string;
  position: KeyPosition;
  keyName: string;
  onPositionChange: (index: number, dx: number, dy: number) => void;
  onClick?: (e: React.MouseEvent) => void;
  onCtrlClick?: (e: React.MouseEvent) => void;
  onShiftClick?: (e: React.MouseEvent) => void;
  isSelected?: boolean;
  selectedElements?: SelectedElement[];
  onMultiDrag?: (dx: number, dy: number) => void;
  onMultiDragStart?: () => void;
  onMultiDragEnd?: () => void;
  activeTool?: string;
  onEraserClick?: () => void;
  onContextMenu?: (e: React.MouseEvent) => void;
  setReferenceRef?: (node: HTMLElement | null) => void;
  zoom?: number;
  panX?: number;
  panY?: number;
  zIndex?: number;
  isViewportTransforming?: boolean;
  counterEnabled?: boolean;
  counterPreviewValue?: number;
  counterValueSignal?: { value: number };
}

interface KeyProps {
  keyName: string;
  globalKey: string;
  /** The ghost or owner drawn in the same place; the newer press is shown. */
  pairedKey?: string;
  position: KeyElementPosition;
  mode?: string;
  counterEnabled?: boolean;
}

const DraggableKey = React.memo(
  ({
    index,
    elementId,
    position,
    keyName,
    onPositionChange,
    onClick,
    onCtrlClick,
    onShiftClick,
    isSelected = false,
    selectedElements = [],
    onMultiDrag,
    onMultiDragStart,
    onMultiDragEnd,
    activeTool,
    onEraserClick,
    onContextMenu,
    setReferenceRef,
    zoom = 1,
    panX = 0,
    panY = 0,
    zIndex = 0,
    isViewportTransforming = false,
    counterEnabled = false,
    counterPreviewValue = 0,
    counterValueSignal,
  }: DraggableKeyProps) => {
    useSignals();

    const macOS = isMac();
    const { displayName } = getKeyInfoByGlobalKey(keyName);
    const active = useKeyActive(keyName);
    const { dx, dy, width, height = 60, className, counter } = position;

    const {
      keyStyle: visualKeyStyle,
      imageStyle,
      textStyle,
      currentImageSrc,
      hasCurrentImage,
      isTransparent,
      labelText,
    } = computeKeyElementStyles({ position, active, label: displayName });

    const counterSettings = normalizeCounterSettings(
      counter ?? createDefaultCounterSettings(),
    );

    const showInsideCounter =
      counterEnabled &&
      counterSettings.enabled &&
      counterSettings.placement === 'inside';

    const { getOtherElements } = useSmartGuidesElements();

    const gridSnapSize = useSettingsStore(
      (state) => state.gridSettings?.gridSnapSize || 5,
    );

    const isDraggingOrResizing = useGridSelectionStore(
      (state) => state.isDraggingOrResizing,
    );

    const multiDragRef = useRef<{
      isDragging: boolean;
      startX: number;
      startY: number;
      lastSnappedDeltaX: number;
      lastSnappedDeltaY: number;
    }>({
      isDragging: false,
      startX: 0,
      startY: 0,
      lastSnappedDeltaX: 0,
      lastSnappedDeltaY: 0,
    });
    const nodeRef = useRef<HTMLElement | null>(null);
    const effectiveElementId = elementId || `key-${index}`;

    const isSelectionMode = isSelected;

    const draggable = useDraggable({
      gridSize: gridSnapSize,
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

    const handleSelectionDragMouseDown = (e: React.MouseEvent) => {
      if (!isSelectionMode || e.button !== 0) return;

      e.preventDefault();
      e.stopPropagation();

      useSmartGuidesStore.getState().clearGuides();

      useGridSelectionStore.getState().setDraggingOrResizing(true);

      onMultiDragStart?.();

      const startDx = dx;
      const startDy = dy;
      const currentWidth = width || 60;
      const currentHeight = height || 60;
      const currentElementId = effectiveElementId;

      multiDragRef.current = {
        isDragging: true,
        startX: e.clientX,
        startY: e.clientY,
        lastSnappedDeltaX: 0,
        lastSnappedDeltaY: 0,
      };

      let rafId: number | null = null;
      let dragEnded = false;
      const smartGuidesStore = useSmartGuidesStore.getState();

      const handleMouseMove = (moveEvent: MouseEvent) => {
        if (!multiDragRef.current.isDragging || dragEnded) return;

        if (rafId) return;
        rafId = requestAnimationFrame(() => {
          rafId = null;

          if (dragEnded) return;

          const currentZoom = zoom;
          const rawDeltaX =
            (moveEvent.clientX - multiDragRef.current.startX) / currentZoom;
          const rawDeltaY =
            (moveEvent.clientY - multiDragRef.current.startY) / currentZoom;

          const newX = startDx + rawDeltaX;
          const newY = startDy + rawDeltaY;

          const gridSettings = useSettingsStore.getState().gridSettings;
          const alignmentGuidesEnabled =
            gridSettings?.alignmentGuides !== false;
          const spacingGuidesEnabled = gridSettings?.spacingGuides !== false;

          const otherElements = getOtherElements(currentElementId);

          const nonSelectedElements = otherElements.filter(
            (el) => !selectedElements.some((sel) => sel.id === el.id),
          );

          const draggedBounds = calculateBounds(
            newX,
            newY,
            currentWidth,
            currentHeight,
            currentElementId,
          );

          let groupBounds: ElementBounds | null = null;
          if (selectedElements.length > 1) {
            const selectedBoundsArray = selectedElements
              .map((sel) => {
                if (
                  sel.id === currentElementId ||
                  (sel.type === 'key' && sel.index === index)
                ) {
                  return draggedBounds;
                }
                const found = otherElements.find((el) => el.id === sel.id);
                if (found) {
                  return calculateBounds(
                    found.left + rawDeltaX,
                    found.top + rawDeltaY,
                    found.width,
                    found.height,
                    found.id,
                  );
                }
                return null;
              })
              .filter(Boolean);
            groupBounds = calculateGroupBounds(selectedBoundsArray);
          }

          const snapTargetBounds =
            selectedElements.length > 1 && groupBounds
              ? groupBounds
              : draggedBounds;

          const snapResult = alignmentGuidesEnabled
            ? calculateSnapPoints(
                snapTargetBounds,
                nonSelectedElements,
                undefined,
                {
                  groupBounds,
                  disableSpacing: !spacingGuidesEnabled,
                },
              )
            : null;

          let finalX: number;
          let finalY: number;

          if (snapResult?.didSnapX) {
            if (selectedElements.length > 1 && groupBounds) {
              const groupSnapDeltaX = snapResult.snappedX - groupBounds.left;
              finalX = newX + groupSnapDeltaX;
            } else {
              finalX = snapResult.snappedX;
            }
          } else {
            const snapSize = gridSettings?.gridSnapSize || 5;
            finalX = Math.round(newX / snapSize) * snapSize;
          }

          if (snapResult?.didSnapY) {
            if (selectedElements.length > 1 && groupBounds) {
              const groupSnapDeltaY = snapResult.snappedY - groupBounds.top;
              finalY = newY + groupSnapDeltaY;
            } else {
              finalY = snapResult.snappedY;
            }
          } else {
            const snapSize = gridSettings?.gridSnapSize || 5;
            finalY = Math.round(newY / snapSize) * snapSize;
          }

          const snappedDeltaX = Math.round(finalX - startDx);
          const snappedDeltaY = Math.round(finalY - startDy);

          if (snapResult && (snapResult.didSnapX || snapResult.didSnapY)) {
            const displayBounds =
              selectedElements.length > 1 && groupBounds
                ? calculateBounds(
                    groupBounds.left +
                      (snapResult.didSnapX
                        ? snapResult.snappedX - groupBounds.left
                        : 0),
                    groupBounds.top +
                      (snapResult.didSnapY
                        ? snapResult.snappedY - groupBounds.top
                        : 0),
                    groupBounds.width,
                    groupBounds.height,
                    'group',
                  )
                : calculateBounds(
                    finalX,
                    finalY,
                    currentWidth,
                    currentHeight,
                    currentElementId,
                  );
            smartGuidesStore.setDraggedBounds(displayBounds);
            smartGuidesStore.setActiveGuides(snapResult.guides);

            if (
              spacingGuidesEnabled &&
              snapResult.spacingGuides &&
              snapResult.spacingGuides.length > 0
            ) {
              smartGuidesStore.setSpacingGuides(snapResult.spacingGuides);
            } else {
              smartGuidesStore.setSpacingGuides([]);
            }
          } else {
            smartGuidesStore.clearGuides();
          }

          const moveDeltaX =
            snappedDeltaX - multiDragRef.current.lastSnappedDeltaX;
          const moveDeltaY =
            snappedDeltaY - multiDragRef.current.lastSnappedDeltaY;

          if (moveDeltaX !== 0 || moveDeltaY !== 0) {
            multiDragRef.current.lastSnappedDeltaX = snappedDeltaX;
            multiDragRef.current.lastSnappedDeltaY = snappedDeltaY;
            onMultiDrag?.(moveDeltaX, moveDeltaY);
          }
        });
      };

      const handleMouseUp = () => {
        dragEnded = true;
        multiDragRef.current.isDragging = false;

        if (rafId) {
          cancelAnimationFrame(rafId);
          rafId = null;
        }

        document.removeEventListener('mousemove', handleMouseMove);
        document.removeEventListener('mouseup', handleMouseUp);
        window.removeEventListener('blur', handleMouseUp);
        useSmartGuidesStore.getState().clearGuides();
        useGridSelectionStore.getState().setDraggingOrResizing(false);
        onMultiDragEnd?.();
      };

      document.addEventListener('mousemove', handleMouseMove);
      document.addEventListener('mouseup', handleMouseUp);
      window.addEventListener('blur', handleMouseUp);
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

      if (activeTool === 'eraser') {
        onEraserClick?.();
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
        if (onClick) {
          onClick(e);
        }
      }
    };

    const handleContextMenu = (e: React.MouseEvent) => {
      e.preventDefault();
      e.stopPropagation();
      onContextMenu?.(e);
    };

    const renderDx = draggable.dx;
    const renderDy = draggable.dy;

    const shouldPromoteTransformLayer =
      isDraggingOrResizing || isViewportTransforming;

    const keyStyle = {
      ...visualKeyStyle,
      transform: `translate(calc(${renderDx}px + var(--key-offset-x, 0px)), calc(${renderDy}px + var(--key-offset-y, 0px)))`,
      willChange: shouldPromoteTransformLayer ? 'transform' : 'auto',
      zIndex: position.zIndex ?? zIndex,
      cursor: undefined,
    };

    if (position?.hidden || isTransparent) return null;

    const counterFillColor = active
      ? counterSettings.fill.active
      : counterSettings.fill.idle;
    const counterStrokeColor = active
      ? counterSettings.stroke.active
      : counterSettings.stroke.idle;
    const contentGap = Number.isFinite(counterSettings.gap)
      ? counterSettings.gap
      : 6;

    const renderInsideCounterPreview = () => {
      if (!showInsideCounter) {
        return null;
      }

      const displayValue =
        (counterValueSignal?.value ?? counterPreviewValue ?? 0) | 0;

      const counterElement = (
        <CountDisplay
          key="counter"
          count={displayValue}
          fillColor={counterFillColor}
          strokeColor={counterStrokeColor}
          active={false}
          fontSize={counterSettings.fontSize}
          fontFamily={counterSettings.fontFamily}
          fontWeight={counterSettings.fontWeight}
          fontItalic={counterSettings.fontItalic}
          fontUnderline={counterSettings.fontUnderline}
          fontStrikethrough={counterSettings.fontStrikethrough}
          animationEnabled={counterSettings.animation.enabled}
          animationBezier={counterSettings.animation.bezier}
          animationScale={counterSettings.animation.scale}
          animationDurationMs={counterSettings.animation.durationMs}
        />
      );

      const nameElement = (
        <span
          key="label"
          className="font-bold text-[14px] pointer-events-none select-none leading-none text-safe-inline"
          style={textStyle}
        >
          {labelText}
        </span>
      );

      const isHorizontal =
        counterSettings.align === 'left' || counterSettings.align === 'right';

      const elements = isHorizontal
        ? counterSettings.align === 'left'
          ? [counterElement, nameElement]
          : [nameElement, counterElement]
        : counterSettings.align === 'top'
          ? [counterElement, nameElement]
          : [nameElement, counterElement];

      const alignMode = counterSettings.alignMode || 'center';
      const isBetween = alignMode === 'between';
      const containerClass = `flex ${
        isHorizontal ? '' : 'flex-col'
      } w-full h-full items-center pointer-events-none select-none`;

      return (
        <div
          className={containerClass}
          style={{
            justifyContent: isBetween ? 'space-between' : 'center',
            padding: isBetween
              ? isHorizontal
                ? `0 ${contentGap}px`
                : `${contentGap}px 0`
              : '0px',
            gap: isBetween ? '0px' : `${contentGap}px`,
          }}
        >
          {elements}
        </div>
      );
    };

    const attachRef = (node: HTMLElement | null) => {
      if (!isSelectionMode) {
        draggable.ref(node);
      }
      nodeRef.current = node;
      if (typeof setReferenceRef === 'function') setReferenceRef(node);
    };

    return (
      <div
        ref={attachRef}
        className={`absolute cursor-pointer ${
          draggable && draggable.wasMoved ? '' : ''
        } ${className || ''}`}
        style={keyStyle}
        data-state={active ? 'active' : 'inactive'}
        data-editing={isDraggingOrResizing ? 'true' : undefined}
        data-key-element="true"
        onClick={handleClick}
        onMouseDown={isSelectionMode ? handleSelectionDragMouseDown : undefined}
        onContextMenu={handleContextMenu}
        onDragStart={(e) => e.preventDefault()}
      >
        {hasCurrentImage ? (
          <img
            src={currentImageSrc || ''}
            alt=""
            style={imageStyle}
            draggable={false}
          />
        ) : showInsideCounter ? (
          renderInsideCounterPreview()
        ) : (
          <div
            className="flex items-center justify-center h-full font-bold text-safe-inline"
            style={textStyle}
          >
            {labelText}
          </div>
        )}
      </div>
    );
  },
);

export default DraggableKey;

// Keys without a pair subscribe to a signal nobody sets.
const NO_PAIR = '';

export const Key = React.memo(function Key({
  keyName,
  globalKey,
  pairedKey,
  position,
  mode,
  counterEnabled = false,
}: KeyProps) {
  const selectorKey = globalKey || keyName;
  const active = useKeyActive(selectorKey);
  const pairActive = useKeyActive(pairedKey ?? NO_PAIR);
  const covered =
    pairedKey !== undefined &&
    isCoveredByPair(
      active,
      pairActive,
      getKeyPressOrder(selectorKey),
      getKeyPressOrder(pairedKey),
    );

  const {
    keyStyle,
    imageStyle,
    textStyle,
    inactiveImageSrc,
    activeImageSrc,
    currentImageSrc,
    hasCurrentImage,
    isTransparent,
    labelText,
  } = computeKeyElementStyles({ position, active, label: keyName });

  useEffect(() => {
    warmupImageSource(inactiveImageSrc);
    warmupImageSource(activeImageSrc);
  }, [inactiveImageSrc, activeImageSrc]);

  if (isTransparent || covered) return null;

  const counterSettings = normalizeCounterSettings(
    position?.counter ?? createDefaultCounterSettings(),
  );
  const showInsideCounter =
    counterEnabled &&
    counterSettings.enabled &&
    counterSettings.placement === 'inside';

  const counterSignal = showInsideCounter
    ? getKeyCounterSignal(mode ?? '', globalKey)
    : undefined;

  return (
    <div
      className={`absolute ${position.className || ''}`}
      style={keyStyle}
      data-state={active ? 'active' : 'inactive'}
    >
      {hasCurrentImage ? (
        <img
          src={currentImageSrc || ''}
          alt=""
          style={imageStyle}
          draggable={false}
        />
      ) : showInsideCounter && counterSignal ? (
        <InsideCounterLayout
          countSignal={counterSignal}
          labelText={labelText}
          textStyle={textStyle}
          active={active}
          counterSettings={counterSettings}
        />
      ) : (
        <div
          className="flex items-center justify-center h-full font-bold text-safe-inline"
          style={textStyle}
        >
          {labelText}
        </div>
      )}
    </div>
  );
});
