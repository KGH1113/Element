import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';
import { obsApi } from '@api/modules/obsApi';
import type { SettingsSection } from './SettingsPanel/settingsNavigation';
import Settings from './Settings';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { changeLanguage: vi.fn() },
  }),
}));
vi.mock('@utils/core/platform', () => ({ isMac: () => false }));
vi.mock('@components/main/common/Dropdown', () => ({ default: () => null }));
vi.mock('@components/main/Modal/content/managers/PluginManagerModal', () => ({
  PluginManagerModal: () => null,
}));
vi.mock('@components/main/Modal/content/dialogs/PluginDataDeleteModal', () => ({
  PluginDataDeleteModal: () => null,
}));
vi.mock(
  '@components/main/Modal/content/settings/ShortcutSettingsModal',
  () => ({ default: () => null }),
);
vi.mock('@api/modules/obsApi', () => ({
  obsApi: {
    status: vi.fn(),
    start: vi.fn(),
    stop: vi.fn(),
    regenerateToken: vi.fn(),
    onStatus: () => vi.fn(),
  },
}));
vi.mock('@api/modules/resourceApi', () => ({
  keySoundOutputApi: {
    listDevices: vi.fn(async () => ({ asio: [], system: [] })),
    getState: vi.fn(async () => ({ requested: { kind: 'defaultDevice' } })),
  },
}));

