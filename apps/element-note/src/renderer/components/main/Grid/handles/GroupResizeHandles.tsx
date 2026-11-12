import React, { useRef, useState } from 'react';
import { MIN_ELEMENT_SIZE } from '@hooks/Grid/constants';
import { clearSnapGuides, resizeWithSnap } from '@hooks/Grid/snapSession';
import { type ElementBounds as SmartGuideElementBounds } from '@utils/grid/smartGuides';
import {
  type CursorType,
  getCursor,
  lockCustomCursor,
  setCustomCursorHover,
  unlockCustomCursor,
} from '@utils/grid/cursorUtils';
import type { KeyPositions } from '@src/types/key/keys';
import type { StatItemPositions } from '@src/types/key/statItems';
import type { GraphItemPositions } from '@src/types/key/graphItems';
import type { KnobItemPositions } from '@src/types/key/knobs';
import type { SpritePositions } from '@src/types/key/sprites';
import type { PluginDisplayElementInternal } from '@src/types/plugin/api';
import {
  isElementResizable,
  getElementBounds,
  calculateGroupBounds,
  type Bounds,
  type SelectedElement,
  type ElementBounds,
} from './groupResizeUtils';

/**
 * 다중 선택 시 그룹 전체를 감싸는 리사이즈 핸들을 표시하는 컴포넌트
 * 선택된 모든 요소의 바운딩 박스를 계산하고, 비율을 유지하며 크기를 조절합니다.
 */

// ===== 타입 정의 =====

interface HandleDef {
  id: string;
  cursor: CursorType;
  x: number;
  y: number;
  dx: -1 | 0 | 1;
  dy: -1 | 0 | 1;
  type: 'corner' | 'edge-v' | 'edge-h';
}

interface HandleProps {
  handle: HandleDef;
  centerX: number;
  centerY: number;
  onMouseDown: (e: React.MouseEvent, handle: HandleDef) => void;
}

interface GroupResizeHandlesProps {
  selectedElements: SelectedElement[];
  positions: KeyPositions;
  statPositions: StatItemPositions;
  graphPositions: GraphItemPositions;
  knobPositions: KnobItemPositions;
  spritePositions: SpritePositions;
  selectedKeyType: string;
  pluginElements: PluginDisplayElementInternal[];
  zoom?: number;
  panX?: number;
  panY?: number;
  previewGroupBounds?: Bounds | null;
  onGroupResizeStart?: (handle: HandleDef) => void;
  onGroupResize?: (result: {
    groupBounds: Bounds;
    elementBounds: ElementBounds[];
    handle: HandleDef;
  }) => void;
  onGroupResizeEnd?: () => void;
  getOtherElements?: (excludeIds: string[]) => SmartGuideElementBounds[];
}

interface ResizeState {
  isResizing: boolean;
  handleId: string | null;
  startMouseX: number;
  startMouseY: number;
  startGroupBounds: Bounds | null;
  startElementBounds: ElementBounds[];
  nonResizableElementBounds?: ElementBounds[];
  maxShrinkX: number;
  maxShrinkY: number;
  handle?: HandleDef;
}

// ===== 조절 가능한 설정 값들 =====
const CORNER_HANDLE_SIZE = 10; // 꼭짓점 핸들의 시각적 크기 (픽셀)
const EDGE_HANDLE_WIDTH = 8; // 상하좌우 핸들의 두께 (픽셀)
const EDGE_HANDLE_LENGTH = 18; // 상하좌우 핸들의 길이 (픽셀)
const HANDLE_HIT_SIZE = 18; // 핸들의 클릭 가능 영역 크기 (픽셀)
const MIN_SIZE = MIN_ELEMENT_SIZE;
const GROUP_BORDER_WIDTH = 3; // 그룹 테두리 두께 (픽셀)
// ================================

const HANDLE_HIT_HALF = HANDLE_HIT_SIZE / 2;

