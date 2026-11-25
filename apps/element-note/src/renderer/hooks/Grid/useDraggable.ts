/* eslint-disable react-hooks/immutability */
import { useState, useEffect, useRef } from 'react';
import {
  MIN_GRID_POSITION,
  MAX_GRID_POSITION,
} from '@stores/grid/useGridViewStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import {
  calculateRotatedBounds,
  type ElementBounds,
} from '@utils/grid/smartGuides';
import { DRAG_THRESHOLD } from './constants';
import { clearSnapGuides, dragKeysOf, moveWithSnap } from './snapSession';

interface UseDraggableOptions {
  initialX?: number;
  initialY?: number;
  onPositionChange?: (x: number, y: number) => void;
  onDragStart?: () => void;
  zoom?: number;
  panX?: number;
  panY?: number;
  elementId?: string;
  elementWidth?: number;
  elementHeight?: number;
  /** Degrees the element is turned by; it snaps by the box it is seen in. */
  elementRotation?: number;
  getOtherElements?: ((excludeId: string) => ElementBounds[]) | null;
  disabled?: boolean;
}

interface UseDraggableReturn {
  ref: (node: HTMLElement | null) => void;
  dx: number;
  dy: number;
  wasMoved: boolean;
  isDragging: boolean;
}

// 위치 클램핑 함수
const clampPosition = (value: number): number => {
  return Math.min(Math.max(value, MIN_GRID_POSITION), MAX_GRID_POSITION);
};