describe('settings workspace', () => {
  let container: HTMLDivElement;
  let root: Root;
  const update = vi.fn(async (_settings: unknown) => ({}));
  const showAlert = vi.fn();
  const showConfirm = vi.fn();
  const onClose = vi.fn();
  const setVisible = vi.fn(async (_visible: boolean) => undefined);
  const setLock = vi.fn(async (_locked: boolean) => undefined);
  // What the app reports for the desktop key viewer and the page server.
  let desktopShown = false;
  const serverOff = { running: false, port: 34891, clientCount: 0 };
  const serverForObs = {
    running: true,
    port: 34891,
    clientCount: 0,
    token: 'secret',
    localIp: '192.168.0.7',
  };
  const originalApi = window.api;
  const originalScrollTo = HTMLElement.prototype.scrollTo;

  const button = (text: string) =>
    Array.from(container.querySelectorAll<HTMLButtonElement>('button')).find(
      (item) => item.textContent === text,
    )!;
  const toggle = (name: string) =>
    container.querySelector<HTMLButtonElement>(
      `[role="switch"][aria-label="${name}"]`,
    )!;
  /** The large button that chooses a place. */
  const place = (output: string) =>
    container.querySelector<HTMLButtonElement>(
      `[role="radio"][data-output-button="${output}"]`,
    )!;
  /** The settings of the chosen place, or the key limiter's group. */
  const group = (output: string) =>
    container.querySelector<HTMLElement>(
      `[data-output="${output}"], [data-output-group="${output}"]`,
    )!;
  /** What the large button of a place says the place is doing. */
  const stateOf = (output: string) =>
    container.querySelector(`[data-output-button="${output}"] .en-output-state`)
      ?.textContent;
  const rowLabels = (output: string) =>
    Array.from(
      group(output).querySelectorAll('.dmn-setting-row__label'),
      (label) => label.textContent,
    );
  const open = async (initialSection?: SettingsSection) => {
    await act(async () =>
      root.render(
        <Settings
          showAlert={showAlert}
          showConfirm={showConfirm}
          onClose={onClose}
          initialSection={initialSection}
        />,
      ),
    );
  };

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    HTMLElement.prototype.scrollTo = vi.fn();
  });
  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
    window.api = originalApi;
    HTMLElement.prototype.scrollTo = originalScrollTo;
  });
  beforeEach(async () => {
    update.mockReset().mockResolvedValue({});
    showAlert.mockClear();
    showConfirm.mockClear();
    onClose.mockClear();
    setVisible.mockReset().mockResolvedValue(undefined);
    setLock.mockReset().mockResolvedValue(undefined);
    desktopShown = false;
    vi.mocked(obsApi.status).mockReset().mockResolvedValue(serverOff);
    vi.mocked(obsApi.start).mockReset().mockResolvedValue(serverForObs);
    vi.mocked(obsApi.stop).mockReset().mockResolvedValue(serverOff);
    window.api = {
      settings: { update },
      css: { history: { list: vi.fn(async () => [{ path: '/theme.css' }]) } },
      overlay: {
        get: vi.fn(async () => ({ visible: desktopShown })),
        onVisibility: () => vi.fn(),
        setVisible,
        setLock,
      },
    } as unknown as typeof window.api;
    useKeyLimiterStatusStore.getState().reset();
    useSettingsStore.setState({
      inGameViewersEnabled: true,
      keyLimiterEnabled: true,
      obsModeEnabled: false,
      overlayLocked: false,
      alwaysOnTop: true,
      noteEffect: true,
      keyCounterEnabled: false,
      useCustomCSS: false,
      useCustomJS: false,
      jsPlugins: [
        {
          id: 'one',
          name: 'One plugin',
          path: '/one.js',
          content: '',
          enabled: true,
        },
      ],
    });
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    await open();
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.restoreAllMocks();
  });

  it('shows one short category at a time and keeps all categories available', async () => {
    expect(container.querySelectorAll('nav button')).toHaveLength(7);
    expect(container.textContent).not.toContain(
      'settingsWorkspace.keepRunningHelp',
    );
    await act(async () => button('settingsWorkspace.app').click());
    expect(container.querySelector('[data-setting-help="perKey"]')).toBeNull();
    expect(toggle('settingsWorkspace.keepRunning')).not.toBeNull();
  });

  it('opens on where the key viewer is shown: the game, the desktop, then OBS', () => {
    expect(
      container.querySelector('nav button[aria-current="page"]')?.textContent,
    ).toBe('settingsWorkspace.outputs');
    expect(
      Array.from(container.querySelectorAll('nav button'), (item) =>
        item.textContent?.replace('settingsWorkspace.', ''),
      ),
    ).toEqual([
      'outputs',
      'viewer',
      'editor',
      'sound',
      'app',
      'extensions',
      'advanced',
    ]);
    // The three places are large buttons, in this order, and one is chosen.
    expect(
      Array.from(
        container.querySelectorAll<HTMLElement>('[data-output-button]'),
        (item) => [
          item.dataset.outputButton,
          item.getAttribute('role'),
          item.getAttribute('aria-checked'),
        ],
      ),
    ).toEqual([
      ['game', 'radio', 'true'],
      ['desktop', 'radio', 'false'],
      ['obs', 'radio', 'false'],
    ]);
    // Inside the game there is nothing to set; the key limiter, which is
    // about the game whatever place is chosen, has its own group.
    expect(container.querySelectorAll('[data-output]')).toHaveLength(0);
    expect(rowLabels('limiter')).toEqual([
      'outputs.keyLimiter',
      'outputs.applied',
    ]);
    // The game connection is read on its button, with no dialog to open.
    expect(stateOf('game')).toBe('keyLimiter.connecting');
    expect(container.querySelector('[role="dialog"]')).toBeNull();
  });

  it('says what the game applied on the page, where the connection dialog used to', async () => {
    expect(group('limiter').textContent).toContain('outputs.appliedWaiting');
    const applied = {
      sessionId: 'session',
      revision: 1,
      profile: { id: 'profile', name: '4K + 2K' },
      enabled: true,
      supportedKeys: ['KeyA', 'KeyS'],
      unsupportedKeys: [] as string[],
      acknowledgedAt: 0,
    };
    act(() =>
      useKeyLimiterStatusStore.setState({
        connectionState: 'ready',
        lastAcknowledgement: applied,
      }),
    );
    // The button names the game, so the state says only "connected".
    expect(stateOf('game')).toBe('outputs.connected');
    expect(group('limiter').textContent).toContain('outputs.appliedValue');
    expect(group('limiter').textContent).not.toContain(
      'outputs.unsupportedKeys',
    );

    act(() =>
      useKeyLimiterStatusStore.setState({
        lastAcknowledgement: { ...applied, unsupportedKeys: ['F13'] },
      }),
    );
    expect(group('limiter').textContent).toContain('outputs.unsupportedKeys');
    // The rows themselves stay as they were.
    expect(rowLabels('limiter')).toEqual([
      'outputs.keyLimiter',
      'outputs.applied',
    ]);
  });

  it('uses one place at a time: choosing one turns the other two off', async () => {
    // Desktop instead of the game.
    await act(async () => place('desktop').click());
    expect(update).toHaveBeenLastCalledWith({ inGameViewersEnabled: false });
    expect(setVisible).toHaveBeenLastCalledWith(true);
    expect(obsApi.start).not.toHaveBeenCalled();
    expect(place('desktop').getAttribute('aria-checked')).toBe('true');
    expect(place('game').getAttribute('aria-checked')).toBe('false');

    // OBS instead of the desktop.
    await act(async () => place('obs').click());
    expect(obsApi.start).toHaveBeenCalledTimes(1);
    expect(setVisible).toHaveBeenLastCalledWith(false);
    expect(useSettingsStore.getState()).toMatchObject({
      obsModeEnabled: true,
      inGameViewersEnabled: false,
    });
    expect(
      ['game', 'desktop', 'obs'].map((name) =>
        place(name).getAttribute('aria-checked'),
      ),
    ).toEqual(['false', 'false', 'true']);

    // Back to the game: OBS stops, the game display starts.
    await act(async () => place('game').click());
    expect(obsApi.stop).toHaveBeenCalledTimes(1);
    expect(update).toHaveBeenLastCalledWith({ inGameViewersEnabled: true });
    // Pressing the chosen place again changes nothing.
    const calls = update.mock.calls.length;
    await act(async () => place('game').click());
    expect(update.mock.calls.length).toBe(calls);
  });

  it('shows only the settings of the chosen place, and the key limiter always', async () => {
    expect(container.querySelector('[data-output="desktop"]')).toBeNull();
    expect(container.querySelector('[data-output="obs"]')).toBeNull();
    expect(toggle('outputs.keyLimiter').disabled).toBe(false);

    await act(async () => place('desktop').click());
    expect(rowLabels('desktop')).toEqual([
      'outputs.lock',
      'outputs.onTop',
      'outputs.anchor',
    ]);
    expect(container.querySelector('[data-output="obs"]')).toBeNull();
    await act(async () => toggle('outputs.lock').click());
    expect(setLock).toHaveBeenCalledWith(true);

    await act(async () => place('obs').click());
    expect(container.querySelector('[data-output="desktop"]')).toBeNull();
    expect(rowLabels('obs')).toEqual(['outputs.address']);
    expect(button('outputs.copy').disabled).toBe(false);
    // The key limiter is about the game, whatever place is chosen.
    expect(toggle('outputs.keyLimiter').disabled).toBe(false);
  });

  it('hands out the OBS address only while OBS is the chosen place', async () => {
    const writeText = vi.fn(async (_text: string) => undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText },
      configurable: true,
    });
    // The page server already runs for the game; that alone offers nothing.
    vi.mocked(obsApi.status).mockResolvedValue({ ...serverForObs });
    act(() => root.unmount());
    root = createRoot(container);
    await open();
    expect(container.querySelector('[data-output="obs"]')).toBeNull();
    expect(stateOf('obs')).toBe('');

    await act(async () => place('obs').click());
    expect(stateOf('obs')).toBe('outputs.obsWaiting');
    await act(async () => button('outputs.copy').click());
    expect(writeText).toHaveBeenCalledWith(
      'http://192.168.0.7:34891?token=secret',
    );
    expect(showAlert).toHaveBeenCalledWith('outputs.copied');
    Reflect.deleteProperty(navigator, 'clipboard');
  });

  it('does not leave OBS chosen when the app cannot turn it on', async () => {
    vi.mocked(obsApi.start).mockRejectedValue(new Error('port in use'));
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    await act(async () => place('obs').click());
    expect(useSettingsStore.getState().obsModeEnabled).toBe(false);
    expect(place('obs').getAttribute('aria-checked')).toBe('false');
    expect(showAlert).toHaveBeenCalledWith('outputs.obsFailed');
  });

  it('opens on the section it is asked for', async () => {
    act(() => root.unmount());
    root = createRoot(container);
    await open('app');
    expect(toggle('settingsWorkspace.keepRunning')).not.toBeNull();
    expect(container.querySelector('[data-output]')).toBeNull();
  });

  it('explains that Raining and counts are switched on each key', async () => {
    await act(async () => button('settingsWorkspace.viewer').click());
    // Where the key viewer is shown, and its desktop rows, are not here.
    expect(container.querySelector('[data-output]')).toBeNull();
    expect(toggle('outputs.lock')).toBeNull();
    // Neither has a switch for every key here any more.
    expect(toggle('settingsWorkspace.trails')).toBeNull();
    expect(toggle('settingsWorkspace.counts')).toBeNull();
    expect(
      container.querySelector('[data-setting-help="perKey"]')?.textContent,
    ).toContain('settingsWorkspace.perKeyHelp');
  });

  it('leaves settings from the chevron beside the title', async () => {
    const leave = container.querySelector<HTMLButtonElement>(
      '.en-settings-title .en-settings-leave',
    )!;
    expect(leave.getAttribute('aria-label')).toBe('settingsWorkspace.leave');
    expect(leave.nextElementSibling?.tagName).toBe('H1');
    await act(async () => leave.click());
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('keeps CSS file controls unavailable until appearance files are on', async () => {
    await act(async () => button('settingsWorkspace.extensions').click());
    await act(async () => button('settings.configure').click());
    expect(button('settings.loadCss').disabled).toBe(true);
    expect(button('settingsWorkspace.useFile').matches(':disabled')).toBe(true);
    expect(container.textContent).toContain('settingsWorkspace.cssOff');
    await act(async () => button('← settingsWorkspace.extensions').click());
    expect(container.textContent).toContain('settingsWorkspace.pluginsHelp');
  });

  it('disables individual add-ons while the add-on master is off', async () => {
    await act(async () => button('settingsWorkspace.extensions').click());
    await act(async () => button('settings.managePlugins').click());
    expect(toggle('One plugin').disabled).toBe(true);
    expect(button('settings.reloadPlugins').disabled).toBe(true);
    expect(container.textContent).toContain('settingsWorkspace.pluginsOff');
  });
});
