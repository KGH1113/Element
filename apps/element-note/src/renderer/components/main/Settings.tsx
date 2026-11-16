import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import { Input } from '@components/ui/Input';
import {
  SettingRow,
  SettingToggleRow,
} from '@components/main/common/SettingRow';
import Dropdown from '@components/main/common/Dropdown';
import Button from '@components/main/common/Button';
import {
  SETTINGS_SECTIONS,
  searchSettings,
  type SettingsSection,
} from '@components/main/SettingsPanel/settingsNavigation';
import {
  EditorGroup,
  ViewerLookGroup,
} from '@components/main/SettingsPanel/CanvasSettings';
import { OutputSettings } from '@components/main/SettingsPanel/OutputSettings';
import type { ShowConfirm } from '@components/main/Modal/content/dialogs/confirm';
import {
  RainingFrameLimitRow,
  RainingGroups,
} from '@components/main/SettingsPanel/RainingSettings';
import './SettingsPanel/settings-workspace.css';
import { PluginManagerModal } from '@components/main/Modal/content/managers/PluginManagerModal';
import { PluginDataDeleteModal } from '@components/main/Modal/content/dialogs/PluginDataDeleteModal';
import ShortcutSettingsModal from '@components/main/Modal/content/settings/ShortcutSettingsModal';
import { extractPluginId } from '@utils/plugin/pluginUtils';
import { isMac } from '@utils/core/platform';
import type { ShortcutsState } from '@src/types/settings/shortcuts';
import type { SupportedLocale } from '@contexts/I18nContextDef';
import type {
  CssLoadResult,
  JsLoadResult,
  JsReloadResult,
  JsRemoveResult,
  JsPluginUpdateResult,
  KeysResetAllResponse,
  CustomCssHistoryEntry,
} from '@src/types/plugin/api';
import type { JsPlugin } from '@src/types/plugin/js';
import { keySoundOutputApi } from '@api/modules/resourceApi';
import type {
  KeySoundOutputBackend,
  KeySoundOutputState,
  KeySoundOutputDevice,
} from '@api/modules/resourceApi';

// ASIO 버퍼 크기 선택지(프레임). 게임 설정값과 맞춰야 ASIO 공존 가능.
const ASIO_BUFFER_SIZES = [64, 128, 256, 512, 1024] as const;
// 기본 버퍼 크기 (게임 기본값과 동일한 최저값)
const DEFAULT_ASIO_BUFFER = 64;

const KEY_SOUND_OUTPUT_ERROR_KEYS: Record<string, string> = {
  asioUnavailableBuild: 'settings.keySoundOutputError.asioUnavailableBuild',
  asioDeviceNotFound: 'settings.keySoundOutputError.asioDeviceNotFound',
  asioOpenFailed: 'settings.keySoundOutputError.asioOpenFailed',
  defaultOpenFailed: 'settings.keySoundOutputError.defaultOpenFailed',
  deviceNotFound: 'settings.keySoundOutputError.deviceNotFound',
  deviceOpenFailed: 'settings.keySoundOutputError.deviceOpenFailed',
};

// 설정 패널은 열 때마다 재마운트되므로, 마지막 출력 상태를 모듈에 캐시해
// 재진입 시 '기본 장치 → ASIO' 드롭다운 깜빡임을 방지한다.
let cachedKeySoundOutput: KeySoundOutputState | null = null;
let cachedAsioDrivers: string[] = [];
let cachedSystemDevices: KeySoundOutputDevice[] = [];
let cachedAsioDriversLoaded = false;

interface SettingsProps {
  showAlert: (msg: string, confirmText?: string) => void;
  showConfirm: ShowConfirm;
  /** Leaves settings and returns to the editor. */
  onClose: () => void;
  /** The section to open on. Without it settings open on the first one. */
  initialSection?: SettingsSection;
}

interface PluginError {
  path?: string;
  error: string;
}

interface PluginToDelete {
  id: string;
  name: string;
  namespace: string;
}

