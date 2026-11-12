import React from 'react';
import { keyPositionSchema, type KeyPosition } from '@src/types/key/keys';
import { createRoot } from 'react-dom/client';
import '@styles/viewer-compat-v1.css';
import '@styles/tokens.css';
import '@styles/chrome-global.css';
import '@styles/chrome-main.css';
import './shell.css';

type TauriCallback = (payload: unknown) => void;

const setupTauriFixture = (): void => {
  let callbackId = 0;
  const callbacks = new Map<number, TauriCallback>();
  const settings: Record<string, unknown> = {
    language:
      new URLSearchParams(window.location.search).get('language') ?? 'ko',
  };
  const saves: unknown[] = [];
  Object.assign(window, { __VISUAL_SAVES__: saves });
  const fixtureInvoke = async (
    command: string,
    args?: Record<string, unknown>,
  ): Promise<unknown> => {
    if (command === 'settings_get') return settings;
    if (command === 'settings_update') {
      Object.assign(settings, args?.patch);
      return { ...settings };
    }
    if (command === 'overlay_get') {
      return { visible: true, locked: false, anchor: 'top-left' };
    }
    if (command === 'obs_status' || command === 'obs_stop') {
      return { running: false, port: 17474, clientCount: 0 };
    }
    if (command === 'obs_start') {
      return {
        running: true,
        port: 17474,
        clientCount: 0,
        localIp: '192.168.0.7',
        token: 'fixture',
      };
    }
    if (command === 'css_history_get' || command === 'counter_animation_list')
      return [];
    if (command === 'key_sound_list_output_devices') {
      return { defaultDevice: true, asio: ['Fixture ASIO'] };
    }
    if (command === 'key_sound_get_output_state') {
      return {
        requested: { kind: 'defaultDevice' },
        effective: { kind: 'defaultDevice' },
        error: null,
        errorCode: null,
        asioAvailable: true,
      };
    }
    if (command === 'sound_list') {
      return [
        {
          soundPath: '/fixtures/mechanical.wav',
          fileName: 'mechanical.wav',
          displayName: 'Mechanical Click',
          sizeBytes: 18432,
          enabled: true,
          source: 'local',
        },
        {
          soundPath: '/fixtures/soft.wav',
          fileName: 'soft.wav',
          displayName: 'Soft Tap',
          sizeBytes: 9216,
          enabled: false,
          source: 'local',
        },
      ];
    }
    if (command === 'plugin:event|listen') return callbackId + 100;
    // Elements are saved as the sidebar and the canvas change them. The
    // fixture takes every save and counts them, so a probe can tell one save
    // from many.
    if (command === 'editor_get') {
      return {
        revision: saves.length,
        document: {
          schemaVersion: 1,
          keys: {},
          keyPositions: {},
          statPositions: {},
          graphPositions: {},
          knobPositions: {},
          spritePositions: {},
          layerGroups: {},
        },
      };
    }
    if (command === 'editor_commit') {
      const request = args?.request as
        { changes?: Record<string, unknown> } | undefined;
      saves.push(request);
      return {
        revision: saves.length,
        changedFields: Object.keys(request?.changes ?? {}).filter(
          (field) => field !== 'schemaVersion',
        ),
      };
    }
    return undefined;
  };

  Object.assign(window as unknown as Record<string, unknown>, {
    __TAURI_INTERNALS__: {
      invoke: fixtureInvoke,
      transformCallback: (callback: TauriCallback, once = false) => {
        const id = ++callbackId;
        callbacks.set(id, (payload) => {
          callback(payload);
          if (once) callbacks.delete(id);
        });
        return id;
      },
      unregisterCallback: (id: number) => callbacks.delete(id),
      callbacks,
    },
  });
};

setupTauriFixture();
window.__dmn_window_type = 'main';

const params = new URLSearchParams(window.location.search);
const requestedState = params.get('state');
const fixtureState =
  requestedState === 'settings' ||
  requestedState === 'manager' ||
  requestedState === 'font' ||
  requestedState === 'sound' ||
  requestedState === 'gallery' ||
  requestedState === 'key'
    ? requestedState
    : 'editor';
// Every state but settings shows the editor: the canvas beside the sidebar.
const isEditorState = fixtureState !== 'settings' && fixtureState !== 'manager';

