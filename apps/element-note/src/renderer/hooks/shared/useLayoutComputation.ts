import {
  computeTrackGeometry,
  groupSameFlowAngles,
  sameFlowStartShift,
  translateTrackGeometry,
} from '@utils/layout/trackGeometry';
import { rotatedRectAabb } from '@utils/element/rotation';
import {
  DEFAULT_NOTE_BORDER_RADIUS,
  DEFAULT_NOTE_SETTINGS,
  FALLBACK_POSITION,
} from '@constants/overlayDefaults';
import type { KeyPosition } from '@src/types/key/keys';
import type { StatItemPosition } from '@src/types/key/statItems';
import type { GraphItemPosition } from '@src/types/key/graphItems';
import type { KnobItemPosition } from '@src/types/key/knobs';
import type { NoteSettings } from '@src/types/settings/noteSettings';
import type { CanonicalReactiveSpritePosition } from '@src/types/editor';
import { computeSpriteReachAabb } from '@utils/sprite/spriteReach';

interface PluginElement {
  hidden?: boolean;
  tabId?: string;
  position: { x: number; y: number };
  anchor?: {
    keyCode?: string;
    offset?: { x?: number; y?: number };
  };
  measuredSize?: { width?: number; height?: number };
  estimatedSize?: { width?: number; height?: number };
}

interface LayoutInput {
  currentKeys: string[];
  currentPositions: KeyPosition[];
  currentStatPositions: StatItemPosition[];
  currentGraphPositions: GraphItemPosition[];
  currentKnobPositions: KnobItemPosition[];
  currentSpritePositions?: CanonicalReactiveSpritePosition[];
  trackHeight: number;
  noteSettings: NoteSettings;
  selectedKeyType?: string;
  pluginElements?: PluginElement[];
  overlayPadding?: number;
}