const Settings = ({
  showAlert,
  showConfirm,
  onClose,
  initialSection = SETTINGS_SECTIONS[0],
}: SettingsProps): React.ReactElement => {
  const { t, i18n } = useTranslation();
  const isMacOS: boolean = isMac();
  const {
    hardwareAcceleration,
    setHardwareAcceleration,
    angleMode,
    setAngleMode,
    laboratoryEnabled,
    setLaboratoryEnabled,
    trayEnabled,
    setTrayEnabled,
    developerModeEnabled,
    setDeveloperModeEnabled,
    useCustomCSS,
    setUseCustomCSS,
    setCustomCSSContent,
    customCSSPath,
    setCustomCSSPath,
    useCustomJS,
    setUseCustomJS,
    jsPlugins,
    language,
    setLanguage,
    shortcuts,
    setShortcuts,
  } = useSettingsStore();

  const [activeSection, setActiveSection] =
    useState<SettingsSection>(initialSection);
  const [searchQuery, setSearchQuery] = useState('');
  const pageRef = useRef<HTMLElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const [isPluginModalOpen, setPluginModalOpen] = useState<boolean>(false);
  const [isDataDeleteModalOpen, setDataDeleteModalOpen] =
    useState<boolean>(false);
  const [isShortcutModalOpen, setShortcutModalOpen] = useState<boolean>(false);
  const [activeSettingsPanel, setActiveSettingsPanel] = useState<
    'plugins' | 'css' | null
  >(null);
  const [cssHistory, setCssHistory] = useState<CustomCssHistoryEntry[]>([]);
  const [pluginToDelete, setPluginToDelete] = useState<PluginToDelete | null>(
    null,
  );
  const [isReloadingPlugins, setIsReloadingPlugins] = useState<boolean>(false);
  const [isAddingPlugins, setIsAddingPlugins] = useState<boolean>(false);
  const [pendingPluginId, setPendingPluginId] = useState<string | null>(null);

  // 키음 출력 백엔드 (기본 장치 / ASIO) — 캐시로 초기화해 재진입 깜빡임 방지
  const [keySoundOutput, setKeySoundOutputRaw] =
    useState<KeySoundOutputState | null>(cachedKeySoundOutput);
  const [asioDrivers, setAsioDrivers] = useState<string[]>(cachedAsioDrivers);
  const [systemDevices, setSystemDevices] =
    useState<KeySoundOutputDevice[]>(cachedSystemDevices);
  // 목록 로딩 완료 전에는 드롭다운을 잠그지 않음 (첫 마운트 비활성 깜빡임 방지)
  const [asioDriversLoaded, setAsioDriversLoaded] = useState(
    cachedAsioDriversLoaded,
  );

  const setKeySoundOutput = (state: KeySoundOutputState) => {
    cachedKeySoundOutput = state;
    setKeySoundOutputRaw(state);
  };

  useEffect(() => {
    if (activeSection !== 'sound') return;
    let cancelled = false;
    (async () => {
      try {
        const [devices, state] = await Promise.all([
          keySoundOutputApi.listDevices(),
          keySoundOutputApi.getState(),
        ]);
        if (cancelled) return;
        cachedAsioDrivers = devices.asio;
        cachedSystemDevices = devices.system ?? [];
        cachedAsioDriversLoaded = true;
        setAsioDrivers(devices.asio);
        setSystemDevices(devices.system ?? []);
        setAsioDriversLoaded(true);
        setKeySoundOutput(state);
      } catch (error) {
        console.error('Failed to load key sound output state', error);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [activeSection]);

  const handleKeySoundOutputChange = async (val: string) => {
    const backend: KeySoundOutputBackend = val.startsWith('asio:')
      ? {
          kind: 'asio',
          driverName: val.slice('asio:'.length),
          // ASIO 선택 시 기본 버퍼 64 (게임과 동일하게 맞춰야 공존 가능)
          bufferSize: DEFAULT_ASIO_BUFFER,
        }
      : val.startsWith('device:')
        ? (() => {
            const id = val.slice('device:'.length);
            const device = systemDevices.find(
              (candidate) => candidate.id === id,
            );
            return { kind: 'device' as const, id, name: device?.name ?? id };
          })()
        : { kind: 'defaultDevice' };
    try {
      const next = await keySoundOutputApi.setBackend(backend);
      setKeySoundOutput(next);
    } catch (error) {
      console.error('Failed to set key sound output backend', error);
      showAlert(t('settingsWorkspace.soundErrorHelp'));
    }
  };

  // ASIO 버퍼 크기 변경 (게임과 동일 버퍼로 맞춰야 ASIO 공존 가능)
  const handleAsioBufferChange = async (val: string) => {
    const requested = keySoundOutput?.requested;
    if (requested?.kind !== 'asio') return;
    try {
      const next = await keySoundOutputApi.setBackend({
        kind: 'asio',
        driverName: requested.driverName,
        bufferSize: Number(val),
      });
      setKeySoundOutput(next);
    } catch (error) {
      console.error('Failed to set ASIO buffer size', error);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const ANGLE_OPTIONS: { value: string; label: string }[] = [
    { value: 'skia', label: 'Skia' },
    { value: 'd3d11', label: 'Direct3D 11' },
    { value: 'd3d9', label: 'Direct3D 9' },
    { value: 'gl', label: 'OpenGL' },
  ];

  const macAngleOptions: { value: string; label: string }[] = [
    { value: 'metal', label: 'Metal' },
  ];

  useEffect(() => {
    if (isMacOS && angleMode !== 'metal') {
      setAngleMode('metal');
    }
  }, [isMacOS, angleMode, setAngleMode]);

  const LANGUAGE_OPTIONS: { value: string; label: string }[] = [
    { value: 'ko', label: '한국어' },
    { value: 'en', label: 'English' },
    { value: 'zh-cn', label: '简体中文' },
    { value: 'zh-Hant', label: '繁體中文' },
    { value: 'ru', label: 'Русский' },
  ];

  const _handleHardwareAccelerationChange = (): void => {
    const next: boolean = !hardwareAcceleration;

    const apply = async (): Promise<void> => {
      setHardwareAcceleration(next);
      try {
        await window.api.settings.update({ hardwareAcceleration: next });
        await window.api.app.restart();
      } catch (error) {
        console.error('Failed to toggle hardware acceleration', error);
      }
    };

    if (showConfirm) {
      showConfirm(t('settings.restartConfirm'), apply);
    } else {
      apply();
    }
  };

  const handleToggleCustomCSS = async (): Promise<void> => {
    const next: boolean = !useCustomCSS;
    setUseCustomCSS(next);
    try {
      await window.api.css.toggle(next);
    } catch (error) {
      console.error('Failed to toggle custom CSS', error);
      setUseCustomCSS(!next);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const handleLoadCustomCSS = async (): Promise<void> => {
    if (!useCustomCSS) return;
    try {
      const result: CssLoadResult = await window.api.css.load();
      if (result.success) {
        if (result.content) setCustomCSSContent(result.content);
        if (result.path) setCustomCSSPath(result.path);
        setCssHistory(await window.api.css.history.list());
        showAlert?.(t('settings.cssLoaded'));
      } else {
        const message: string = result.error
          ? `${t('settings.cssLoadFailed')}${result.error}`
          : t('settings.cssLoadFailed');
        showAlert?.(message);
      }
    } catch (error) {
      console.error('Failed to load custom CSS', error);
      showAlert?.(`${t('settings.cssLoadFailed')}${error}`);
    }
  };

  useEffect(() => {
    if (activeSettingsPanel !== 'css') return;
    window.api.css.history.list().then(setCssHistory).catch(console.error);
  }, [activeSettingsPanel]);

  const activateCssHistory = async (path: string) => {
    const result = await window.api.css.history.activate(path);
    if (!result.success || !result.css) {
      showAlert?.(result.error ?? t('settings.cssLoadFailed'));
      return;
    }
    setCustomCSSContent(result.css.content);
    setCustomCSSPath(result.css.path ?? null);
    setCssHistory(await window.api.css.history.list());
  };

  const handleToggleCustomJS = async (): Promise<void> => {
    const next: boolean = !useCustomJS;
    setUseCustomJS(next);
    try {
      await window.api.js.toggle(next);
    } catch (error) {
      console.error('Failed to toggle custom JS', error);
      setUseCustomJS(!next);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const formatPluginErrors = (errors: PluginError[] = []): string =>
    errors.map((item) => `${item.path ?? 'unknown'}: ${item.error}`).join('\n');

  const canReloadPlugins: boolean = jsPlugins.some(
    (plugin: JsPlugin) => plugin.path,
  );

  const handleReloadPlugins = async (): Promise<void> => {
    if (isReloadingPlugins) return;
    if (jsPlugins.length === 0) {
      showAlert?.(t('settings.jsReloadNoPlugins'));
      return;
    }
    const startTime: number = performance.now();
    setIsReloadingPlugins(true);
    try {
      const result: JsReloadResult = await window.api.js.reload();
      const updated: JsPlugin[] = result.updated ?? [];
      const errors: PluginError[] = result.errors ?? [];

      if (errors.length && updated.length) {
        showAlert?.(
          `${t('settings.jsReloadPartial', {
            count: updated.length,
          })}\n${formatPluginErrors(errors)}`,
        );
      } else if (errors.length) {
        showAlert?.(
          `${t('settings.jsReloadFailed')}\n${formatPluginErrors(errors)}`,
        );
      } else if (updated.length) {
        showAlert?.(t('settings.jsReloadSuccess', { count: updated.length }));
      } else {
        showAlert?.(t('settings.jsReloadNoChanges'));
      }
    } catch (error) {
      console.error('Failed to reload JS plugins', error);
      showAlert?.(`${t('settings.jsReloadFailed')}${error}`);
    } finally {
      const elapsed: number = performance.now() - startTime;
      const MIN_SPINNER_MS = 250;
      if (elapsed < MIN_SPINNER_MS) {
        setTimeout(
          () => setIsReloadingPlugins(false),
          MIN_SPINNER_MS - elapsed,
        );
      } else {
        setIsReloadingPlugins(false);
      }
    }
  };

  const handleOpenPluginModal = (): void => {
    setPluginModalOpen(true);
  };

  const handleClosePluginModal = (): void => {
    setPluginModalOpen(false);
  };

  const handleAddPlugins = async (): Promise<void> => {
    if (isAddingPlugins) return;
    setIsAddingPlugins(true);
    try {
      const result: JsLoadResult = await window.api.js.load();
      if (!result) return;
      const added: JsPlugin[] = result.added ?? [];
      const errors: PluginError[] = result.errors ?? [];

      if (errors.length && added.length) {
        showAlert?.(
          `${t('settings.jsAddPartial', {
            count: added.length,
          })}\n${formatPluginErrors(errors)}`,
        );
      } else if (errors.length) {
        showAlert?.(
          `${t('settings.jsAddFailed')}\n${formatPluginErrors(errors)}`,
        );
      } else if (added.length) {
        showAlert?.(t('settings.jsAddSuccess', { count: added.length }));
      }
    } catch (error) {
      console.error('Failed to add JS plugins', error);
      showAlert?.(`${t('settings.jsAddFailed')}${error}`);
    } finally {
      setIsAddingPlugins(false);
    }
  };

  const handlePluginToggle = async (
    pluginId: string,
    nextState: boolean,
  ): Promise<void> => {
    if (pendingPluginId) return;
    setPendingPluginId(pluginId);
    try {
      const result: JsPluginUpdateResult = await window.api.js.setPluginEnabled(
        pluginId,
        nextState,
      );
      if (!result.success) {
        showAlert?.(t('settings.jsPluginToggleFailed'));
      }
    } catch (error) {
      console.error('Failed to toggle JS plugin', error);
      showAlert?.(t('settings.jsPluginToggleFailed'));
    } finally {
      setPendingPluginId(null);
    }
  };

  const handlePluginRemove = async (pluginId: string): Promise<void> => {
    if (pendingPluginId) return;

    const plugin: JsPlugin | undefined = jsPlugins.find(
      (p: JsPlugin) => p.id === pluginId,
    );
    if (!plugin) return;

    try {
      // 실제 플러그인 네임스페이스 추출 (@id 또는 파일명 기반)
      const pluginNamespace: string = extractPluginId(
        plugin.content,
        plugin.name,
      );

      // 네임스페이스를 prefix로 사용하는 데이터가 있는지 확인
      // 백엔드에서 자동으로 "plugin_data_" 를 붙이므로 순수 네임스페이스만 전달
      const hasData: boolean =
        await window.api.plugin.storage.hasData(pluginNamespace);
      console.warn(
        '[PluginRemove] namespace=',
        pluginNamespace,
        'hasData=',
        hasData,
      );

      if (hasData) {
        setPluginToDelete({
          id: pluginId,
          name: plugin.name,
          namespace: pluginNamespace,
        });
        setDataDeleteModalOpen(true);
      } else {
        await removePluginOnly(pluginId);
      }
    } catch (error) {
      console.error('Failed to check plugin data', error);
      showAlert?.(t('settings.jsPluginRemoveFailed'));
    }
  };

  const removePluginOnly = async (pluginId: string): Promise<void> => {
    setPendingPluginId(pluginId);
    try {
      const result: JsRemoveResult = await window.api.js.remove(pluginId);
      if (!result.success) {
        showAlert?.(t('settings.jsPluginRemoveFailed'));
      }
    } catch (error) {
      console.error('Failed to remove JS plugin', error);
      showAlert?.(t('settings.jsPluginRemoveFailed'));
    } finally {
      setPendingPluginId(null);
      setDataDeleteModalOpen(false);
      setPluginToDelete(null);
    }
  };

  const removePluginWithData = async (pluginId: string): Promise<void> => {
    setPendingPluginId(pluginId);
    try {
      const plugin: JsPlugin | undefined = jsPlugins.find(
        (p: JsPlugin) => p.id === pluginId,
      );
      if (!plugin) {
        throw new Error('Plugin not found');
      }

      // 실제 네임스페이스를 다시 추출
      const pluginNamespace: string = extractPluginId(
        plugin.content,
        plugin.name,
      );

      // 1) 먼저 플러그인 제거 → 클린업이 실행되며 일부 플러그인은 저장을 시도할 수 있음
      const result: JsRemoveResult = await window.api.js.remove(pluginId);
      if (!result.success) {
        showAlert?.(t('settings.jsPluginRemoveFailed'));
      }

      // 2) 그 다음 스토리지 정리 → 클린업 중 재생성된 값까지 함께 제거
      await window.api.plugin.storage.clearByPrefix(pluginNamespace);
    } catch (error) {
      console.error('Failed to remove JS plugin with data', error);
      showAlert?.(t('settings.jsPluginRemoveFailed'));
    } finally {
      setPendingPluginId(null);
      setDataDeleteModalOpen(false);
      setPluginToDelete(null);
    }
  };

  const handleSaveShortcuts = async (next: ShortcutsState): Promise<void> => {
    setShortcuts(next);
    try {
      await window.api.settings.update({ shortcuts: next });
    } catch (error) {
      console.error('Failed to update shortcuts', error);
      showAlert?.(t('shortcutSetting.saveFailed'));
    }
  };

  const handleAngleModeChangeSelect = (val: string): void => {
    if (isMacOS) return;
    const apply = async (): Promise<void> => {
      setAngleMode(val);
      try {
        await window.api.settings.update({ angleMode: val });
        await window.api.app.restart();
      } catch (error) {
        console.error('Failed to change angle mode', error);
      }
    };

    if (showConfirm) {
      showConfirm(t('settings.restartConfirm'), apply);
    } else {
      apply();
    }
  };

  const _handleLaboratoryToggle = async (): Promise<void> => {
    const next: boolean = !laboratoryEnabled;
    setLaboratoryEnabled(next);
    try {
      await window.api.settings.update({ laboratoryEnabled: next });
    } catch (error) {
      console.error('Failed to toggle laboratory mode', error);
    }
  };

  const handleTrayToggle = async (): Promise<void> => {
    const next: boolean = !trayEnabled;
    setTrayEnabled(next);
    try {
      await window.api.settings.update({ trayEnabled: next });
    } catch (error) {
      console.error('Failed to toggle tray mode', error);
      setTrayEnabled(!next);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const handleDeveloperModeToggle = async (): Promise<void> => {
    const next: boolean = !developerModeEnabled;
    setDeveloperModeEnabled(next);
    try {
      await window.api.settings.update({ developerModeEnabled: next });
      // 개발자 모드가 활성화되면 즉시 DevTools 오픈 (메인 & 오버레이)
      if (next) {
        try {
          await window.api.window.openDevtoolsAll?.();
        } catch {}
      }
    } catch (error) {
      console.error('Failed to toggle developer mode', error);
      setDeveloperModeEnabled(!next);
      showAlert(t('settingsWorkspace.changeFailed'));
    }
  };

  const handleResetAll = (): void => {
    const reset = async (): Promise<void> => {
      try {
        const result: KeysResetAllResponse = await window.api.keys.resetAll();
        if (result) {
          // 리셋 직후 메모리 상태도 바로 초기값으로 변경
          useKeyStore.setState({
            keyMappings: result.keys,
            positions: result.positions,
            tabs: result.tabs,
            selectedViewerTabs: result.selectedViewerTabs,
            selectedKeyType: result.selectedKeyType,
          });
        }
      } catch (error) {
        console.error('Failed to reset presets', error);
      }
    };

    if (showConfirm) {
      showConfirm(t('settings.resetAllConfirm'), reset, {
        confirmText: t('settings.initialize'),
        danger: true,
      });
    } else {
      reset();
    }
  };

  const handleLanguageChange = (val: string): void => {
    setLanguage(val);
    i18n.changeLanguage(val as SupportedLocale);
  };

  const requestedAsioDriver =
    keySoundOutput?.requested.kind === 'asio'
      ? keySoundOutput.requested.driverName
      : null;
  const requestedSystemDevice =
    keySoundOutput?.requested.kind === 'device'
      ? keySoundOutput.requested
      : null;
  const visibleSystemDevices =
    requestedSystemDevice &&
    !systemDevices.some((device) => device.id === requestedSystemDevice.id)
      ? [...systemDevices, requestedSystemDevice]
      : systemDevices;
  const visibleAsioDrivers =
    requestedAsioDriver && !asioDrivers.includes(requestedAsioDriver)
      ? [...asioDrivers, requestedAsioDriver]
      : asioDrivers;
  const requestedAsioBuffer =
    keySoundOutput?.requested.kind === 'asio'
      ? keySoundOutput.requested.bufferSize || DEFAULT_ASIO_BUFFER
      : DEFAULT_ASIO_BUFFER;
  const visibleAsioBuffers = ASIO_BUFFER_SIZES.some(
    (size) => size === requestedAsioBuffer,
  )
    ? ASIO_BUFFER_SIZES
    : [...ASIO_BUFFER_SIZES, requestedAsioBuffer].sort((a, b) => a - b);
  const keySoundOutputErrorKey =
    keySoundOutput?.errorCode &&
    KEY_SOUND_OUTPUT_ERROR_KEYS[keySoundOutput.errorCode];
  const keySoundOutputError = keySoundOutputErrorKey
    ? t(keySoundOutputErrorKey)
    : keySoundOutput?.error;

  const sectionTitle = t(`settingsWorkspace.${activeSection}`);
  const sectionHelp = t(`settingsWorkspace.${activeSection}Help`);
  const searchResults = searchSettings(searchQuery, t);
  const isSearching = searchQuery.trim().length > 0;
  const navigate = (section: SettingsSection) => {
    setActiveSection(section);
    setActiveSettingsPanel(null);
    setSearchQuery('');
    pageRef.current?.scrollTo({ top: 0 });
    headingRef.current?.focus({ preventScroll: true });
  };

  return (
    <div className="en-settings-workspace">
      <aside className="en-settings-nav">
        <div className="en-settings-title">
          <button
            type="button"
            className="en-settings-leave"
            aria-label={t('settingsWorkspace.leave')}
            title={t('settingsWorkspace.leave')}
            onClick={onClose}
          >
            <svg
              width="16"
              height="16"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
            >
              <path
                d="M10 3.5 5.5 8l4.5 4.5"
                stroke="currentColor"
                strokeWidth="1.7"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
          <h1>{t('settingsWorkspace.title')}</h1>
        </div>
        <Input
          type="search"
          value={searchQuery}
          aria-label={t('settingsWorkspace.search')}
          placeholder={t('settingsWorkspace.search')}
          onChange={(event) => {
            setSearchQuery(event.target.value);
            setActiveSettingsPanel(null);
            pageRef.current?.scrollTo({ top: 0 });
          }}
          onKeyDown={(event) => {
            if (event.key === 'Escape') setSearchQuery('');
          }}
        />
        <nav aria-label={t('settingsWorkspace.categories')}>
          {SETTINGS_SECTIONS.map((section) => (
            <button
              key={section}
              type="button"
              aria-current={
                !isSearching && activeSection === section ? 'page' : undefined
              }
              onClick={() => navigate(section)}
            >
              {t(`settingsWorkspace.${section}`)}
            </button>
          ))}
        </nav>
        <div className="en-settings-about">
          <button
            type="button"
            onClick={() =>
              window.api.app.openExternal('https://github.com/KGH1113/Element')
            }
          >
            {t('tooltip.github')}
          </button>
          <button
            type="button"
            onClick={() =>
              window.api.app.openExternal(
                'https://github.com/KGH1113/Element/issues',
              )
            }
          >
            {t('tooltip.issue')}
          </button>
          <p className="en-settings-version">ElementNote {__APP_VERSION__}</p>
        </div>
      </aside>
      <section
        className="en-settings-page"
        ref={pageRef}
        aria-label={sectionTitle}
      >
        <div className="en-settings-page-inner">
          {activeSettingsPanel && (
            <Button
              className="en-settings-back"
              variant="ghost"
              onClick={() => setActiveSettingsPanel(null)}
            >
              ← {sectionTitle}
            </Button>
          )}
          <header>
            <h2 ref={headingRef} tabIndex={-1}>
              {isSearching
                ? t('settingsWorkspace.results')
                : activeSettingsPanel === 'css'
                  ? t('settingsWorkspace.css')
                  : activeSettingsPanel === 'plugins'
                    ? t('settingsWorkspace.plugins')
                    : sectionTitle}
            </h2>
            <p className="en-settings-help">
              {isSearching
                ? t('settingsWorkspace.resultsHelp')
                : activeSettingsPanel === 'css'
                  ? t('settingsWorkspace.cssHelp')
                  : activeSettingsPanel === 'plugins'
                    ? t('settingsWorkspace.pluginsHelp')
                    : sectionHelp}
            </p>
          </header>
          {isSearching ? (
            <div className="en-settings-results">
              <p className="sr-only" role="status">
                {t('settingsWorkspace.resultsCount', {
                  count: searchResults.length,
                })}
              </p>
              {searchResults.length === 0 ? (
                <p className="en-settings-status">
                  {t('settingsWorkspace.noResults')}
                </p>
              ) : (
                searchResults.map((item) => (
                  <button
                    className="en-settings-result"
                    type="button"
                    key={item.label}
                    onClick={() => navigate(item.section)}
                  >
                    <span>{t(`settingsWorkspace.${item.section}`)}</span>
                    <strong>{t(item.label)}</strong>
                    <span>{t(item.hint)}</span>
                  </button>
                ))
              )}
            </div>
          ) : activeSettingsPanel === 'css' ? (
            <section className="en-settings-group">
              <SettingToggleRow
                label={t('settingsWorkspace.cssSwitch')}
                description={t('settingsWorkspace.cssHelp')}
                checked={useCustomCSS}
                onToggle={handleToggleCustomCSS}
              />
              <p className="en-settings-status">
                {customCSSPath || t('settings.noCssFile')}
              </p>
              {!useCustomCSS && (
                <p className="en-settings-help">
                  {t('settingsWorkspace.cssOff')}
                </p>
              )}
              <div className="en-settings-actions">
                <Button
                  variant="primary"
                  disabled={!useCustomCSS}
                  onClick={handleLoadCustomCSS}
                >
                  {t('settings.loadCss')}
                </Button>
              </div>
              {cssHistory.length > 0 && (
                <fieldset
                  disabled={!useCustomCSS}
                  className={!useCustomCSS ? 'en-settings-disabled' : ''}
                >
                  <legend className="en-settings-help">
                    {t('settings.cssHistory')}
                  </legend>
                  {cssHistory.map((entry) => (
                    <SettingRow
                      key={entry.path}
                      label={entry.path.split(/[/\\]/).pop()}
                      description={entry.path}
                    >
                      <div className="en-settings-actions">
                        <Button
                          size="sm"
                          onClick={() => void activateCssHistory(entry.path)}
                        >
                          {t('settingsWorkspace.useFile')}
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={async () => {
                            await window.api.css.history.remove(entry.path);
                            setCssHistory(await window.api.css.history.list());
                          }}
                        >
                          {t('tabCss.remove')}
                        </Button>
                      </div>
                    </SettingRow>
                  ))}
                </fieldset>
              )}
            </section>
          ) : activeSettingsPanel === 'plugins' ? (
            <section className="en-settings-group">
              <SettingToggleRow
                label={t('settingsWorkspace.pluginsSwitch')}
                description={t('settingsWorkspace.pluginsHelp')}
                checked={useCustomJS}
                onToggle={handleToggleCustomJS}
              />
              {!useCustomJS && (
                <p className="en-settings-status">
                  {t('settingsWorkspace.pluginsOff')}
                </p>
              )}
              {jsPlugins.length === 0 ? (
                <p className="en-settings-help">
                  {t('settings.jsReloadNoPlugins')}
                </p>
              ) : (
                jsPlugins.map((plugin) => (
                  <SettingToggleRow
                    key={plugin.id}
                    label={plugin.name}
                    description={plugin.path || plugin.id}
                    checked={plugin.enabled}
                    disabled={!useCustomJS || pendingPluginId !== null}
                    onToggle={() =>
                      void handlePluginToggle(plugin.id, !plugin.enabled)
                    }
                  />
                ))
              )}
              <div className="en-settings-actions">
                <Button
                  disabled={!useCustomJS || !canReloadPlugins}
                  loading={isReloadingPlugins}
                  onClick={() => void handleReloadPlugins()}
                >
                  {t('settings.reloadPlugins')}
                </Button>
                <Button variant="primary" onClick={handleOpenPluginModal}>
                  {t('settings.managePlugins')}
                </Button>
              </div>
              <p className="en-settings-help">
                {t('settings.pluginSecurityWarning')}
              </p>
            </section>
          ) : (
            <>
              {activeSection === 'outputs' && (
                <OutputSettings
                  showAlert={showAlert}
                  showConfirm={showConfirm}
                />
              )}
              {activeSection === 'viewer' && (
                <>
                  <ViewerLookGroup />
                  <RainingGroups />
                  <section className="en-settings-group">
                    <div className="py-3" data-setting-help="perKey">
                      <strong className="text-label text-ui-fg-primary">
                        {t('settingsWorkspace.perKey')}
                      </strong>
                      <p className="en-settings-help">
                        {t('settingsWorkspace.perKeyHelp')}
                      </p>
                    </div>
                  </section>
                </>
              )}
              {activeSection === 'editor' && <EditorGroup />}
              {activeSection === 'sound' && (
                <section className="en-settings-group">
                  <SettingRow
                    label={t('settings.keySoundOutput')}
                    description={t('settingsWorkspace.soundDeviceHelp')}
                  >
                    <Dropdown
                      options={[
                        {
                          value: 'defaultDevice',
                          label: t('settings.keySoundOutputDefault'),
                        },
                        ...visibleSystemDevices.map((device) => ({
                          value: `device:${device.id}`,
                          label: device.name,
                        })),
                        ...visibleAsioDrivers.map((name) => ({
                          value: `asio:${name}`,
                          label: `ASIO: ${name}`,
                        })),
                      ]}
                      value={
                        keySoundOutput?.requested.kind === 'asio'
                          ? `asio:${keySoundOutput.requested.driverName}`
                          : keySoundOutput?.requested.kind === 'device'
                            ? `device:${keySoundOutput.requested.id}`
                            : 'defaultDevice'
                      }
                      onChange={handleKeySoundOutputChange}
                      placeholder={t('settings.keySoundOutputDefault')}
                      align="right"
                      widthClass="max-w-[220px]"
                      disabled={
                        asioDriversLoaded &&
                        visibleAsioDrivers.length === 0 &&
                        visibleSystemDevices.length === 0
                      }
                    />
                  </SettingRow>
                  {keySoundOutputError && (
                    <div
                      className="en-settings-status en-settings-warning"
                      role="status"
                    >
                      <p>{keySoundOutputError}</p>
                      <p>{t('settingsWorkspace.soundErrorHelp')}</p>
                    </div>
                  )}
                  {keySoundOutput?.requested.kind === 'asio' && (
                    <SettingRow
                      label={t('settings.keySoundOutputBuffer')}
                      description={t('settingsWorkspace.bufferHelp')}
                    >
                      <Dropdown
                        options={visibleAsioBuffers.map((size) => ({
                          value: String(size),
                          label: String(size),
                        }))}
                        value={String(requestedAsioBuffer)}
                        onChange={handleAsioBufferChange}
                        align="right"
                        widthClass="w-[80px]"
                      />
                    </SettingRow>
                  )}
                </section>
              )}
              {activeSection === 'app' && (
                <section className="en-settings-group">
                  <SettingRow label={t('settings.language')}>
                    <Dropdown
                      options={LANGUAGE_OPTIONS}
                      value={language}
                      onChange={handleLanguageChange}
                      placeholder={t('settings.selectLanguage')}
                      align="right"
                    />
                  </SettingRow>
                  <SettingToggleRow
                    label={t('settingsWorkspace.keepRunning')}
                    description={t('settingsWorkspace.keepRunningHelp')}
                    checked={trayEnabled}
                    onToggle={handleTrayToggle}
                  />
                  <SettingRow
                    label={t('settings.shortcuts')}
                    description={t('settingsWorkspace.shortcutsHelp')}
                  >
                    <Button onClick={() => setShortcutModalOpen(true)}>
                      {t('settings.configure')}
                    </Button>
                  </SettingRow>
                </section>
              )}
              {activeSection === 'extensions' && (
                <section className="en-settings-group">
                  <SettingRow
                    label={t('settingsWorkspace.css')}
                    description={t('settingsWorkspace.cssHelp')}
                  >
                    <Button onClick={() => setActiveSettingsPanel('css')}>
                      {t('settings.configure')}
                    </Button>
                  </SettingRow>
                  <SettingRow
                    label={t('settingsWorkspace.plugins')}
                    description={t('settingsWorkspace.pluginsHelp')}
                  >
                    <Button onClick={() => setActiveSettingsPanel('plugins')}>
                      {t('settings.managePlugins')}
                    </Button>
                  </SettingRow>
                </section>
              )}
              {activeSection === 'advanced' && (
                <>
                  <section className="en-settings-group">
                    <SettingRow
                      label={t('settings.graphicsOption')}
                      description={t('settingsWorkspace.graphicsHelp')}
                    >
                      <Dropdown
                        options={isMacOS ? macAngleOptions : ANGLE_OPTIONS}
                        value={isMacOS ? 'metal' : angleMode}
                        onChange={handleAngleModeChangeSelect}
                        placeholder={t('settings.renderMode')}
                        disabled={isMacOS}
                        align="right"
                      />
                    </SettingRow>
                    <RainingFrameLimitRow />
                    <SettingToggleRow
                      label={t('settings.developerMode')}
                      description={t('settingsWorkspace.developerHelp')}
                      checked={developerModeEnabled}
                      onToggle={handleDeveloperModeToggle}
                    />
                  </section>
                  <section className="en-settings-group">
                    <SettingRow
                      label={t('settings.resetData')}
                      description={t('settingsWorkspace.resetHelp')}
                    >
                      <Button variant="danger" onClick={handleResetAll}>
                        {t('settings.initialize')}
                      </Button>
                    </SettingRow>
                  </section>
                </>
              )}
            </>
          )}
        </div>
      </section>
      {isPluginModalOpen && (
        <PluginManagerModal
          isOpen={isPluginModalOpen}
          onClose={handleClosePluginModal}
          onAdd={handleAddPlugins}
          onToggle={handlePluginToggle}
          onRemove={handlePluginRemove}
          plugins={jsPlugins}
          isAdding={isAddingPlugins}
          pendingPluginAction={
            pendingPluginId ? { id: pendingPluginId, op: 'toggle' } : null
          }
          t={t}
        />
      )}
      {isDataDeleteModalOpen && pluginToDelete && (
        <PluginDataDeleteModal
          isOpen={isDataDeleteModalOpen}
          onClose={() => {
            setDataDeleteModalOpen(false);
            setPluginToDelete(null);
          }}
          onDeleteWithData={() => removePluginWithData(pluginToDelete.id)}
          onDeletePluginOnly={() => removePluginOnly(pluginToDelete.id)}
          pluginName={pluginToDelete.name}
          t={t}
        />
      )}
      {isShortcutModalOpen && (
        <ShortcutSettingsModal
          isOpen={isShortcutModalOpen}
          shortcuts={shortcuts}
          onClose={() => setShortcutModalOpen(false)}
          onSave={handleSaveShortcuts}
        />
      )}
    </div>
  );
};

export default Settings;