// 핸들 타입 정의
const HANDLES: HandleDef[] = [
  {
    id: 'nw',
    cursor: 'nwse-resize',
    x: 0,
    y: 0,
    dx: -1,
    dy: -1,
    type: 'corner',
  },
  { id: 'n', cursor: 'ns-resize', x: 0.5, y: 0, dx: 0, dy: -1, type: 'edge-h' },
  {
    id: 'ne',
    cursor: 'nesw-resize',
    x: 1,
    y: 0,
    dx: 1,
    dy: -1,
    type: 'corner',
  },
  { id: 'w', cursor: 'ew-resize', x: 0, y: 0.5, dx: -1, dy: 0, type: 'edge-v' },
  { id: 'e', cursor: 'ew-resize', x: 1, y: 0.5, dx: 1, dy: 0, type: 'edge-v' },
  {
    id: 'sw',
    cursor: 'nesw-resize',
    x: 0,
    y: 1,
    dx: -1,
    dy: 1,
    type: 'corner',
  },
  { id: 's', cursor: 'ns-resize', x: 0.5, y: 1, dx: 0, dy: 1, type: 'edge-h' },
  { id: 'se', cursor: 'nwse-resize', x: 1, y: 1, dx: 1, dy: 1, type: 'corner' },
];

// 핸들 시각적 스타일 반환
const getHandleStyle = (
  type: HandleDef['type'],
  isHovered: boolean,
): React.CSSProperties => {
  const baseStyle: React.CSSProperties = {
    backgroundColor: isHovered ? 'var(--ui-selection)' : 'white',
    border: '2px solid var(--ui-selection-border-strong)',
    pointerEvents: 'none',
    transition: 'background-color 0.15s ease',
  };

  if (type === 'corner') {
    return {
      ...baseStyle,
      width: CORNER_HANDLE_SIZE,
      height: CORNER_HANDLE_SIZE,
      borderRadius: '50%',
    };
  } else if (type === 'edge-h') {
    return {
      ...baseStyle,
      width: EDGE_HANDLE_LENGTH,
      height: EDGE_HANDLE_WIDTH,
      borderRadius: EDGE_HANDLE_WIDTH / 2,
    };
  } else {
    return {
      ...baseStyle,
      width: EDGE_HANDLE_WIDTH,
      height: EDGE_HANDLE_LENGTH,
      borderRadius: EDGE_HANDLE_WIDTH / 2,
    };
  }
};