interface Bounds {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

export function computeLayout(input: LayoutInput) {
  const {
    currentKeys,
    currentPositions,
    currentStatPositions,
    currentGraphPositions,
    currentKnobPositions,
    currentSpritePositions = [],
    trackHeight,
    noteSettings,
    selectedKeyType,
    pluginElements,
    overlayPadding: PADDING = 30,
  } = input;

  // Preserve v1's axis-aligned hitline. Only v2 rotated tracks participate in
  // the additional same-direction correction, including nearby 0° tracks.
  const rotatedFlowShiftByIndex = new Map<number, number>();
  if (
    currentPositions.some((position) => !position.hidden && position.rotation)
  ) {
    const candidates = currentPositions.flatMap((position, index) =>
      position.hidden ? [] : [index],
    );
    const hitline = Math.min(
      ...candidates.map((index) => currentPositions[index].dy),
    );
    const geometries = candidates.map((index) => {
      const position = currentPositions[index];
      return computeTrackGeometry({
        keyX: position.dx,
        keyY: position.dy,
        keyWidth: position.width,
        keyHeight: position.height,
        rotation: position.rotation ?? 0,
        trackHeight,
        noteWidth: position.noteWidth,
        noteAlignment: position.noteAlignment,
        hitline: position.noteAutoYCorrection !== false ? hitline : undefined,
      });
    });
    for (const group of groupSameFlowAngles(
      candidates.map((index) => currentPositions[index].rotation ?? 0),
    )) {
      const origins = group.map((member) => geometries[member].origin);
      for (const member of group) {
        const index = candidates[member];
        const position = currentPositions[index];
        if (!position.rotation || position.noteAutoYCorrection === false)
          continue;
        const geometry = geometries[member];
        rotatedFlowShiftByIndex.set(
          index,
          sameFlowStartShift(geometry.origin, geometry.direction, origins),
        );
      }
    }
  }

  // bounds 계산
  const bounds: Bounds | null = (() => {
    const hasContent =
      currentPositions.length > 0 ||
      currentStatPositions.length > 0 ||
      currentGraphPositions.length > 0 ||
      currentKnobPositions.length > 0 ||
      currentSpritePositions.length > 0 ||
      (pluginElements && pluginElements.length > 0);
    if (!hasContent) return null;

    const xs: number[] = [];
    const ys: number[] = [];
    const widths: number[] = [];
    const heights: number[] = [];
    const includeRotation = (
      position: {
        dx: number;
        dy: number;
        width?: number;
        height?: number;
        rotation?: unknown;
      },
      width: number,
      height: number,
    ) => {
      if (typeof position.rotation !== 'number' || !position.rotation) return;
      const rect = rotatedRectAabb(
        position.dx,
        position.dy,
        position.width ?? width,
        position.height ?? height,
        position.rotation,
      );
      xs.push(rect.minX);
      ys.push(rect.minY);
      widths.push(rect.maxX);
      heights.push(rect.maxY);
    };

    currentPositions.forEach((pos, index) => {
      if (pos.hidden) return;
      xs.push(pos.dx);
      ys.push(pos.dy);
      widths.push(pos.dx + pos.width);
      heights.push(pos.dy + pos.height);
      if (pos.rotation) {
        const keyRect = rotatedRectAabb(
          pos.dx,
          pos.dy,
          pos.width,
          pos.height,
          pos.rotation,
        );
        const track = translateTrackGeometry(
          computeTrackGeometry({
            keyX: pos.dx,
            keyY: pos.dy,
            keyWidth: pos.width,
            keyHeight: pos.height,
            rotation: pos.rotation,
            trackHeight,
            noteWidth: pos.noteWidth,
            noteAlignment: pos.noteAlignment,
            noteOffsetX: pos.noteOffsetX,
            noteOffsetY: pos.noteOffsetY,
          }),
          rotatedFlowShiftByIndex.get(index) ?? 0,
        );
        xs.push(keyRect.minX, track.rect.minX);
        ys.push(keyRect.minY, track.rect.minY);
        widths.push(keyRect.maxX, track.rect.maxX);
        heights.push(keyRect.maxY, track.rect.maxY);
      }

      // 노트 오프셋에 의한 트랙 영역 확장 반영
      const userOffsetX = pos.noteOffsetX ?? 0;
      const userOffsetY = pos.noteOffsetY ?? 0;
      if (userOffsetX !== 0) {
        const keyWidth = pos.width;
        const desiredNoteWidth =
          typeof pos.noteWidth === 'number' && Number.isFinite(pos.noteWidth)
            ? Math.max(1, pos.noteWidth)
            : keyWidth;
        const noteAlign = pos.noteAlignment ?? 'center';
        const alignOff =
          noteAlign === 'left'
            ? 0
            : noteAlign === 'right'
              ? keyWidth - desiredNoteWidth
              : (keyWidth - desiredNoteWidth) / 2;
        const noteLeft = pos.dx + alignOff + userOffsetX;
        const noteRight = noteLeft + desiredNoteWidth;
        xs.push(noteLeft);
        widths.push(noteRight);
      }
      if (userOffsetY > 0) {
        heights.push(pos.dy + pos.height + userOffsetY);
      } else if (userOffsetY < 0) {
        ys.push(pos.dy + userOffsetY);
      }
    });

    currentStatPositions.forEach((pos) => {
      if (!pos || pos.hidden) return;
      includeRotation(pos, 60, 60);
      xs.push(pos.dx);
      ys.push(pos.dy);
      widths.push(pos.dx + (pos.width ?? 60));
      heights.push(pos.dy + (pos.height ?? 60));
    });

    currentGraphPositions.forEach((pos) => {
      if (!pos || pos.hidden) return;
      includeRotation(pos, 200, 100);
      xs.push(pos.dx);
      ys.push(pos.dy);
      widths.push(pos.dx + (pos.width ?? 200));
      heights.push(pos.dy + (pos.height ?? 100));
    });

    currentKnobPositions.forEach((pos) => {
      if (!pos || pos.hidden) return;
      includeRotation(pos, 80, 80);
      xs.push(pos.dx);
      ys.push(pos.dy);
      widths.push(pos.dx + (pos.width ?? 80));
      heights.push(pos.dy + (pos.height ?? 80));
    });

    const canonicalByTrigger = new Map<string, string>();
    currentPositions.forEach((position, index) => {
      if (position.id && currentKeys[index])
        canonicalByTrigger.set(position.id, currentKeys[index]);
    });
    for (const sprite of currentSpritePositions) {
      if (sprite.hidden) continue;
      const reach = computeSpriteReachAabb(sprite, canonicalByTrigger);
      if (!reach) continue;
      xs.push(sprite.dx + reach.minX);
      ys.push(sprite.dy + reach.minY);
      widths.push(sprite.dx + reach.maxX);
      heights.push(sprite.dy + reach.maxY);
    }

    // 플러그인 요소 위치 (앵커 기반 계산 포함)
    if (pluginElements && selectedKeyType) {
      pluginElements
        .filter(
          (el) => !el.hidden && (!el.tabId || el.tabId === selectedKeyType),
        )
        .forEach((element) => {
          let x = element.position.x;
          let y = element.position.y;

          if (element.anchor?.keyCode) {
            const keyIndex = currentKeys.findIndex(
              (key) => key === element.anchor?.keyCode,
            );
            if (keyIndex >= 0 && currentPositions[keyIndex]) {
              const keyPosition = currentPositions[keyIndex];
              const offsetX = element.anchor.offset?.x ?? 0;
              const offsetY = element.anchor.offset?.y ?? 0;
              x = keyPosition.dx + offsetX;
              y = keyPosition.dy + offsetY;
            }
          }

          const width =
            element.measuredSize?.width ?? element.estimatedSize?.width ?? 200;
          const height =
            element.measuredSize?.height ??
            element.estimatedSize?.height ??
            150;

          xs.push(x);
          ys.push(y);
          widths.push(x + width);
          heights.push(y + height);
        });
    }

    if (xs.length === 0) return null;

    return {
      minX: Math.min(...xs),
      minY: Math.min(...ys),
      maxX: Math.max(...widths),
      maxY: Math.max(...heights),
    };
  })();

  // 오프셋 계산
  const topOffset = trackHeight + PADDING;
  const offsetX = bounds ? PADDING - bounds.minX : 0;
  const offsetY = bounds ? topOffset - bounds.minY : 0;

  const applyOffset = <T extends { dx: number; dy: number }>(
    items: T[],
  ): T[] => {
    if (!bounds || !items.length) return items;
    return items.map((item) => ({
      ...item,
      dx: item.dx + offsetX,
      dy: item.dy + offsetY,
    }));
  };

  const displayPositions = applyOffset(currentPositions);
  const displayStatPositions = applyOffset(currentStatPositions);
  const displayGraphPositions = applyOffset(currentGraphPositions);
  const displayKnobPositions = applyOffset(currentKnobPositions);
  const displaySpritePositions = applyOffset(currentSpritePositions);

  const positionOffset = bounds ? { x: offsetX, y: offsetY } : { x: 0, y: 0 };

  const topMostY = bounds ? topOffset : 0;

  // WebGL 트랙 계산
  const webglTracks = currentKeys
    .map((key, index) => {
      const originalPosition = currentPositions[index] ?? FALLBACK_POSITION;
      if (originalPosition.hidden) return null;
      const position = displayPositions[index] ?? originalPosition;
      const useAutoCorrection = position.noteAutoYCorrection !== false;
      const trackStartY = useAutoCorrection ? topMostY : position.dy;
      const keyWidth = position.width;
      const desiredNoteWidth =
        typeof position.noteWidth === 'number' &&
        Number.isFinite(position.noteWidth)
          ? Math.max(1, position.noteWidth)
          : keyWidth;
      const noteAlign = position.noteAlignment ?? 'center';
      const userOffsetX = position.noteOffsetX ?? 0;
      const userOffsetY = position.noteOffsetY ?? 0;
      const geometry = translateTrackGeometry(
        computeTrackGeometry({
          keyX: position.dx,
          keyY: position.dy,
          keyWidth,
          keyHeight: position.height,
          rotation: position.rotation ?? 0,
          trackHeight,
          noteWidth: desiredNoteWidth,
          noteAlignment: noteAlign,
          noteOffsetX: userOffsetX,
          noteOffsetY: userOffsetY,
          hitline: trackStartY,
        }),
        rotatedFlowShiftByIndex.get(index) ?? 0,
      );

      return {
        trackKey: key,
        trackIndex: position.zIndex ?? index,
        position: {
          ...position,
          dx: geometry.origin.x,
          dy: geometry.origin.y,
        },
        direction: geometry.direction,
        width: desiredNoteWidth,
        height: trackHeight,
        noteColor: position.noteColor,
        noteGradient: position.noteGradient,
        noteGlowGradient: position.noteGlowGradient,
        noteBorderGradient: position.noteBorderGradient,
        noteOpacity: position.noteOpacity,
        noteOpacityTop: position.noteOpacityTop ?? position.noteOpacity,
        noteOpacityBottom: position.noteOpacityBottom ?? position.noteOpacity,
        noteGlowEnabled: position.noteGlowEnabled ?? false,
        noteGlowSize: position.noteGlowSize ?? 20,
        noteGlowOpacity: position.noteGlowOpacity ?? 70,
        noteGlowOpacityTop:
          position.noteGlowOpacityTop ?? position.noteGlowOpacity ?? 70,
        noteGlowOpacityBottom:
          position.noteGlowOpacityBottom ?? position.noteGlowOpacity ?? 70,
        // Keep absence meaningful: the v2 buffer inherits the body's entire
        // gradient when no independent glow paint was stored.
        noteGlowColor: position.noteGlowColor,
        flowSpeed: noteSettings?.speed ?? DEFAULT_NOTE_SETTINGS.speed,
        borderRadius: position.noteBorderRadius ?? DEFAULT_NOTE_BORDER_RADIUS,
        noteBorderWidth: position.noteBorderWidth ?? 0,
        noteBorderColor: position.noteBorderColor,
        noteBorderOpacity: position.noteBorderOpacity ?? 100,
        noteBorderSide: position.noteBorderSide ?? 'all',
      };
    })
    .filter(Boolean);

  return {
    bounds,
    displayPositions,
    displayStatPositions,
    displayGraphPositions,
    displayKnobPositions,
    displaySpritePositions,
    positionOffset,
    topMostY,
    webglTracks,
  };
}
