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
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { warmupImageSource } from '@utils/core/imageWarmup';
import {
  computeKeyElementStyles,
  type KeyElementPosition,
} from '@hooks/overlay/useKeyElementStyles';
import { useKeyActive } from '@hooks/overlay/useKeyActive';
import { useShownPressed } from '@src/renderer/editor/runtime/pressedPreview';
import {
  usePreviewCount,
  usePreviewPressed,
} from '@src/renderer/editor/runtime/partPreview';
import { useShownPosition } from '@src/renderer/editor/runtime/choicePreview';
import { getKeyPressOrder } from '@stores/signals/keySignals';
import { isCoveredByPair } from '@src/renderer/editor/model/ghostKeys';
import InsideCounterLayout from '@components/overlay/counters/InsideCounterLayout';
import CountDisplay from '@components/overlay/counters/CountDisplay';
import { calculateRotatedBounds } from '@utils/grid/smartGuides';
import { startSelectionDrag } from '@hooks/Grid/selectionDrag';

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
    position: storedPosition,
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
    // A look the pointer rests on in the sidebar is drawn over the stored
    // one (choicePreview.ts); it never changes where the key is or its size.
    const position = useShownPosition(
      elementId?.startsWith('stat-') ? 'stat' : 'key',
      index,
      storedPosition,
    );

    const macOS = isMac();
    const { displayName } = getKeyInfoByGlobalKey(keyName);
    const held = useKeyActive(keyName);
    // While its pressed look is being chosen, the key is drawn as pressed.
    const shownPressed = useShownPressed(isSelected);
    // While one of its parts is previewed, it goes down now and then.
    const previewPressed = usePreviewPressed(isSelected);
    const previewCount = usePreviewCount(isSelected);
    const active = held || shownPressed || previewPressed;
    const { dx, dy, width, height = 60, className, counter } = position;

    const {
      keyStyle: visualKeyStyle,
      imageStyle,
      textStyle,
      currentImageSrc,
      hasCurrentImage,
      imageReplaces,
      borderRingStyle,
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

    const isDraggingOrResizing = useGridSelectionStore(
      (state) => state.isDraggingOrResizing,
    );
    const nodeRef = useRef<HTMLElement | null>(null);
    const effectiveElementId = elementId || `key-${index}`;

    const isSelectionMode = isSelected;

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
      elementRotation: position.rotation,
      getOtherElements,
      disabled: isSelectionMode,
    });

    const handleSelectionDragMouseDown = (e: React.MouseEvent) => {
      if (!isSelectionMode) return;
      startSelectionDrag({
        event: e,
        elementBox: calculateRotatedBounds(
          dx,
          dy,
          width || 60,
          height || 60,
          position.rotation,
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
      transform: `translate(calc(${renderDx}px + var(--key-offset-x, 0px)), calc(${renderDy}px + var(--key-offset-y, 0px)))${position.rotation ? ` rotate(${position.rotation}deg)` : ''}`,
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
        (previewCount ??
          counterValueSignal?.value ??
          counterPreviewValue ??
          0) | 0;

      const counterElement = (
        <CountDisplay
          key="counter"
          count={displayValue}
          fillColor={counterFillColor}
          fillGradient={
            active
              ? counterSettings.fillActiveGradient
              : counterSettings.fillIdleGradient
          }
          fontBold={counterSettings.fontBold}
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
        {borderRingStyle && <span aria-hidden="true" style={borderRingStyle} />}
        {hasCurrentImage && !imageReplaces && (
          <img
            src={currentImageSrc || ''}
            alt=""
            style={imageStyle}
            draggable={false}
          />
        )}
        {imageReplaces ? (
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
    imageReplaces,
    borderRingStyle,
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
      {borderRingStyle && <span aria-hidden="true" style={borderRingStyle} />}
      {hasCurrentImage && !imageReplaces && (
        <img
          src={currentImageSrc || ''}
          alt=""
          style={imageStyle}
          draggable={false}
        />
      )}
      {imageReplaces ? (
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