// 개별 핸들 컴포넌트
const Handle = ({
  handle,
  centerX,
  centerY,
  onMouseDown,
}: HandleProps): React.ReactElement => {
  const [isHovered, setIsHovered] = useState<boolean>(false);

  const hitX = centerX - HANDLE_HIT_HALF;
  const hitY = centerY - HANDLE_HIT_HALF;

  // 플랫폼에 따른 커서 스타일 적용 (macOS는 커스텀 SVG, Windows/Linux는 기본 CSS)
  const cursorStyle = getCursor(handle.cursor);

  return (
    <div
      style={{
        position: 'absolute',
        left: hitX,
        top: hitY,
        width: HANDLE_HIT_SIZE,
        height: HANDLE_HIT_SIZE,
        cursor: cursorStyle,
        zIndex: 25,
        backgroundColor: 'transparent',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
      onMouseDown={(e) => onMouseDown(e, handle)}
      onMouseEnter={(e) => {
        setIsHovered(true);
        setCustomCursorHover(handle.cursor, e.nativeEvent);
      }}
      onMouseLeave={(e) => {
        setIsHovered(false);
        setCustomCursorHover(null, e.nativeEvent);
      }}
    >
      <div style={getHandleStyle(handle.type, isHovered)} />
    </div>
  );
};

const GroupResizeHandles = ({
  selectedElements,
  positions,
  statPositions,
  graphPositions,
  knobPositions,
  spritePositions,
  selectedKeyType,
  pluginElements,
  zoom = 1,
  panX = 0,
  panY = 0,
  previewGroupBounds,
  onGroupResizeStart,
  onGroupResize,
  onGroupResizeEnd,
  getOtherElements,
}: GroupResizeHandlesProps): React.ReactElement | null => {
  const resizeRef = useRef<ResizeState>({
    isResizing: false,
    handleId: null,
    startMouseX: 0,
    startMouseY: 0,
    startGroupBounds: null,
    startElementBounds: [],
    maxShrinkX: 0,
    maxShrinkY: 0,
  });

  // 그룹 바운딩 박스 계산
  const groupData = calculateGroupBounds(
    selectedElements,
    positions,
    statPositions,
    graphPositions,
    knobPositions,
    spritePositions,
    selectedKeyType,
    pluginElements,
  );

  // 각 요소가 리사이즈 가능한지 확인
  const resizabilityInfo = selectedElements.map((element) => ({
    element,
    isResizable: isElementResizable(
      element,
      positions,
      statPositions,
      graphPositions,
      knobPositions,
      spritePositions,
      selectedKeyType,
      pluginElements,
    ),
  }));

  const nonResizableElements = resizabilityInfo.filter(
    (info) => !info.isResizable,
  );
  const resizableElements = resizabilityInfo.filter((info) => info.isResizable);

  const handleMouseDown = (e: React.MouseEvent, handle: HandleDef) => {
    e.preventDefault();
    e.stopPropagation();
    lockCustomCursor(handle.cursor, e.nativeEvent);

    if (!groupData) return;

    // 리사이즈 가능한 요소만 처리 (리사이즈 불가능한 요소의 bounds도 기억)
    const resizableElementBounds = groupData.elementBounds.filter(
      ({ element }) =>
        isElementResizable(
          element,
          positions,
          statPositions,
          graphPositions,
          knobPositions,
          spritePositions,
          selectedKeyType,
          pluginElements,
        ),
    );

    const nonResizableElementBounds = groupData.elementBounds.filter(
      ({ element }) =>
        !isElementResizable(
          element,
          positions,
          statPositions,
          graphPositions,
          knobPositions,
          spritePositions,
          selectedKeyType,
          pluginElements,
        ),
    );

    const getMaxShrink = (
      boundsList: ElementBounds[],
      groupSize: number,
      axis: 'x' | 'y',
    ): number => {
      if (!Number.isFinite(groupSize) || groupSize <= 0) return 0;
      let minScale = 0;
      for (const { bounds } of boundsList) {
        const size = axis === 'x' ? bounds.width : bounds.height;
        if (!Number.isFinite(size) || size <= 0) continue;
        if (size >= MIN_SIZE) {
          minScale = Math.max(minScale, MIN_SIZE / size);
        }
      }
      const groupMinScale = MIN_SIZE / groupSize;
      minScale = Math.min(1, Math.max(minScale, groupMinScale));
      return groupSize * (1 - minScale);
    };

    resizeRef.current = {
      isResizing: true,
      handleId: handle.id,
      startMouseX: e.clientX,
      startMouseY: e.clientY,
      startGroupBounds: {
        x: groupData.x,
        y: groupData.y,
        width: groupData.width,
        height: groupData.height,
      },
      startElementBounds: resizableElementBounds,
      nonResizableElementBounds: nonResizableElementBounds,
      maxShrinkX: getMaxShrink(resizableElementBounds, groupData.width, 'x'),
      maxShrinkY: getMaxShrink(resizableElementBounds, groupData.height, 'y'),
      handle,
    };

    onGroupResizeStart?.(handle);

    const handleMouseMove = (moveEvent: MouseEvent) => {
      if (!resizeRef.current.isResizing) return;

      const {
        handle,
        startMouseX,
        startMouseY,
        startGroupBounds,
        startElementBounds,
        nonResizableElementBounds,
        maxShrinkX,
        maxShrinkY,
      } = resizeRef.current;

      if (!handle || !startGroupBounds) return;

      // 마우스 이동량 계산 (줌 보정)
      const rawDeltaX = (moveEvent.clientX - startMouseX) / zoom;
      const rawDeltaY = (moveEvent.clientY - startMouseY) / zoom;

      // The group may shrink only until its smallest element is as small
      // as an element may be.
      const leastOf = (size: number, maxShrink: number) =>
        Number.isFinite(maxShrink) && maxShrink > 0
          ? Math.max(MIN_SIZE, Math.ceil(size - maxShrink))
          : MIN_SIZE;
      const least = {
        x: leastOf(startGroupBounds.width, maxShrinkX),
        y: leastOf(startGroupBounds.height, maxShrinkY),
      };

      // Where the pointer puts the pulled edges of the box around the group.
      let left = startGroupBounds.x;
      let top = startGroupBounds.y;
      let right = left + startGroupBounds.width;
      let bottom = top + startGroupBounds.height;
      if (handle.dx === -1) {
        left = Math.min(left + rawDeltaX, right - least.x);
      } else if (handle.dx === 1) {
        right = Math.max(right + rawDeltaX, left + least.x);
      }
      if (handle.dy === -1) {
        top = Math.min(top + rawDeltaY, bottom - least.y);
      } else if (handle.dy === 1) {
        bottom = Math.max(bottom + rawDeltaY, top + least.y);
      }

      // The group's box settles by the same rule as a single element's.
      const selectedIds = selectedElements.map((el) =>
        el.type === 'key' ? `key-${el.index}` : el.id,
      );
      const settled = resizeWithSnap({
        pointed: { id: 'group', left, top, right, bottom },
        pulled: { x: handle.dx, y: handle.dy },
        targets: getOtherElements ? getOtherElements(selectedIds) : [],
        zoom,
        suspended: moveEvent.altKey,
        minSize: least,
      });
      const newGroupX = settled.left;
      const newGroupY = settled.top;
      const newGroupWidth = settled.right - settled.left;
      const newGroupHeight = settled.bottom - settled.top;

      // 스케일 비율 계산 (스냅 적용된 그룹 bounds 기준)
      const scaleX =
        startGroupBounds.width > 0 ? newGroupWidth / startGroupBounds.width : 1;
      const scaleY =
        startGroupBounds.height > 0
          ? newGroupHeight / startGroupBounds.height
          : 1;

      // 각 리사이즈 가능한 요소의 새 위치/크기 계산 (그룹 스케일만 적용)
      const newElementBounds: ElementBounds[] = startElementBounds.map(
        ({ element, bounds }) => {
          // 그룹 내에서의 상대 위치
          const relativeX = bounds.x - startGroupBounds.x;
          const relativeY = bounds.y - startGroupBounds.y;

          // Each edge is scaled and then put on a whole unit. Rounding edges
          // (not sizes) keeps elements that touched each other touching.
          const elementLeft = Math.round(newGroupX + relativeX * scaleX);
          const elementTop = Math.round(newGroupY + relativeY * scaleY);
          const elementRight = Math.round(
            newGroupX + (relativeX + bounds.width) * scaleX,
          );
          const elementBottom = Math.round(
            newGroupY + (relativeY + bounds.height) * scaleY,
          );

          return {
            element,
            bounds: {
              x: elementLeft,
              y: elementTop,
              width: Math.max(1, elementRight - elementLeft),
              height: Math.max(1, elementBottom - elementTop),
            },
          };
        },
      );

      // 새 그룹 bounds 계산
      // - 요소별 스냅 결과로 min/max가 흔들리면(특히 왼쪽 핸들) 프리뷰 선이 좌우로 떨릴 수 있어,
      //   프리뷰는 "그룹 변환 결과"를 기준으로 유지한다.
      let finalGroupMinX = newGroupX;
      let finalGroupMinY = newGroupY;
      let finalGroupMaxX = newGroupX + newGroupWidth;
      let finalGroupMaxY = newGroupY + newGroupHeight;

      // 리사이즈 불가능한 요소들 (원래 위치 유지)
      for (const { bounds } of nonResizableElementBounds || []) {
        finalGroupMinX = Math.min(finalGroupMinX, bounds.x);
        finalGroupMinY = Math.min(finalGroupMinY, bounds.y);
        finalGroupMaxX = Math.max(finalGroupMaxX, bounds.x + bounds.width);
        finalGroupMaxY = Math.max(finalGroupMaxY, bounds.y + bounds.height);
      }

      onGroupResize?.({
        groupBounds: {
          x: finalGroupMinX,
          y: finalGroupMinY,
          width: finalGroupMaxX - finalGroupMinX,
          height: finalGroupMaxY - finalGroupMinY,
        },
        elementBounds: newElementBounds,
        handle,
      });
    };

    const handleMouseUp = () => {
      resizeRef.current.isResizing = false;
      // 스마트 가이드 클리어
      clearSnapGuides();
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
      window.removeEventListener('blur', handleMouseUp);
      unlockCustomCursor();
      onGroupResizeEnd?.();
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    window.addEventListener('blur', handleMouseUp);
  };

  if (!groupData || selectedElements.length < 2) return null;

  // 표시할 bounds (프리뷰 또는 실제)
  const displayBounds: Bounds = previewGroupBounds || {
    x: groupData.x,
    y: groupData.y,
    width: groupData.width,
    height: groupData.height,
  };

  // 그룹 테두리 좌표 계산 - 내부 요소 테두리와 동일한 위치에 겹치게
  const selectionLeft = displayBounds.x * zoom + panX - 2;
  const selectionTop = displayBounds.y * zoom + panY - 2;
  const selectionWidth = displayBounds.width * zoom + 4;
  const selectionHeight = displayBounds.height * zoom + 4;

  // 핸들 위치 계산용 - 테두리 중앙에 배치하기 위해 테두리 두께의 절반만큼 오프셋
  const borderHalf = GROUP_BORDER_WIDTH / 2;
  const handleAreaLeft = selectionLeft + borderHalf;
  const handleAreaTop = selectionTop + borderHalf;
  const handleAreaWidth = selectionWidth - GROUP_BORDER_WIDTH;
  const handleAreaHeight = selectionHeight - GROUP_BORDER_WIDTH;

  // 리사이즈 불가능한 요소가 있으면 핸들 비활성화
  const _hasNonResizable = nonResizableElements.length > 0;

  return (
    <>
      {/* 그룹 바운딩 박스 테두리 */}
      <div
        style={{
          position: 'absolute',
          left: selectionLeft,
          top: selectionTop,
          width: selectionWidth,
          height: selectionHeight,
          border: `${GROUP_BORDER_WIDTH}px solid var(--ui-selection-border-strong)`,
          borderRadius: '6px',
          pointerEvents: 'none' as const,
          zIndex: 22,
        }}
      />

      {/* 리사이즈 불가능한 요소들에 대한 표시 (주황색 점선만, 아이콘 없음) */}
      {nonResizableElements.map(({ element }) => {
        const bounds = getElementBounds(
          element,
          positions,
          statPositions,
          graphPositions,
          knobPositions,
          spritePositions,
          selectedKeyType,
          pluginElements,
        );
        if (!bounds) return null;

        return (
          <div
            key={`non-resizable-${element.id}`}
            style={{
              position: 'absolute',
              left: bounds.x * zoom + panX - 2,
              top: bounds.y * zoom + panY - 2,
              width: bounds.width * zoom + 4,
              height: bounds.height * zoom + 4,
              border:
                '2px dashed color-mix(in srgb, var(--ui-danger) 90%, transparent)',
              borderRadius: '4px',
              pointerEvents: 'none' as const,
              zIndex: 21,
            }}
            title="크기 조절 불가능한 요소"
          />
        );
      })}

      {/* 리사이즈 핸들들 - 리사이즈 가능한 요소가 있을 때만 표시 */}
      {resizableElements.length > 0 &&
        HANDLES.map((handle) => {
          // 핸들을 테두리 중앙에 배치
          const centerX = handleAreaLeft + handleAreaWidth * handle.x;
          const centerY = handleAreaTop + handleAreaHeight * handle.y;

          return (
            <Handle
              key={handle.id}
              handle={handle}
              centerX={centerX}
              centerY={centerY}
              onMouseDown={handleMouseDown}
            />
          );
        })}
    </>
  );
};

export default GroupResizeHandles;
