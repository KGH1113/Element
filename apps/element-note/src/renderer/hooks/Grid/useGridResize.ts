import { useRef, useState } from 'react';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import { useHistoryStore } from '@stores/data/useHistoryStore';
import type { Edges } from '@utils/grid/snapEngine';
import { MIN_ELEMENT_SIZE } from './constants';
import { clearSnapGuides, resizeWithSnap } from './snapSession';
import type { SelectedElement } from '@stores/grid/useGridSelectionStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import type { KeyPositions } from '@src/types/key/keys';
import type { StatItemPositions } from '@src/types/key/statItems';
import type { GraphItemPositions } from '@src/types/key/graphItems';
import type { KnobItemPositions } from '@src/types/key/knobs';
import type { ElementBounds } from '@utils/grid/smartGuides';
import { patchSpritesAt } from '@src/renderer/editor/runtime/spritePositions';

interface ResizeHandle {
  id: string;
  dx: number;
  dy: number;
}

interface ResizeBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** What the resize handles report while an edge is dragged. */
interface PointedResize extends ResizeBounds {
  handle?: ResizeHandle;
  start?: ResizeBounds;
  keepAspect?: boolean;
  suspended?: boolean;
  zoom?: number;
}

const side = (value: number): -1 | 0 | 1 =>
  value < 0 ? -1 : value > 0 ? 1 : 0;

/**
 * The other dimension of a box that keeps its shape: its size follows the
 * dragged one, and it stays on its anchor (the opposite edge for a corner,
 * the middle for an edge handle).
 */
function followAspect(
  size: number,
  pulled: -1 | 0 | 1,
  startLo: number,
  startSize: number,
): [number, number] {
  const whole = Math.max(MIN_ELEMENT_SIZE, Math.round(size));
  if (pulled === 1) return [startLo, startLo + whole];
  if (pulled === -1) return [startLo + startSize - whole, startLo + startSize];
  const lo = Math.round(startLo + (startSize - whole) / 2);
  return [lo, lo + whole];
}

// 그룹 리사이즈용 요소 bounds
interface GroupElementBounds {
  element: SelectedElement;
  bounds: ResizeBounds;
}

// 그룹 리사이즈 결과
interface GroupResizeResult {
  groupBounds: ResizeBounds;
  elementBounds: GroupElementBounds[];
  handle: ResizeHandle;
}

interface UseGridResizeOptions {
  selectedElements: SelectedElement[];
  selectedKeyType: string;
  onResizeEnd?: () => void;
  getOtherElements?: (excludeId: string) => ElementBounds[];
}

/**
 * 그리드에서 키 및 플러그인 요소 리사이즈를 처리하는 훅
 * 드래그 중에는 프리뷰 bounds만 업데이트하고, 드래그 종료 시 실제 크기를 적용하여
 * 시각적 흔들림을 방지합니다.
 */
