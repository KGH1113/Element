import React, { useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import {
  TABS,
  type TabType,
} from '@components/main/Grid/PropertiesPanel/types';
import '@styles/viewer-compat-v1.css';
import '@styles/tokens.css';
import '@styles/chrome-global.css';
import '@styles/chrome-main.css';
import './shell.css';

type TauriCallback = (payload: unknown) => void;

const setupTauriFixture = (): void => {
  let callbackId = 0;
  const callbacks = new Map<number, TauriCallback>();
  const fixtureInvoke = async (command: string): Promise<unknown> => {
    if (command === 'settings_get') return { language: 'ko' };
    if (command === 'overlay_get') {
      return { visible: true, locked: false, anchor: 'top-left' };
    }
    if (command === 'obs_status') {
      return { running: false, port: 17474, clientCount: 0 };
    }
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
  requestedState === 'tabs'
    ? requestedState
    : 'editor';

const bootstrap = async (): Promise<void> => {
  await import('@api/dmnoteApi');
  const [
    { I18nProvider },
    { default: TitleBar },
    { default: ToolBar },
    { default: Settings },
    { default: FloatingPopup },
    { default: Palette },
    { default: FontManagerModal },
    { default: SoundManagerModal },
    propertyInputs,
    panelChrome,
    { useSettingsStore },
    { useKeyStore },
    { useFontStore },
  ] = await Promise.all([
    import('@contexts/I18nContext'),
    import('@components/main/TitleBar'),
    import('@components/main/Tool/ToolBar'),
    import('@components/main/Settings'),
    import('@components/main/Modal/FloatingPopup'),
    import('@components/main/Modal/content/pickers/Palette'),
    import('@components/main/Modal/content/managers/FontManagerModal'),
    import('@components/main/Modal/content/managers/SoundManagerModal'),
    import('@components/main/Grid/PropertiesPanel/PropertyInputs'),
    import('@components/main/Grid/PropertiesPanel/panelChrome'),
    import('@stores/useSettingsStore'),
    import('@stores/data/useKeyStore'),
    import('@stores/useFontStore'),
  ]);

  useSettingsStore.setState({
    noteEffect: true,
    useCustomCSS: true,
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
  useKeyStore.setState({
    isBootstrapped: true,
    ...(fixtureState === 'tabs'
      ? {
          selectedKeyType: 'hand-main',
          selectedViewerTabs: { hand: 'hand-main', foot: 'foot-main' },
          tabs: [
            { id: 'hand-main', name: 'Hand 1', viewerKind: 'hand' as const },
            { id: 'foot-main', name: 'Foot 1', viewerKind: 'foot' as const },
          ],
        }
      : {}),
  });
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

  const { NumberInput, PropertyRow, Tabs, ToggleSwitch } = propertyInputs;
  const { PANEL_ROOT_CLASS } = panelChrome;

  const PropertyPanelFixture = (): React.ReactElement => {
    const [tab, setTab] = useState<TabType>(TABS.STYLE);
    const [enabled, setEnabled] = useState(true);
    return (
      <aside className={PANEL_ROOT_CLASS} aria-label="Properties fixture">
        <div className="dmn-properties-panel__head-stack">
          <div className="dmn-properties-panel__header flex items-center justify-between px-[12px]">
            <div>
              <strong className="text-title block text-ui-fg-primary">
                Primary Key
              </strong>
              <span className="text-caption text-ui-fg-muted">
                Key 32 · Space
              </span>
            </div>
            <button
              type="button"
              className="dmn-panel-icon-button"
              aria-label="Close panel"
            >
              ×
            </button>
          </div>
          <div className="px-[12px] pb-[10px]">
            <Tabs
              activeTab={tab}
              onTabChange={setTab}
              t={(key) =>
                key.endsWith('tabStyle')
                  ? 'Style'
                  : key.endsWith('tabNote')
                    ? 'Note'
                    : 'Counter'
              }
            />
          </div>
        </div>
        <div className="visual-shell-panel-copy">
          <PropertyRow label="Position">
            <NumberInput value={128} prefix="X" onChange={() => undefined} />
            <NumberInput value={72} prefix="Y" onChange={() => undefined} />
          </PropertyRow>
          <PropertyRow label="Size">
            <NumberInput value={96} prefix="W" onChange={() => undefined} />
            <NumberInput value={64} prefix="H" onChange={() => undefined} />
          </PropertyRow>
          <div className="dmn-property-divider h-px w-full" />
          <PropertyRow label="Custom style">
            <ToggleSwitch checked={enabled} onChange={setEnabled} />
          </PropertyRow>
          <p className="text-caption mt-auto text-ui-fg-muted">
            Changes are previewed on the canvas and saved with the current tab.
          </p>
        </div>
      </aside>
    );
  };

  const ShellFixture = (): React.ReactElement => {
    const isSettings =
      fixtureState === 'settings' || fixtureState === 'manager';
    const [paletteOpen, setPaletteOpen] = useState(false);
    const [activeTool, setActiveTool] = useState('move');
    const paletteRef = useRef<HTMLButtonElement>(null);

    return (
      <div
        data-dmn-app-chrome
        data-shell-root="true"
        data-shell-state={fixtureState}
        className="visual-shell-root dmn-app-frame flex flex-col overflow-hidden"
      >
        <TitleBar />
        <main className="dmn-workspace relative flex-1 overflow-hidden">
          {isSettings ? (
            <Settings
              showAlert={() => undefined}
              showConfirm={() => undefined}
            />
          ) : (
            <>
              <div className="visual-shell-canvas" data-dmn-viewer-surface>
                <div className="visual-shell-selection" />
                <div className="visual-shell-minimap" />
              </div>
              <PropertyPanelFixture />
            </>
          )}
        </main>
        <ToolBar
          onAddItem={() => undefined}
          onTogglePalette={() => setPaletteOpen((open) => !open)}
          isPaletteOpen={paletteOpen}
          onResetCurrentMode={() => undefined}
          onResetCounters={() => undefined}
          activeTool={activeTool}
          setActiveTool={setActiveTool}
          isSettingsOpen={isSettings}
          onOpenSettings={() => undefined}
          onCloseSettings={() => undefined}
          onOpenNoteSetting={() => undefined}
          primaryButtonRef={paletteRef}
        />
        <FloatingPopup
          open={paletteOpen}
          referenceRef={paletteRef}
          placement="top"
          offset={12}
          portal
          onClose={() => setPaletteOpen(false)}
        >
          <Palette color="#ba712b" onColorChange={() => undefined} />
        </FloatingPopup>
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
