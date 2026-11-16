/**
 * The boxes elements take up on the canvas and the guides drawn between
 * them. Where a dragged box lands is decided in `snapEngine.ts`.
 */

export interface ElementBounds {
  id: string;
  left: number;
  top: number;
  right: number;
  bottom: number;
  centerX: number;
  centerY: number;
  width: number;
  height: number;
}

export interface GuideLine {
  type: 'vertical' | 'horizontal';
  position: number; // 가이드라인의 x 또는 y 위치
  alignType: 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom';
  /** Where the line starts and ends along its own direction. */
  start?: number;
  end?: number;
}

/**
 * 간격 가이드 인터페이스
 * 요소 사이의 간격을 시각화
 */
export interface SpacingGuide {
  type: 'spacing';
  direction: 'horizontal' | 'vertical';
  value: number; // 간격 값 (px)
  // 간격 표시 위치
  startPos: number; // 간격 시작 위치 (x 또는 y)
  endPos: number; // 간격 끝 위치 (x 또는 y)
  crossAxisPos: number; // 교차 축 위치 (라벨 표시용)
  // 관련 요소
  fromElementId: string;
  toElementId: string;
  // 이 간격이 다른 간격과 일치하여 스냅됐는지
  isMatched: boolean;
}

/**
 * 크기 일치 가이드 인터페이스
 * 리사이즈 시 다른 요소와 동일한 크기로 스냅
 */
export interface SizeMatchGuide {
  type: 'size-match';
  dimension: 'width' | 'height';
  value: number; // 일치하는 크기 값
  position: { x: number; y: number }; // 표시 위치
  matchedElementId: string;
  // 일치하는 요소의 bounds (테두리 표시용)
  matchedElementBounds: {
    left: number;
    top: number;
    width: number;
    height: number;
  };
}

/**
 * 요소의 bounds 정보 계산
 */
export function calculateBounds(
  x: number,
  y: number,
  width: number,
  height: number,
  id: string = '',
): ElementBounds {
  return {
    id,
    left: x,
    top: y,
    right: x + width,
    bottom: y + height,
    centerX: x + width / 2,
    centerY: y + height / 2,
    width,
    height,
  };
}

/**
 * The box a rotated element takes up on the canvas: the upright box around
 * it. Elements turn around their centre. Snapping lines up what is seen, so
 * a turned element lines up by this box.
 */
export function calculateRotatedBounds(
  x: number,
  y: number,
  width: number,
  height: number,
  rotation: number | undefined,
  id: string = '',
): ElementBounds {
  if (!rotation || !Number.isFinite(rotation) || rotation % 180 === 0) {
    return calculateBounds(x, y, width, height, id);
  }
  const angle = (rotation * Math.PI) / 180;
  const cos = Math.abs(Math.cos(angle));
  const sin = Math.abs(Math.sin(angle));
  const seenWidth = width * cos + height * sin;
  const seenHeight = width * sin + height * cos;
  return calculateBounds(
    x + (width - seenWidth) / 2,
    y + (height - seenHeight) / 2,
    seenWidth,
    seenHeight,
    id,
  );
}

/**
 * 여러 요소들의 전체 바운딩 박스 계산 (그룹 선택용)
 * @param elements 요소들의 bounds 배열
 * @returns 전체 요소를 감싸는 바운딩 박스
 */
export function calculateGroupBounds(
  elements: ElementBounds[],
): ElementBounds | null {
  if (elements.length === 0) return null;

  if (elements.length === 1) return elements[0];

  const left = Math.min(...elements.map((el) => el.left));
  const top = Math.min(...elements.map((el) => el.top));
  const right = Math.max(...elements.map((el) => el.right));
  const bottom = Math.max(...elements.map((el) => el.bottom));
  const width = right - left;
  const height = bottom - top;

  return {
    id: 'group',
    left,
    top,
    right,
    bottom,
    centerX: left + width / 2,
    centerY: top + height / 2,
    width,
    height,
  };
}

// 캔버스 중앙 좌표 (그리드 렌더링 영역 900x396 기준)
export const CANVAS_CENTER_X = 450;
export const CANVAS_CENTER_Y = 195;