const bootstrap = async (): Promise<void> => {
  await import('@api/dmnoteApi');
  // The images of the tab, drawn as the real canvas draws them.
  const { default: ShellSprites } = await import('./ShellSprites');
  const [
    { I18nProvider },
    { default: TitleBar },
    { default: Settings },
    { default: FontManagerModal },
    { default: SoundManagerModal },
    { default: PropertiesPanel },
    { default: Sidebar },
    { default: ElementListPage },
    { default: TopBar },
    { default: ViewerGallery },
    { default: ViewerPicker },
    { ChooserScopeProvider },
    { useSettingsStore },
    { useKeyStore },
    { useFontStore },
    { useGridSelectionStore },
  ] = await Promise.all([
    import('@contexts/I18nContext'),
    import('@components/main/TitleBar'),
    import('@components/main/Settings'),
    import('@components/main/Modal/content/managers/FontManagerModal'),
    import('@components/main/Modal/content/managers/SoundManagerModal'),
    import('@components/main/Grid/PropertiesPanel'),
    import('@components/main/Sidebar/Sidebar'),
    import('@components/main/Sidebar/ElementListPage'),
    import('@components/main/TopBar/TopBar'),
    import('@components/main/Viewers/ViewerGallery'),
    import('@components/main/Viewers/ViewerPicker'),
    import('@components/main/Sidebar/ChooserScopeProvider'),
    import('@stores/useSettingsStore'),
    import('@stores/data/useKeyStore'),
    import('@stores/useFontStore'),
    import('@stores/grid/useGridSelectionStore'),
  ]);

  useSettingsStore.setState({
    // `css=on` shows the rows that only exist while custom CSS is in use.
    useCustomCSS: !isEditorState || params.get('css') === 'on',
    customCSSPath: '/fixtures/viewer.css',
    useCustomJS: true,
    jsPlugins: [
      {
        id: 'fixture-input-meter',
        name: 'Input Meter',
        path: '/fixtures/input-meter.js',
        content: '',
        enabled: true,
      },
      {
        id: 'fixture-session-clock',
        name: 'Session Clock',
        path: '/fixtures/session-clock.js',
        content: '',
        enabled: false,
      },
    ],
  });
  useKeyStore.setState({ isBootstrapped: true });
  if (isEditorState) {
    const selectionMode = params.get('selection');
    const isBatchSelection = selectionMode === 'batch';
    // `viewer=hand-second` opens the ten-key one, whose list is long.
    const handViewer =
      params.get('viewer') === 'hand-second' ? 'hand-second' : 'hand-default';
    useKeyStore.setState({
      selectedKeyType: handViewer,
      selectedViewerTabs: { hand: handViewer, foot: 'foot-default' },
      tabs: [
        { id: 'hand-default', name: '16K', viewerKind: 'hand' as const },
        { id: 'hand-second', name: '10K', viewerKind: 'hand' as const },
        { id: 'hand-third', name: '4K', viewerKind: 'hand' as const },
        { id: 'foot-default', name: 'Pedal', viewerKind: 'foot' as const },
      ],
      keyMappings: {
        'hand-default': isBatchSelection ? ['SPACE', 'ENTER'] : ['SPACE'],
      },
      positions: {
        'hand-second': Array.from({ length: 10 }, (_, index) =>
          keyPositionSchema.parse({
            dx: (index % 5) * 68,
            dy: Math.floor(index / 5) * 68,
            width: 60,
            height: 60,
            count: 0,
            noteColor: '#f59e0b',
            noteOpacity: 100,
            useInlineStyles: true,
          }),
        ),
        'hand-third': Array.from({ length: 4 }, (_, index) =>
          keyPositionSchema.parse({
            dx: index * 68,
            dy: 0,
            width: 60,
            height: 60,
            count: 0,
            noteColor: '#f59e0b',
            noteOpacity: 100,
            useInlineStyles: true,
          }),
        ),
        'foot-default': Array.from({ length: 2 }, (_, index) =>
          keyPositionSchema.parse({
            dx: index * 110,
            dy: 0,
            width: 100,
            height: 60,
            count: 0,
            noteColor: '#f59e0b',
            noteOpacity: 100,
            useInlineStyles: true,
          }),
        ),
        'hand-default': [
          keyPositionSchema.parse({
            dx: 258,
            dy: 106,
            width: 142,
            height: 82,
            count: 128,
            layerName: 'Space',
            noteColor: '#f59e0b',
            noteOpacity: 100,
            useInlineStyles: true,
          }),
          ...(isBatchSelection
            ? [
                keyPositionSchema.parse({
                  dx: 420,
                  dy: 106,
                  width: 142,
                  height: 82,
                  count: 64,
                  layerName: 'Enter',
                  noteColor: '#38bdf8',
                  noteOpacity: 100,
                  useInlineStyles: true,
                }),
              ]
            : []),
        ],
      },
    });
    useGridSelectionStore.setState({
      selectedElements:
        selectionMode === 'none'
          ? []
          : [
              { type: 'key', id: 'key-0', index: 0 },
              ...(isBatchSelection
                ? [{ type: 'key' as const, id: 'key-1', index: 1 }]
                : []),
            ],
    });
    // Other kinds of element, and a key with a ghost: see shellSelections.ts.
    const { applyShellSelection } = await import('./shellSelections');
    applyShellSelection(selectionMode, handViewer);
  }
  useFontStore.setState({
    customFonts: [
      {
        id: 'fixture-inter',
        type: 'web',
        name: 'Inter',
        displayName: 'Inter Variable',
        enabled: true,
        cssContent:
          '@font-face { font-family: Inter; src: url(fixture.woff2); }',
      },
      {
        id: 'fixture-display',
        type: 'web',
        name: 'DM Display',
        displayName: 'DM Display',
        enabled: false,
        cssContent:
          '@font-face { font-family: DM Display; src: url(fixture.woff2); }',
      },
    ],
  });

  const KeyPanelFixture = (): React.ReactElement => {
    // The components here are locals of `bootstrap`; the compiler would hoist
    // the render callback out of their scope.
    'use no memo';
    const selectElement = useGridSelectionStore((state) => state.selectElement);
    const selected = useGridSelectionStore((state) =>
      state.isSelected('key-0'),
    );
    const position = useKeyStore((state) => state.positions['hand-default'][0]);
    const keyName = useKeyStore(
      (state) => state.keyMappings['hand-default'][0],
    );
    const onKeyUpdate = ({
      index,
      ...update
    }: Partial<KeyPosition> & { index: number }) => {
      useKeyStore.setState((state) => ({
        positions: {
          ...state.positions,
          'hand-default': state.positions['hand-default'].map(
            (key, keyIndex) =>
              keyIndex === index ? { ...key, ...update } : key,
          ),
        },
      }));
    };
    return (
      <>
        <div className="visual-shell-canvas" data-dmn-viewer-surface>
          <ViewerPicker onChoose={() => undefined} />
          <div
            data-key-element
            className={`visual-shell-key ${selected ? 'visual-shell-selection' : ''}`}
            onMouseDown={() =>
              selectElement({ type: 'key', id: 'key-0', index: 0 })
            }
            style={{
              left: position.dx,
              top: position.dy,
              width: position.width,
              height: position.height,
              backgroundColor: position.backgroundColor,
              color: position.fontColor,
              fontSize: position.fontSize,
              borderRadius: position.borderRadius,
            }}
          >
            {position.displayText || (keyName === 'SPACE' ? 'Space' : keyName)}
          </div>
          <ShellSprites />
          <div className="visual-shell-minimap" />
        </div>
        <Sidebar>
          <PropertiesPanel
            onKeyUpdate={onKeyUpdate}
            onKeyPreview={(index, update) => onKeyUpdate({ index, ...update })}
            onPositionChange={(index, dx, dy) => onKeyUpdate({ index, dx, dy })}
            onKeyMappingChange={(index, newKey) => {
              useKeyStore.setState((state) => ({
                keyMappings: {
                  ...state.keyMappings,
                  'hand-default': state.keyMappings['hand-default'].map(
                    (key, keyIndex) => (keyIndex === index ? newKey : key),
                  ),
                },
              }));
            }}
            renderElementList={(slots) => (
              <ElementListPage {...slots} onAddItem={() => undefined} />
            )}
          />
        </Sidebar>
      </>
    );
  };

  const ShellFixture = (): React.ReactElement => {
    const isSettings =
      fixtureState === 'settings' || fixtureState === 'manager';

    return (
      <ChooserScopeProvider>
        <div
          data-dmn-app-chrome
          data-shell-root="true"
          data-shell-state={fixtureState}
          className="visual-shell-root dmn-app-frame flex flex-col overflow-hidden"
        >
          <TitleBar>
            {!isSettings && fixtureState !== 'gallery' && (
              <TopBar
                onOpenSettings={() => undefined}
                onOpenOutputs={() => undefined}
              />
            )}
          </TitleBar>
          <main className="dmn-workspace relative flex min-h-0 min-w-0 flex-1 overflow-clip">
            {isSettings ? (
              <Settings
                showAlert={() => undefined}
                showConfirm={() => undefined}
                onClose={() => undefined}
              />
            ) : fixtureState === 'gallery' ? (
              <ViewerGallery
                slot="hand"
                onClose={() => undefined}
                showConfirm={() => undefined}
                showAlert={() => undefined}
              />
            ) : (
              <KeyPanelFixture />
            )}
          </main>
          <FontManagerModal
            isOpen={fixtureState === 'font'}
            onClose={() => undefined}
            t={(key) => key.split('.').at(-1) ?? key}
          />
          <SoundManagerModal
            isOpen={fixtureState === 'sound'}
            selectedSound="/fixtures/mechanical.wav"
            onSelectSound={() => undefined}
            onClose={() => undefined}
          />
        </div>
      </ChooserScopeProvider>
    );
  };

  const root = document.getElementById('root');
  if (!root) throw new Error('Shell fixture root not found');
  createRoot(root).render(
    <I18nProvider>
      <ShellFixture />
    </I18nProvider>,
  );
  await document.fonts.ready;
  await new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );
  window.__VISUAL_READY__ = true;
};

void bootstrap();
