/* eslint-disable react-refresh/only-export-components */
import React, { useEffect, useMemo } from 'react';
import { createRoot } from 'react-dom/client';
import '@styles/viewer-compat-v1.css';
import './visual.css';
import DraggableKey, { Key } from '@components/shared/Key';
import OutsideCounter from '@components/overlay/counters/OutsideCounter';
import { WebGLTracksOGL } from '@components/overlay/WebGLTracksOGL';
import {
  getDefaultCounterSettings,
  getDefaultNoteSettings,
} from '@src/renderer/defaults';
import { NoteBuffer } from '@stores/signals/noteBuffer';
import { resetAllKeySignals, setKeyActive } from '@stores/signals/keySignals';
import {
  createDefaultGhostSettings,
  expandGhostKeys,
} from '@src/renderer/editor/model/ghostKeys';
import {
  resetAllCounters,
  setKeyCounter,
} from '@stores/signals/keyCounterSignals';
import type { KeyCounterSettings, KeyPosition } from '@src/types/key/keys';

type Surface = 'main' | 'overlay' | 'obs';
type FixtureId =
  'hand-default' | 'hand-custom-css' | 'foot-note-counter' | 'hand-ghost';

interface VisualFixture {
  id: FixtureId;
  viewerKind: 'hand' | 'foot';
  keys: string[];
  positions: KeyPosition[];
  hiddenKeyCount: number;
  noteEnabled: boolean;
}

declare global {
  interface Window {
    __VISUAL_READY__?: boolean;
    __VISUAL_SET_KEY__?: (key: string, active: boolean) => void;
  }
}

const counter = (
  placement: 'inside' | 'outside',
  overrides: Partial<KeyCounterSettings> = {},
): KeyCounterSettings => ({
  ...getDefaultCounterSettings(),
  placement,
  ...overrides,
});

const position = (
  dx: number,
  dy: number,
  overrides: Partial<KeyPosition> = {},
): KeyPosition => ({
  dx,
  dy,
  width: 88,
  height: 68,
  hidden: false,
  count: 0,
  noteColor: '#77C7FF',
  noteOpacity: 86,
  noteAlignment: 'center',
  noteEffectEnabled: true,
  noteGlowEnabled: false,
  noteGlowSize: 20,
  noteGlowOpacity: 70,
  noteAutoYCorrection: true,
  className: 'visual-key',
  backgroundColor: 'rgba(46, 46, 47, 0.9)',
  borderColor: 'rgba(121, 121, 121, 0.9)',
  borderWidth: 3,
  borderRadius: 10,
  fontSize: 16,
  fontColor: 'rgba(121, 121, 121, 0.9)',
  fontWeight: 700,
  counter: counter('inside'),
  ...overrides,
});

// E with a ghost on R: the ghost is a different key while it is held.
const ghostOwner = (() => {
  const owner = position(220, 470, {
    activeBackgroundColor: 'rgba(186, 113, 43, 0.9)',
    activeFontColor: '#FFFFFF',
    counter: counter('inside', { enabled: false }),
  });
  return {
    ...owner,
    ghost: {
      enabled: true,
      key: 'R',
      settings: {
        ...createDefaultGhostSettings(owner),
        activeBackgroundColor: 'rgba(69, 155, 248, 0.92)',
      },
    },
  };
})();

const FIXTURES: Record<FixtureId, VisualFixture> = {
  'hand-ghost': {
    id: 'hand-ghost',
    viewerKind: 'hand',
    keys: ['E'],
    positions: [ghostOwner],
    hiddenKeyCount: 0,
    noteEnabled: false,
  },
  'hand-default': {
    id: 'hand-default',
    viewerKind: 'hand',
    keys: ['A', 'S', 'D'],
    positions: [
      position(220, 470),
      position(360, 470, { counter: counter('outside', { align: 'top' }) }),
      position(500, 470, {
        activeBackgroundColor: 'rgba(69, 155, 248, 0.92)',
        activeBorderColor: '#A8D4FF',
        activeFontColor: '#FFFFFF',
      }),
    ],
    hiddenKeyCount: 0,
    noteEnabled: false,
  },
  'hand-custom-css': {
    id: 'hand-custom-css',
    viewerKind: 'hand',
    keys: ['A', 'S'],
    positions: [
      position(220, 470, {
        className: 'visual-key visual-custom-primary',
        useInlineStyles: false,
      }),
      position(360, 470, {
        className: 'visual-key visual-custom-sibling',
        useInlineStyles: false,
      }),
    ],
    hiddenKeyCount: 0,
    noteEnabled: false,
  },
  'foot-note-counter': {
    id: 'foot-note-counter',
    viewerKind: 'foot',
    keys: ['LEFT', 'RIGHT', 'SPACE'],
    positions: [
      position(420, 520, {
        width: 118,
        height: 62,
        displayText: 'LEFT',
        noteGlowEnabled: true,
        noteGlowSize: 20,
        counter: counter('outside', {
          align: 'top',
          stroke: { idle: '#202026', active: '#202026' },
        }),
      }),
      position(610, 520, {
        width: 118,
        height: 62,
        displayText: 'RIGHT',
        noteGlowEnabled: true,
        noteGlowSize: 20,
      }),
      position(800, 520, {
        hidden: true,
        displayText: 'HIDDEN',
        noteGlowEnabled: true,
        noteGlowSize: 20,
      }),
    ],
    hiddenKeyCount: 1,
    noteEnabled: true,
  },
};