export function useGridResize({
  selectedElements,
  selectedKeyType,
  onResizeEnd,
  getOtherElements,
}: UseGridResizeOptions) {
  const resizeStartRef = useRef(false);
  // 드래그 중 프리뷰 bounds (드래그 중일 때만 값이 있음)
  const [previewBounds, setPreviewBounds] = useState<ResizeBounds | null>(null);
  // 최종 적용할 bounds를 저장 (드래그 종료 시 사용)
  const finalBoundsRef = useRef<ResizeBounds | null>(null);

  // 그룹 리사이즈용 상태
  const [previewGroupBounds, setPreviewGroupBounds] =
    useState<ResizeBounds | null>(null);
  const [previewElementBounds, setPreviewElementBounds] = useState<
    GroupElementBounds[] | null
  >(null);
  const finalGroupBoundsRef = useRef<{
    groupBounds: ResizeBounds;
    elementBounds: GroupElementBounds[];
  } | null>(null);

  // 리사이즈 시작 시 히스토리 저장
  const handleResizeStart = (_handle?: ResizeHandle) => {
    if (resizeStartRef.current) return;
    resizeStartRef.current = true;

    // 기존 스마트 가이드 클리어
    clearSnapGuides();

    // 리사이즈 시작 시 애니메이션 비활성화
    useGridSelectionStore.getState().setDraggingOrResizing(true);

    const currentPositions = useKeyStore.getState().positions;
    const currentPluginElements =
      usePluginDisplayElementStore.getState().elements;
    const currentStatPositions = useStatItemStore.getState().positions;
    const currentGraphPositions = useGraphItemStore.getState().positions;
    const { keyMappings } = useKeyStore.getState();
    useHistoryStore.getState().pushState({
      keyMappings,
      positions: currentPositions,
      statPositions: currentStatPositions,
      graphPositions: currentGraphPositions,
      pluginElements: currentPluginElements,
    });
  };

  // One preview for every kind of element. The handles say where the pointer
  // puts the edges; the pulled edges are settled here by the same rule that
  // moving uses (utils/grid/snapEngine). Nothing is saved until the drag ends.
  const handleElementResizePreview = (
    elementId: string,
    pointed: PointedResize,
  ) => {
    const { handle, start } = pointed;
    const box = {
      id: elementId,
      left: pointed.x,
      top: pointed.y,
      right: pointed.x + pointed.width,
      bottom: pointed.y + pointed.height,
    };
    let edges: Edges = box;

    if (handle) {
      const pulled = { x: side(handle.dx), y: side(handle.dy) };
      const settle = (pull: typeof pulled) =>
        resizeWithSnap({
          pointed: box,
          pulled: pull,
          targets: getOtherElements ? getOtherElements(elementId) : [],
          zoom: pointed.zoom ?? 1,
          suspended: pointed.suspended === true,
          minSize: MIN_ELEMENT_SIZE,
        });

      const keepsShape =
        pointed.keepAspect === true &&
        !!start &&
        start.width > 0 &&
        start.height > 0;
      if (!keepsShape) {
        edges = settle(pulled);
      } else {
        // The dimension that changed more leads; only it is settled, and the
        // other one follows it.
        const leadsX =
          pulled.y === 0 ||
          (pulled.x !== 0 &&
            Math.abs(pointed.width - start.width) / start.width >=
              Math.abs(pointed.height - start.height) / start.height);
        if (leadsX) {
          const led = settle({ x: pulled.x, y: 0 });
          const [top, bottom] = followAspect(
            ((led.right - led.left) * start.height) / start.width,
            pulled.y,
            start.y,
            start.height,
          );
          edges = { left: led.left, right: led.right, top, bottom };
        } else {
          const led = settle({ x: 0, y: pulled.y });
          const [left, right] = followAspect(
            ((led.bottom - led.top) * start.width) / start.height,
            pulled.x,
            start.x,
            start.width,
          );
          edges = { left, right, top: led.top, bottom: led.bottom };
        }
      }
    }

    const previewData = {
      x: edges.left,
      y: edges.top,
      width: edges.right - edges.left,
      height: edges.bottom - edges.top,
    };
    setPreviewBounds(previewData);
    finalBoundsRef.current = previewData;
  };

  // The selected element, whatever kind it is, under the id snapping knows.
  const handleResize = (pointed: PointedResize) => {
    if (selectedElements.length !== 1) return;
    const element = selectedElements[0];
    if (element.type === 'plugin') {
      handleElementResizePreview(element.id, pointed);
    } else if (element.index !== undefined) {
      handleElementResizePreview(`${element.type}-${element.index}`, pointed);
    }
  };

  // 리사이즈 종료 처리 - 실제 요소에 최종 bounds 적용
  const handleResizeComplete = () => {
    resizeStartRef.current = false;

    // 스마트 가이드 클리어
    clearSnapGuides();

    // 리사이즈 종료 시 애니메이션 복원
    useGridSelectionStore.getState().setDraggingOrResizing(false);

    // 최종 bounds를 실제 요소에 적용
    const finalBounds = finalBoundsRef.current;
    if (finalBounds && selectedElements.length === 1) {
      const element = selectedElements[0];

      if (element.type === 'key' && element.index !== undefined) {
        // 키 요소에 최종 크기 적용
        const positions = useKeyStore.getState().positions;
        const setPositions = useKeyStore.getState().setPositions;
        const current = positions[selectedKeyType] || [];
        const nextPositions: KeyPositions = {
          ...positions,
          [selectedKeyType]: current.map((pos, i) =>
            i === element.index
              ? {
                  ...pos,
                  dx: finalBounds.x,
                  dy: finalBounds.y,
                  width: finalBounds.width,
                  height: finalBounds.height,
                }
              : pos,
          ),
        };
        setPositions(nextPositions);

        // 백엔드에 저장
        window.api.keys.updatePositions(nextPositions).catch((error) => {
          console.error('Failed to update key positions after resize', error);
        });
      } else if (element.type === 'stat' && element.index !== undefined) {
        const statStore = useStatItemStore.getState();
        const statPositions = statStore.positions;
        const current = statPositions[selectedKeyType] || [];
        const nextPositions: StatItemPositions = {
          ...statPositions,
          [selectedKeyType]: current.map((pos, i) =>
            i === element.index
              ? {
                  ...pos,
                  dx: finalBounds.x,
                  dy: finalBounds.y,
                  width: finalBounds.width,
                  height: finalBounds.height,
                }
              : pos,
          ),
        };
        statStore.setPositions(nextPositions);
        window.api.statItems.updatePositions(nextPositions).catch((error) => {
          console.error('Failed to update stat positions after resize', error);
        });
      } else if (element.type === 'graph' && element.index !== undefined) {
        const graphStore = useGraphItemStore.getState();
        const graphPositions = graphStore.positions;
        const current = graphPositions[selectedKeyType] || [];
        const nextPositions: GraphItemPositions = {
          ...graphPositions,
          [selectedKeyType]: current.map((pos, i) =>
            i === element.index
              ? {
                  ...pos,
                  dx: finalBounds.x,
                  dy: finalBounds.y,
                  width: finalBounds.width,
                  height: finalBounds.height,
                }
              : pos,
          ),
        };
        graphStore.setPositions(nextPositions);
        window.api.graphItems.updatePositions(nextPositions).catch((error) => {
          console.error('Failed to update graph positions after resize', error);
        });
      } else if (element.type === 'knob' && element.index !== undefined) {
        const knobStore = useKnobItemStore.getState();
        const knobPositions = knobStore.positions;
        const current = knobPositions[selectedKeyType] || [];
        const nextPositions: KnobItemPositions = {
          ...knobPositions,
          [selectedKeyType]: current.map((pos, i) =>
            i === element.index
              ? {
                  ...pos,
                  dx: finalBounds.x,
                  dy: finalBounds.y,
                  width: finalBounds.width,
                  height: finalBounds.height,
                }
              : pos,
          ),
        };
        knobStore.setPositions(nextPositions);
        window.api.knobItems.updatePositions(nextPositions).catch((error) => {
          console.error('Failed to update knob positions after resize', error);
        });
        try {
          window.api.bridge.sendTo('overlay', 'knobPositions:sync', {
            positions: nextPositions,
          });
        } catch {
          // 무시
        }
      } else if (element.type === 'sprite' && element.index !== undefined) {
        void patchSpritesAt(selectedKeyType, [element.index], () => ({
          dx: finalBounds.x,
          dy: finalBounds.y,
          width: finalBounds.width,
          height: finalBounds.height,
        }));
      } else if (element.type === 'plugin') {
        // 플러그인 요소에 최종 크기 적용
        const pluginStore = usePluginDisplayElementStore.getState();
        pluginStore.updateElement(element.id, {
          position: { x: finalBounds.x, y: finalBounds.y },
          measuredSize: {
            width: finalBounds.width,
            height: finalBounds.height,
          },
        });
      }
    }

    // 프리뷰 상태 클리어
    setPreviewBounds(null);
    finalBoundsRef.current = null;

    onResizeEnd?.();
  };

  // 그룹 리사이즈 핸들러 - 프리뷰 모드
  const handleGroupResize = (result: GroupResizeResult) => {
    setPreviewGroupBounds(result.groupBounds);
    setPreviewElementBounds(result.elementBounds);
    finalGroupBoundsRef.current = {
      groupBounds: result.groupBounds,
      elementBounds: result.elementBounds,
    };
  };

  // 그룹 리사이즈 완료 처리 - 실제 요소들에 최종 bounds 적용
  const handleGroupResizeComplete = () => {
    resizeStartRef.current = false;

    // 스마트 가이드 클리어
    clearSnapGuides();

    // 리사이즈 종료 시 애니메이션 복원
    useGridSelectionStore.getState().setDraggingOrResizing(false);

    const finalData = finalGroupBoundsRef.current;
    if (finalData && finalData.elementBounds.length > 0) {
      const positions = useKeyStore.getState().positions;
      const setPositions = useKeyStore.getState().setPositions;
      const current = positions[selectedKeyType] || [];
      const pluginStore = usePluginDisplayElementStore.getState();
      const statStore = useStatItemStore.getState();
      const statPositions = statStore.positions;
      const currentStats = statPositions[selectedKeyType] || [];
      const graphStore = useGraphItemStore.getState();
      const graphPositions = graphStore.positions;
      const currentGraphs = graphPositions[selectedKeyType] || [];
      const knobStore = useKnobItemStore.getState();
      const knobPositions = knobStore.positions;
      const currentKnobs = knobPositions[selectedKeyType] || [];

      // 프리뷰 값을 그대로 사용 (스냅은 이미 드래그 중에 적용됨)
      // 추가 스냅 적용 시 프리뷰와 최종 위치가 달라지는 문제 발생

      // 키 요소들 업데이트
      const keyUpdates = finalData.elementBounds.filter(
        ({ element }) => element.type === 'key' && element.index !== undefined,
      );

      if (keyUpdates.length > 0) {
        const nextPositions: KeyPositions = {
          ...positions,
          [selectedKeyType]: current.map((pos, i) => {
            const update = keyUpdates.find(
              ({ element }) => element.index === i,
            );
            if (update) {
              return {
                ...pos,
                dx: update.bounds.x,
                dy: update.bounds.y,
                width: update.bounds.width,
                height: update.bounds.height,
              };
            }
            return pos;
          }),
        };
        setPositions(nextPositions);

        // 백엔드에 저장
        window.api.keys.updatePositions(nextPositions).catch((error) => {
          console.error(
            'Failed to update key positions after group resize',
            error,
          );
        });
      }

      // 통계 요소들 업데이트
      const statUpdates = finalData.elementBounds.filter(
        ({ element }) => element.type === 'stat' && element.index !== undefined,
      );

      if (statUpdates.length > 0) {
        const nextStatPositions: StatItemPositions = {
          ...statPositions,
          [selectedKeyType]: currentStats.map((pos, i) => {
            const update = statUpdates.find(
              ({ element }) => element.index === i,
            );
            if (update) {
              return {
                ...pos,
                dx: update.bounds.x,
                dy: update.bounds.y,
                width: update.bounds.width,
                height: update.bounds.height,
              };
            }
            return pos;
          }),
        };

        statStore.setPositions(nextStatPositions);
        window.api.statItems
          .updatePositions(nextStatPositions)
          .catch((error) => {
            console.error(
              'Failed to update stat positions after group resize',
              error,
            );
          });
      }

      // 그래프 요소들 업데이트
      const graphUpdates = finalData.elementBounds.filter(
        ({ element }) =>
          element.type === 'graph' && element.index !== undefined,
      );

      if (graphUpdates.length > 0) {
        const nextGraphPositions: GraphItemPositions = {
          ...graphPositions,
          [selectedKeyType]: currentGraphs.map((pos, i) => {
            const update = graphUpdates.find(
              ({ element }) => element.index === i,
            );
            if (update) {
              return {
                ...pos,
                dx: update.bounds.x,
                dy: update.bounds.y,
                width: update.bounds.width,
                height: update.bounds.height,
              };
            }
            return pos;
          }),
        };

        graphStore.setPositions(nextGraphPositions);
        window.api.graphItems
          .updatePositions(nextGraphPositions)
          .catch((error) => {
            console.error(
              'Failed to update graph positions after group resize',
              error,
            );
          });
      }

      // 노브 요소들 업데이트
      const knobUpdates = finalData.elementBounds.filter(
        ({ element }) => element.type === 'knob' && element.index !== undefined,
      );

      if (knobUpdates.length > 0) {
        const nextKnobPositions: KnobItemPositions = {
          ...knobPositions,
          [selectedKeyType]: currentKnobs.map((pos, i) => {
            const update = knobUpdates.find(
              ({ element }) => element.index === i,
            );
            if (update) {
              return {
                ...pos,
                dx: update.bounds.x,
                dy: update.bounds.y,
                width: update.bounds.width,
                height: update.bounds.height,
              };
            }
            return pos;
          }),
        };

        knobStore.setPositions(nextKnobPositions);
        window.api.knobItems
          .updatePositions(nextKnobPositions)
          .catch((error) => {
            console.error(
              'Failed to update knob positions after group resize',
              error,
            );
          });
        try {
          window.api.bridge.sendTo('overlay', 'knobPositions:sync', {
            positions: nextKnobPositions,
          });
        } catch {
          // 무시
        }
      }

      const spriteBounds = new Map(
        finalData.elementBounds
          .filter(
            ({ element }) =>
              element.type === 'sprite' && element.index !== undefined,
          )
          .map(({ element, bounds }) => [element.index as number, bounds]),
      );
      void patchSpritesAt(
        selectedKeyType,
        [...spriteBounds.keys()],
        (_, index) => {
          const bounds = spriteBounds.get(index)!;
          return {
            dx: bounds.x,
            dy: bounds.y,
            width: bounds.width,
            height: bounds.height,
          };
        },
      );

      // 플러그인 요소들 업데이트
      const pluginUpdates = finalData.elementBounds.filter(
        ({ element }) => element.type === 'plugin',
      );

      for (const { element, bounds } of pluginUpdates) {
        pluginStore.updateElement(element.id, {
          position: { x: bounds.x, y: bounds.y },
          measuredSize: {
            width: bounds.width,
            height: bounds.height,
          },
        });
      }
    }

    // 프리뷰 상태 클리어
    setPreviewGroupBounds(null);
    setPreviewElementBounds(null);
    finalGroupBoundsRef.current = null;

    onResizeEnd?.();
  };

  return {
    handleResizeStart,
    handleResize,
    handleResizeComplete,
    previewBounds,
    // 그룹 리사이즈 관련
    handleGroupResize,
    handleGroupResizeComplete,
    previewGroupBounds,
    previewElementBounds,
  };
}
