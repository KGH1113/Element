import React from 'react';
import { useSmartGuidesStore } from '@stores/grid/useSmartGuidesStore';

interface SmartGuidesOverlayProps {
  zoom?: number;
  panX?: number;
  panY?: number;
}

/**
 * 스마트 가이드 오버레이 컴포넌트
 * 드래그 중 다른 요소와 정렬될 때 가이드라인을 표시
 * - 정렬 가이드 (빨간 점선)
 * - 간격 가이드 (보라색 양방향 화살표 + 수치)
 * - 크기 일치 가이드 (파란색 수치)
 */
export const SmartGuidesOverlay: React.FC<SmartGuidesOverlayProps> = ({
  zoom = 1,
  panX = 0,
  panY = 0,
}) => {
  const activeGuides = useSmartGuidesStore((state) => state.activeGuides);
  const spacingGuides = useSmartGuidesStore((state) => state.spacingGuides);
  const sizeMatchGuides = useSmartGuidesStore((state) => state.sizeMatchGuides);
  const draggedBounds = useSmartGuidesStore((state) => state.draggedBounds);
  const isActive = useSmartGuidesStore((state) => state.isActive);

  const hasGuides =
    activeGuides.length > 0 ||
    spacingGuides.length > 0 ||
    sizeMatchGuides.length > 0;

  // sizeMatchGuides는 draggedBounds 없이도 표시 가능
  if (!isActive || !hasGuides) {
    return null;
  }

  // 정렬/간격 가이드는 draggedBounds가 필요하지만, sizeMatchGuides는 별도로 표시
  const showAlignmentGuides = draggedBounds && activeGuides.length > 0;
  const showSpacingGuides = draggedBounds && spacingGuides.length > 0;

  return (
    <svg
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        pointerEvents: 'none',
        overflow: 'visible',
        zIndex: 9999,
      }}
    >
      {/* 화살표 마커 정의 */}
      <defs>
        <marker
          id="spacing-arrow-start"
          markerWidth={6}
          markerHeight={6}
          refX={3}
          refY={3}
          orient="auto"
        >
          <path d="M 6 0 L 0 3 L 6 6 z" fill="var(--ui-selection)" />
        </marker>
        <marker
          id="spacing-arrow-end"
          markerWidth={6}
          markerHeight={6}
          refX={3}
          refY={3}
          orient="auto"
        >
          <path d="M 0 0 L 6 3 L 0 6 z" fill="var(--ui-selection)" />
        </marker>
      </defs>

      <g transform={`translate(${panX}, ${panY}) scale(${zoom})`}>
        {/* 정렬 가이드라인 (빨간 점선) - draggedBounds가 있을 때만 */}
        {showAlignmentGuides &&
          activeGuides.map((guide, index) => {
            // The line is as long as what it joins; the engine says how long.
            const extent = {
              start: guide.start ?? guide.position,
              end: guide.end ?? guide.position,
            };

            if (guide.type === 'vertical') {
              return (
                <line
                  key={`guide-${index}`}
                  x1={guide.position}
                  y1={extent.start}
                  x2={guide.position}
                  y2={extent.end}
                  stroke="var(--ui-guide-align)"
                  strokeWidth={1 / zoom}
                  strokeDasharray={`${4 / zoom} ${2 / zoom}`}
                  style={{
                    filter: 'drop-shadow(0 0 2px var(--ui-guide-align-glow))',
                  }}
                />
              );
            } else {
              return (
                <line
                  key={`guide-${index}`}
                  x1={extent.start}
                  y1={guide.position}
                  x2={extent.end}
                  y2={guide.position}
                  stroke="var(--ui-guide-align)"
                  strokeWidth={1 / zoom}
                  strokeDasharray={`${4 / zoom} ${2 / zoom}`}
                  style={{
                    filter: 'drop-shadow(0 0 2px var(--ui-guide-align-glow))',
                  }}
                />
              );
            }
          })}

        {/* 간격 가이드 (보라색 양방향 화살표 + 수치) - draggedBounds가 있을 때만 */}
        {showSpacingGuides &&
          spacingGuides.map((spacing, index) => {
            const isHorizontal = spacing.direction === 'horizontal';
            const midPoint = (spacing.startPos + spacing.endPos) / 2;
            const labelOffset = 12 / zoom;

            if (isHorizontal) {
              // 수평 간격: 가로 방향 화살표
              return (
                <g key={`spacing-${index}`}>
                  {/* 간격 라인 */}
                  <line
                    x1={spacing.startPos}
                    y1={spacing.crossAxisPos}
                    x2={spacing.endPos}
                    y2={spacing.crossAxisPos}
                    stroke="var(--ui-selection)"
                    strokeWidth={1.5 / zoom}
                    markerStart="url(#spacing-arrow-start)"
                    markerEnd="url(#spacing-arrow-end)"
                  />
                  {/* 간격 값 라벨 */}
                  <rect
                    x={midPoint - 16 / zoom}
                    y={spacing.crossAxisPos - labelOffset - 8 / zoom}
                    width={32 / zoom}
                    height={16 / zoom}
                    rx={4 / zoom}
                    fill="var(--ui-selection)"
                    opacity={0.9}
                  />
                  <text
                    x={midPoint}
                    y={spacing.crossAxisPos - labelOffset + 4 / zoom}
                    textAnchor="middle"
                    fontSize={10 / zoom}
                    fill="white"
                    fontWeight="bold"
                    style={{ userSelect: 'none' }}
                  >
                    {Math.round(spacing.value)}
                  </text>
                </g>
              );
            } else {
              // 수직 간격: 세로 방향 화살표
              return (
                <g key={`spacing-${index}`}>
                  {/* 간격 라인 */}
                  <line
                    x1={spacing.crossAxisPos}
                    y1={spacing.startPos}
                    x2={spacing.crossAxisPos}
                    y2={spacing.endPos}
                    stroke="var(--ui-selection)"
                    strokeWidth={1.5 / zoom}
                    markerStart="url(#spacing-arrow-start)"
                    markerEnd="url(#spacing-arrow-end)"
                  />
                  {/* 간격 값 라벨 */}
                  <rect
                    x={spacing.crossAxisPos + labelOffset - 4 / zoom}
                    y={midPoint - 8 / zoom}
                    width={32 / zoom}
                    height={16 / zoom}
                    rx={4 / zoom}
                    fill="var(--ui-selection)"
                    opacity={0.9}
                  />
                  <text
                    x={spacing.crossAxisPos + labelOffset + 12 / zoom}
                    y={midPoint + 4 / zoom}
                    textAnchor="middle"
                    fontSize={10 / zoom}
                    fill="white"
                    fontWeight="bold"
                    style={{ userSelect: 'none' }}
                  >
                    {Math.round(spacing.value)}
                  </text>
                </g>
              );
            }
          })}

        {/* 크기 일치 가이드 (파란색 라벨 + 일치 요소 테두리) */}
        {sizeMatchGuides.map((sizeMatch, index) => {
          const isWidth = sizeMatch.dimension === 'width';
          const label = isWidth
            ? `W: ${Math.round(sizeMatch.value)}`
            : `H: ${Math.round(sizeMatch.value)}`;
          const bounds = sizeMatch.matchedElementBounds;

          return (
            <g key={`size-match-${index}`}>
              {/* 일치하는 요소에 파란색 테두리 표시 */}
              {bounds && (
                <rect
                  x={bounds.left}
                  y={bounds.top}
                  width={bounds.width}
                  height={bounds.height}
                  fill="none"
                  stroke="var(--ui-selection)"
                  strokeWidth={2 / zoom}
                  strokeDasharray={`${4 / zoom} ${2 / zoom}`}
                  rx={4 / zoom}
                  style={{
                    filter: 'drop-shadow(0 0 3px var(--ui-selection-glow))',
                  }}
                />
              )}
              {/* 라벨 배경 */}
              <rect
                x={sizeMatch.position.x - 24 / zoom}
                y={sizeMatch.position.y - 8 / zoom}
                width={48 / zoom}
                height={16 / zoom}
                rx={4 / zoom}
                fill="var(--ui-selection)"
                opacity={0.9}
              />
              {/* 라벨 텍스트 */}
              <text
                x={sizeMatch.position.x}
                y={sizeMatch.position.y + 4 / zoom}
                textAnchor="middle"
                fontSize={10 / zoom}
                fill="white"
                fontWeight="bold"
                style={{ userSelect: 'none' }}
              >
                {label}
              </text>
            </g>
          );
        })}
      </g>
    </svg>
  );
};

export default SmartGuidesOverlay;
