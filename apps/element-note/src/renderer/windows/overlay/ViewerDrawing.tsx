import React, { useEffect, useRef } from 'react';
import OverlayScene from '@components/shared/OverlayScene';
import { computeLayout } from '@hooks/shared/useLayoutComputation';
import type { useNoteSystem } from '@hooks/overlay/useNoteSystem';
import { publishViewerLayout } from '@api/viewerLayout';
import { hasSpriteId } from '@utils/sprite/spriteIdentity';
import type { ExpandedKeys } from '@src/renderer/editor/model/ghostKeys';
import type { KeyViewerKind } from '@src/types/key/keys';
import type { StatItemPosition } from '@src/types/key/statItems';
import type { GraphItemPosition } from '@src/types/key/graphItems';
import type { KnobItemPosition } from '@src/types/key/knobs';
import type { useSpriteStore } from '@stores/data/useSpriteStore';
import type { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import type { useSettingsStore } from '@stores/useSettingsStore';

type SceneProps = React.ComponentProps<typeof OverlayScene>;
type NoteSystem = ReturnType<typeof useNoteSystem>;

interface ViewerDrawingProps extends Pick<
  SceneProps,
  | 'noteEffect'
  | 'noteSettings'
  | 'notesRef'
  | 'subscribe'
  | 'noteBuffer'
  | 'backgroundColor'
  | 'onMouseDownCapture'
> {
  viewerKind: KeyViewerKind | undefined;
  viewerKeys: ExpandedKeys;
  selectedKeyType: string;
  statPositions: Record<string, StatItemPosition[]>;
  graphPositions: Record<string, GraphItemPosition[]>;
  knobPositions: Record<string, KnobItemPosition[]>;
  spritePositions: ReturnType<typeof useSpriteStore.getState>['positions'];
  pluginElements: ReturnType<
    typeof usePluginDisplayElementStore.getState
  >['elements'];
  trackHeight: number;
  overlayPadding: number;
  overlayAnchor: ReturnType<
    typeof useSettingsStore.getState
  >['overlayResizeAnchor'];
  updateTrackLayouts: NoteSystem['updateTrackLayouts'];
}

/**
 * Lays out a key viewer's tab and draws it, and tells the window or the
 * game how big it is. The overlay window keeps receiving the app's data
 * outside of this, so a drawing error never stops the data.
 */
export function ViewerDrawing({
  viewerKind,
  viewerKeys,
  selectedKeyType,
  statPositions,
  graphPositions,
  knobPositions,
  spritePositions,
  pluginElements,
  trackHeight,
  overlayPadding,
  overlayAnchor,
  updateTrackLayouts,
  noteEffect,
  noteSettings,
  notesRef,
  subscribe,
  noteBuffer,
  backgroundColor,
  onMouseDownCapture,
}: ViewerDrawingProps) {
  const currentKeys = viewerKeys.keys;
  const currentPositions = viewerKeys.positions;
  const currentStatPositions = statPositions[selectedKeyType] ?? [];
  const currentGraphPositions = graphPositions[selectedKeyType] ?? [];
  const currentKnobPositions = knobPositions[selectedKeyType] ?? [];
  const currentSpritePositions = (
    spritePositions[selectedKeyType] ?? []
  ).filter(hasSpriteId);

  const {
    bounds,
    displayPositions,
    displayStatPositions,
    displayGraphPositions,
    displayKnobPositions,
    displaySpritePositions,
    positionOffset,
    webglTracks,
  } = computeLayout({
    currentKeys,
    currentPositions,
    currentStatPositions,
    currentGraphPositions,
    currentKnobPositions,
    currentSpritePositions,
    trackHeight,
    noteSettings,
    selectedKeyType,
    pluginElements,
    overlayPadding,
  });

  useEffect(() => {
    updateTrackLayouts(webglTracks);
  }, [webglTracks, updateTrackLayouts]);

  // 이전 resize 값을 추적하여 실제로 변경되었을 때만 resize 호출
  const lastResizeParams = useRef<{
    width: number;
    height: number;
    anchor: string;
    contentTopOffset: number;
    minX: number;
    minY: number;
  } | null>(null);

  useEffect(() => {
    if (!bounds) {
      publishViewerLayout(1, 1, 0);
      return;
    }

    const keyAreaWidth = bounds.maxX - bounds.minX;
    const keyAreaHeight = bounds.maxY - bounds.minY;
    const extraTop = trackHeight;
    const totalWidth = keyAreaWidth + overlayPadding * 2;
    const totalHeight = keyAreaHeight + overlayPadding * 2 + extraTop;
    publishViewerLayout(totalWidth, totalHeight, overlayPadding);
    const contentTopOffset = extraTop + overlayPadding;
    const currentMinX = bounds.minX;
    const currentMinY = bounds.minY;

    // 이전 값과 비교하여 실제로 변경되었을 때만 resize 호출
    const lastParams = lastResizeParams.current;
    const fixedPositionAnchor = overlayAnchor === 'fixed-position';
    const fixedPositionDeltaX =
      fixedPositionAnchor && lastParams?.anchor === 'fixed-position'
        ? currentMinX - lastParams.minX
        : 0;
    const fixedPositionDeltaY =
      fixedPositionAnchor && lastParams?.anchor === 'fixed-position'
        ? currentMinY - lastParams.minY
        : 0;
    if (
      lastParams &&
      Math.abs(lastParams.width - totalWidth) < 0.5 &&
      Math.abs(lastParams.height - totalHeight) < 0.5 &&
      lastParams.anchor === overlayAnchor &&
      Math.abs(lastParams.contentTopOffset - contentTopOffset) < 0.5 &&
      (!fixedPositionAnchor ||
        (Math.abs(lastParams.minX - currentMinX) < 0.5 &&
          Math.abs(lastParams.minY - currentMinY) < 0.5))
    ) {
      return; // 변경사항 없음, resize 건너뛰기
    }

    lastResizeParams.current = {
      width: totalWidth,
      height: totalHeight,
      anchor: overlayAnchor,
      contentTopOffset,
      minX: currentMinX,
      minY: currentMinY,
    };

    window.api.overlay
      .resize({
        viewerKind: viewerKind ?? 'hand',
        width: totalWidth,
        height: totalHeight,
        anchor: overlayAnchor,
        contentTopOffset,
        fixedPositionDeltaX: fixedPositionAnchor
          ? fixedPositionDeltaX
          : undefined,
        fixedPositionDeltaY: fixedPositionAnchor
          ? fixedPositionDeltaY
          : undefined,
      })
      .catch((error) => {
        console.error('Failed to resize overlay window', error);
      });
  }, [bounds, trackHeight, overlayAnchor, overlayPadding, viewerKind]);

  return (
    <OverlayScene
      currentKeys={currentKeys}
      pairedKeys={viewerKeys.pairedKeys}
      displayPositions={displayPositions}
      currentPositions={currentPositions}
      displayStatPositions={displayStatPositions}
      displayGraphPositions={displayGraphPositions}
      displayKnobPositions={displayKnobPositions}
      displaySpritePositions={displaySpritePositions}
      selectedKeyType={selectedKeyType}
      noteEffect={noteEffect}
      noteSettings={noteSettings}
      webglTracks={webglTracks}
      notesRef={notesRef}
      subscribe={subscribe}
      noteBuffer={noteBuffer}
      backgroundColor={backgroundColor}
      positionOffset={positionOffset}
      onMouseDownCapture={onMouseDownCapture}
      showPluginElements={true}
    />
  );
}