export const useDraggable = ({
  initialX = 0,
  initialY = 0,
  onPositionChange,
  onDragStart = undefined, // 드래그 시작 시 호출되는 콜백 (히스토리 저장용, 선택적)
  zoom = 1, // 줌 레벨 (기본값 1)
  panX = 0, // 팬 X 오프셋
  panY = 0, // 팬 Y 오프셋
  // 스마트 가이드 관련 옵션
  elementId = '', // 요소 식별자
  elementWidth = 60, // 요소 너비
  elementHeight = 60, // 요소 높이
  elementRotation = 0,
  getOtherElements = null, // 다른 요소들의 bounds를 반환하는 함수
  disabled = false, // 드래그 비활성화 옵션
}: UseDraggableOptions): UseDraggableReturn => {
  const [node, setNode] = useState<HTMLElement | null>(null);
  const [isDragging, setIsDragging] = useState<boolean>(false);
  const [wasMoved, setWasMoved] = useState<boolean>(false);
  const [{ dx, dy }, setOffset] = useState<{ dx: number; dy: number }>({
    dx: initialX,
    dy: initialY,
  });

  // 마지막 스냅 좌표를 ref로 보관 (mouseup 시 커밋)
  const lastSnappedRef = useRef<{ dx: number; dy: number }>({
    dx: initialX,
    dy: initialY,
  });
  // 드래그 감지를 위한 최소 거리 임계값
  const dragThresholdRef = useRef<number>(DRAG_THRESHOLD);

  // 줌/팬 값을 ref로 저장 (드래그 중 최신 값 참조)
  const zoomRef = useRef<number>(zoom);
  const panXRef = useRef<number>(panX);
  const panYRef = useRef<number>(panY);

  // 스마트 가이드 관련 ref
  const elementIdRef = useRef<string>(elementId);
  const elementWidthRef = useRef<number>(elementWidth);
  const elementHeightRef = useRef<number>(elementHeight);
  const elementRotationRef = useRef<number>(elementRotation);
  const getOtherElementsRef = useRef<
    ((excludeId: string) => ElementBounds[]) | null
  >(getOtherElements);
  const disabledRef = useRef<boolean>(disabled);
  const previousBodyCursorRef = useRef<string | null>(null);

  useEffect(() => {
    zoomRef.current = zoom;
    panXRef.current = panX;
    panYRef.current = panY;
  }, [zoom, panX, panY]);

  useEffect(() => {
    elementIdRef.current = elementId;
    elementWidthRef.current = elementWidth;
    elementHeightRef.current = elementHeight;
    elementRotationRef.current = elementRotation;
    getOtherElementsRef.current = getOtherElements;
    disabledRef.current = disabled;
  }, [
    elementId,
    elementWidth,
    elementHeight,
    elementRotation,
    getOtherElements,
    disabled,
  ]);

  // initialX, initialY 변경 시 동기화
  useEffect(() => {
    setOffset({ dx: initialX, dy: initialY });
    lastSnappedRef.current = { dx: initialX, dy: initialY };
  }, [initialX, initialY]);

  const ref = (nodeEle: HTMLElement | null) => {
    setNode(nodeEle);
  };

  const restoreBodyCursor = () => {
    if (typeof document === 'undefined') return;
    if (previousBodyCursorRef.current === null) return;
    document.body.style.cursor = previousBodyCursorRef.current;
    previousBodyCursorRef.current = null;
  };

  const setBodyCursor = (cursor: string) => {
    if (typeof document === 'undefined') return;
    if (previousBodyCursorRef.current === null) {
      previousBodyCursorRef.current = document.body.style.cursor || '';
    }
    document.body.style.cursor = cursor;
  };

  const handleMouseOver = () => {
    // 미들 버튼 드래그 중이면 커서 변경하지 않음
    if (useGridSelectionStore.getState().isMiddleButtonDragging) return;
    if (node && !isDragging) node.style.cursor = 'grab';
  };

  const handleMouseOut = () => {
    // 미들 버튼 드래그 중이면 커서 변경하지 않음
    if (useGridSelectionStore.getState().isMiddleButtonDragging) return;
    if (node && !isDragging) node.style.cursor = '';
  };

  const handleMouseDown = (e: MouseEvent) => {
    if (!node) return;

    // 좌클릭만 처리 (미들 버튼은 그리드 팬에 사용)
    if (e.button !== 0) return;

    // disabled 상태면 드래그 무시
    if (disabledRef.current) return;

    // 미들 버튼 드래그 중이면 요소 드래그 무시 (그리드 팬 우선)
    if (useGridSelectionStore.getState().isMiddleButtonDragging) return;

    // A drag that did not end cleanly may have left guides behind.
    clearSnapGuides();

    const startClientX = e.clientX;
    const startClientY = e.clientY;
    let actuallyDragging = false;

    setIsDragging(true);
    setWasMoved(false);

    const currentZoom = zoomRef.current;
    const initialPosition = { dx, dy };
    const elementIdNow = elementIdRef.current;
    // The box the element is seen in. A turned element's box is not at its
    // own position, so the two are kept apart by `boxOffset`.
    const startBox = calculateRotatedBounds(
      dx,
      dy,
      elementWidthRef.current,
      elementHeightRef.current,
      elementRotationRef.current,
      elementIdNow,
    );
    const boxOffset = { x: startBox.left - dx, y: startBox.top - dy };
    // What the element can line up with. Nothing else moves during the drag.
    let targets: ElementBounds[] = [];

    let rafId: number | null = null;
    let dragEnded = false;
    let pointer: MouseEvent | null = null;

    // One rule for every drag on the canvas: see utils/grid/snapEngine.
    const follow = () => {
      rafId = null;
      if (!pointer || !actuallyDragging) return;
      const landed = moveWithSnap({
        start: startBox,
        deltaX: (pointer.clientX - startClientX) / currentZoom,
        deltaY: (pointer.clientY - startClientY) / currentZoom,
        targets,
        zoom: currentZoom,
        keys: dragKeysOf(pointer),
      });
      const snappedX = clampPosition(Math.round(landed.left - boxOffset.x));
      const snappedY = clampPosition(Math.round(landed.top - boxOffset.y));

      if (snappedX !== initialPosition.dx || snappedY !== initialPosition.dy) {
        setWasMoved(true);
      }
      lastSnappedRef.current = { dx: snappedX, dy: snappedY };
      setOffset({ dx: snappedX, dy: snappedY });
    };

    const handleMouseMove = (moveEvent: MouseEvent) => {
      if (dragEnded) return;
      pointer = moveEvent;

      if (!actuallyDragging) {
        // A press that barely moves is a click, not a drag.
        if (
          Math.abs(moveEvent.clientX - startClientX) <=
            dragThresholdRef.current &&
          Math.abs(moveEvent.clientY - startClientY) <= dragThresholdRef.current
        )
          return;
        actuallyDragging = true;
        node.style.cursor = 'grabbing';
        setBodyCursor('grabbing');
        node.style.pointerEvents = 'none';
        node.style.userSelect = 'none';
        useGridSelectionStore.getState().setDraggingOrResizing(true);
        targets =
          elementIdNow && getOtherElementsRef.current
            ? getOtherElementsRef.current(elementIdNow)
            : [];
        onDragStart?.();
      }

      // The newest pointer position is used, once per frame.
      if (rafId === null) rafId = requestAnimationFrame(follow);
    };

    const handleMouseUp = () => {
      if (rafId !== null) {
        cancelAnimationFrame(rafId);
        // Let go where the pointer is, not where the last frame left it.
        follow();
      }
      dragEnded = true;
      restoreBodyCursor();

      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
      window.removeEventListener('blur', handleMouseUp);

      setIsDragging(false);
      clearSnapGuides();

      if (actuallyDragging) {
        node.style.cursor = 'grab';
        node.style.pointerEvents = 'auto';
        node.style.userSelect = 'auto';
        useGridSelectionStore.getState().setDraggingOrResizing(false);

        // Only the final place is handed to the owner.
        const { dx: finalDx, dy: finalDy } = lastSnappedRef.current;
        onPositionChange?.(finalDx, finalDy);
      } else {
        node.style.cursor = 'grab';
      }
    };

    document.addEventListener('mousemove', handleMouseMove, {
      passive: true,
    });
    document.addEventListener('mouseup', handleMouseUp);
    window.addEventListener('blur', handleMouseUp);
  };

  useEffect(() => {
    if (!node) return;

    node.addEventListener('mousedown', handleMouseDown);
    node.addEventListener('mouseenter', handleMouseOver);
    node.addEventListener('mouseleave', handleMouseOut);

    return () => {
      node.removeEventListener('mousedown', handleMouseDown);
      node.removeEventListener('mouseenter', handleMouseOver);
      node.removeEventListener('mouseleave', handleMouseOut);
    };
  });

  useEffect(() => {
    return () => {
      restoreBodyCursor();
    };
  });

  return { ref, dx, dy, wasMoved, isDragging };
};