const params = new URLSearchParams(window.location.search);
const requestedSurface = params.get('surface');
const requestedFixture = params.get('fixture');
const surface: Surface =
  requestedSurface === 'overlay' || requestedSurface === 'obs'
    ? requestedSurface
    : 'main';
const fixtureId: FixtureId =
  requestedFixture === 'hand-custom-css' ||
  requestedFixture === 'foot-note-counter' ||
  requestedFixture === 'hand-ghost'
    ? requestedFixture
    : 'hand-default';

function VisualViewer({ fixture }: { fixture: VisualFixture }) {
  const noteSettings = getDefaultNoteSettings();
  const noteBuffer = useMemo(() => {
    if (!fixture.noteEnabled) return null;
    const buffer = new NoteBuffer();
    const visibleTracks = fixture.positions
      .map((item, index) => ({ item, index }))
      .filter(({ item }) => !item.hidden);
    buffer.updateTrackLayouts(
      visibleTracks.map(({ item, index }) => ({
        trackKey: `${fixture.viewerKind}:${fixture.keys[index]}`,
        trackIndex: index,
        position: { dx: item.dx, dy: item.dy },
        width: item.noteWidth ?? item.width,
        height: noteSettings.trackHeight,
        noteColor: item.noteColor,
        noteOpacity: item.noteOpacity,
        noteGlowEnabled: item.noteGlowEnabled,
        noteGlowSize: item.noteGlowSize,
        noteGlowOpacity: item.noteGlowOpacity,
        noteGlowColor: item.noteGlowColor,
        borderRadius: item.noteBorderRadius,
      })),
    );
    visibleTracks.forEach(({ index }) => {
      const key = fixture.keys[index];
      buffer.allocate(
        `${fixture.viewerKind}:${key}`,
        `baseline:${fixture.viewerKind}:${key}`,
        1,
      );
    });
    return buffer;
  }, [fixture, noteSettings.trackHeight]);

  useEffect(() => {
    resetAllKeySignals();
    resetAllCounters();
    fixture.keys.forEach((key, index) => {
      setKeyActive(key, index === fixture.keys.length - 1);
      setKeyCounter(fixture.viewerKind, key, (index + 1) * 12);
    });

    window.__VISUAL_SET_KEY__ = setKeyActive;
    const settle = async () => {
      await document.fonts.ready;
      await new Promise<void>((resolve) =>
        requestAnimationFrame(() => resolve()),
      );
      await new Promise<void>((resolve) =>
        requestAnimationFrame(() => resolve()),
      );
      if (fixture.noteEnabled) {
        await new Promise<void>((resolve) => window.setTimeout(resolve, 900));
      }
      window.__VISUAL_READY__ = true;
    };
    void settle();

    return () => {
      window.__VISUAL_READY__ = false;
      resetAllKeySignals();
      resetAllCounters();
    };
  }, [fixture]);

  const expanded = useMemo(
    () => expandGhostKeys(fixture.keys, fixture.positions),
    [fixture],
  );
  const visibleEntries = expanded.positions
    .map((item, index) => ({
      item,
      index,
      key: expanded.keys[index],
    }))
    .filter(({ item }) => !item.hidden);

  return (
    <main
      className={`visual-surface-${surface}`}
      data-visual-root="true"
      data-surface={surface}
      data-fixture={fixture.id}
      data-viewer-kind={fixture.viewerKind}
      data-hidden-key-count={fixture.hiddenKeyCount}
      data-note-enabled={fixture.noteEnabled}
      data-note-speed={noteSettings.speed}
      data-track-height={noteSettings.trackHeight}
      data-note-glow-size={fixture.positions[0]?.noteGlowSize ?? 20}
    >
      {noteBuffer ? (
        <WebGLTracksOGL
          tracks={[]}
          notesRef={null}
          subscribe={() => () => undefined}
          noteSettings={noteSettings}
          noteBuffer={noteBuffer}
        />
      ) : null}

      <section data-viewer-layer="keys">
        {visibleEntries.map(({ item, index, key }) =>
          surface === 'main' ? (
            <DraggableKey
              key={key}
              index={index}
              elementId={`visual-${fixture.id}-${key}`}
              position={item}
              keyName={key}
              onPositionChange={() => undefined}
              counterEnabled
              counterPreviewValue={(index + 1) * 12}
            />
          ) : (
            <Key
              key={key}
              keyName={item.displayText || key}
              globalKey={key}
              pairedKey={expanded.pairedKeys?.[index]}
              position={item}
              mode={fixture.viewerKind}
              counterEnabled
            />
          ),
        )}
      </section>

      <section data-viewer-layer="outside-counters">
        {visibleEntries.map(({ item, index, key }) =>
          item.counter?.placement === 'outside' ? (
            <OutsideCounter
              key={key}
              position={item}
              count={(index + 1) * 12}
              active={index === fixture.keys.length - 1}
              globalKey={key}
            />
          ) : null,
        )}
      </section>
    </main>
  );
}

const root = document.getElementById('root');
if (!root) throw new Error('Visual fixture root not found');
root.dataset.dmnViewerSurface = '';

createRoot(root).render(<VisualViewer fixture={FIXTURES[fixtureId]} />);
