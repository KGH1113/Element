import { lazy, Suspense, useEffect, useMemo } from 'react';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { usePartPreviewStore } from '@src/renderer/editor/runtime/partPreview';
import { useChoiceChanges } from '@src/renderer/editor/runtime/choicePreview';
import { computeLayout } from '@hooks/shared/useLayoutComputation';
import { useNoteSystem } from '@hooks/overlay/useNoteSystem';
import { mergeNoteSettings } from '@src/types/settings/noteSettings';
import { trackRectFromOrigin } from '@utils/layout/trackGeometry';
import { hasSpriteId } from '@utils/sprite/spriteIdentity';
import type { TrackLayoutInput } from '@stores/signals/noteBuffer';

// The overlay's own note renderer, loaded only when notes are previewed.
const Tracks = lazy(async () => {
  const mod = await import('@components/overlay/WebGLTracksOGL');
  return { default: mod.WebGLTracksOGL };
});

/** A key no keyboard has: the preview's notes are its own. */
const PREVIEW_TRACK = 'element:note-preview';
/** Room around the lane for the glow of its notes. */
const ROOM = 64;
const UP = { x: 0, y: -1 };

/**
 * The notes of the selected key, rising on the editor canvas while its notes
 * are previewed (see partPreview.ts). The lane is laid out exactly as the
 * overlay lays it out, so the notes start where they will in the game.
 */
export default function KeyNotePreviewLayer({
  viewerId,
}: {
  viewerId: string;
}) {
  const shown = usePartPreviewStore((state) => state.part === 'note');
  const index = useGridSelectionStore((state) => {
    const [only, ...rest] = state.selectedElements;
    return only && rest.length === 0 && only.type === 'key'
      ? (only.index ?? null)
      : null;
  });
  if (!shown || index === null) return null;
  return <NotePreview viewerId={viewerId} index={index} />;
}

function NotePreview({ viewerId, index }: { viewerId: string; index: number }) {
  const keys = useKeyStore((state) => state.keyMappings[viewerId]);
  const storedPositions = useKeyStore((state) => state.positions[viewerId]);
  const stats = useStatItemStore((state) => state.positions[viewerId]);
  const graphs = useGraphItemStore((state) => state.positions[viewerId]);
  const knobs = useKnobItemStore((state) => state.positions[viewerId]);
  const sprites = useSpriteStore((state) => state.positions[viewerId]);
  const globalNotes = useSettingsStore((state) => state.noteSettings);
  const tabNotes = useSettingsStore(
    (state) => state.tabNoteOverrides?.[viewerId],
  );
  const overlayPadding = useSettingsStore(
    (state) => state.gridSettings.overlayPadding ?? 30,
  );
  const noteSettings = useMemo(
    () => mergeNoteSettings(globalNotes, tabNotes),
    [globalNotes, tabNotes],
  );
  // Where the notes rise from, as the pointer rests on a choice of it.
  const choice = useChoiceChanges('key', index);
  const positions = useMemo(
    () =>
      choice && storedPositions?.[index]
        ? storedPositions.map((item, at) =>
            at === index ? { ...item, ...choice } : item,
          )
        : storedPositions,
    [storedPositions, choice, index],
  );

  const lane = useMemo(() => {
    const position = positions?.[index];
    if (!keys || !positions || !position || position.hidden) return null;
    if (position.noteEffectEnabled === false) return null;
    const layout = computeLayout({
      currentKeys: keys,
      currentPositions: positions,
      currentStatPositions: stats ?? [],
      currentGraphPositions: graphs ?? [],
      currentKnobPositions: knobs ?? [],
      currentSpritePositions: (sprites ?? []).filter(hasSpriteId),
      trackHeight: noteSettings.trackHeight,
      noteSettings,
      selectedKeyType: viewerId,
      overlayPadding,
    });
    // Tracks are made for the keys that are shown, in their order.
    const shownIndices = positions.flatMap((item, at) =>
      item.hidden ? [] : [at],
    );
    const track = layout.webglTracks[shownIndices.indexOf(index)];
    if (!track) return null;
    // Back from the overlay's frame to the canvas.
    const origin = {
      x: track.position.dx - layout.positionOffset.x,
      y: track.position.dy - layout.positionOffset.y,
    };
    const rect = trackRectFromOrigin(
      origin,
      track.direction ?? UP,
      noteSettings.trackHeight,
      track.width,
    );
    const left = rect.minX - ROOM;
    const top = rect.minY - ROOM;
    // The renderer draws in the box it is put in: the lane is moved into it.
    const local: TrackLayoutInput = {
      ...track,
      trackKey: PREVIEW_TRACK,
      position: { dx: origin.x - left, dy: origin.y - top },
    };
    return {
      left,
      top,
      width: rect.maxX - rect.minX + ROOM * 2,
      height: rect.maxY - rect.minY + ROOM * 2,
      track: local,
    };
  }, [
    keys,
    positions,
    stats,
    graphs,
    knobs,
    sprites,
    index,
    noteSettings,
    viewerId,
    overlayPadding,
  ]);

  const {
    subscribe,
    noteBuffer,
    handleKeyDown,
    handleKeyUp,
    finalizeAllActive,
    updateTrackLayouts,
  } = useNoteSystem({ noteEffect: true, noteSettings });

  useEffect(() => {
    if (lane) updateTrackLayouts([lane.track]);
  }, [lane, updateTrackLayouts]);

  // A note starts when the preview presses the key and ends when it lets go.
  useEffect(
    () =>
      usePartPreviewStore.subscribe((state, before) => {
        if (state.pressed === before.pressed) return;
        if (state.pressed) handleKeyDown(PREVIEW_TRACK);
        else handleKeyUp(PREVIEW_TRACK);
      }),
    [handleKeyDown, handleKeyUp],
  );

  useEffect(() => () => finalizeAllActive(), [finalizeAllActive]);

  const tracks = useMemo(() => (lane ? [lane.track] : []), [lane]);
  if (!lane) return null;
  return (
    <div
      className="pointer-events-none"
      aria-hidden="true"
      style={{
        position: 'absolute',
        left: lane.left,
        top: lane.top,
        width: lane.width,
        height: lane.height,
      }}
    >
      <Suspense fallback={null}>
        <Tracks
          tracks={tracks}
          notesRef={null}
          subscribe={subscribe}
          noteSettings={noteSettings}
          noteBuffer={noteBuffer}
        />
      </Suspense>
    </div>
  );
}
