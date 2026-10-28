import React, { useEffect, useRef, useState } from 'react';
import { useLenis } from '@hooks/useLenis';
import { useTranslation } from '@contexts/useTranslation';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import Checkbox from '@components/main/common/Checkbox';
import Dropdown from '@components/main/common/Dropdown';
import Button from '@components/main/common/Button';
import SettingsSidePanel, {
  type SettingsPanelKey,
} from '@components/main/SettingsPanel/SettingsSidePanel';
import ResetIcon from '@assets/svgs/reset.svg';
import { PluginManagerModal } from '@components/main/Modal/content/managers/PluginManagerModal';
import { PluginDataDeleteModal } from '@components/main/Modal/content/dialogs/PluginDataDeleteModal';
import ShortcutSettingsModal from '@components/main/Modal/content/settings/ShortcutSettingsModal';
import { applyCounterSnapshot } from '@stores/signals/keyCounterSignals';
import { extractPluginId } from '@utils/plugin/pluginUtils';
import { isMac } from '@utils/core/platform';
import type { OverlayResizeAnchor } from '@src/types/settings/settings';
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
import type { KeyCounters } from '@src/types/key/keys';
import { obsApi } from '@api/modules/obsApi';
import { setObsModeRunning } from '@utils/obsMode';
import { keySoundOutputApi } from '@api/modules/resourceApi';
import type {
  KeySoundOutputBackend,
  KeySoundOutputState,
  KeySoundOutputDevice,
} from '@api/modules/resourceApi';
import type { ObsStatus } from '@src/types/obs';
import { DEFAULT_OBS_PORT } from '@src/types/obs';

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
  showConfirm: (
    msg: string,
    onConfirm: () => void,
    onCancel?: () => void,
    confirmText?: string,
  ) => void;
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
}: SettingsProps): React.ReactElement => {
  const { t, i18n } = useTranslation();
  const isMacOS: boolean = isMac();
  const {
    hardwareAcceleration,
    setHardwareAcceleration,
    alwaysOnTop,
    setAlwaysOnTop,
    overlayLocked,
    setOverlayLocked,
    angleMode,
    setAngleMode,
    noteEffect,
    setNoteEffect,
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
    overlayResizeAnchor,
    setOverlayResizeAnchor,
    keyCounterEnabled,
    setKeyCounterEnabled,
    shortcuts,
    setShortcuts,
  } = useSettingsStore();

  const [isScrollHovered, setIsScrollHovered] = useState<boolean>(false);
  const [isPluginModalOpen, setPluginModalOpen] = useState<boolean>(false);
  const [isDataDeleteModalOpen, setDataDeleteModalOpen] =
    useState<boolean>(false);
  const [isShortcutModalOpen, setShortcutModalOpen] = useState<boolean>(false);
  const [activeSettingsPanel, setActiveSettingsPanel] =
    useState<SettingsPanelKey | null>(null);
  const [cssHistory, setCssHistory] = useState<CustomCssHistoryEntry[]>([]);
  const [pluginToDelete, setPluginToDelete] = useState<PluginToDelete | null>(
    null,
  );
  const [isReloadingPlugins, setIsReloadingPlugins] = useState<boolean>(false);
  const [isAddingPlugins, setIsAddingPlugins] = useState<boolean>(false);
  const [pendingPluginId, setPendingPluginId] = useState<string | null>(null);

  // OBS 모드
  const [obsStatus, setObsStatus] = useState<ObsStatus>({
    running: false,
    port: DEFAULT_OBS_PORT,
    clientCount: 0,
  });
  const obsTogglingRef = useRef(false);

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
  }, []);

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
    }
  };

  // Lenis smooth scroll 적용 (전역 설정 사용)
  const { scrollContainerRef } = useLenis();

  const RESIZE_ANCHOR_OPTIONS: { value: string; key: string }[] = [
    { value: 'top-left', key: 'topLeft' },
    { value: 'bottom-left', key: 'bottomLeft' },
    { value: 'top-right', key: 'topRight' },
    { value: 'bottom-right', key: 'bottomRight' },
    { value: 'center', key: 'center' },
    // 미완성 기능
    // { value: "fixed-position", key: "fixedPosition" },
  ];

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

  // OBS 상태 이벤트 구독 + clientCount 폴링
  useEffect(() => {
    let mounted = true;
    obsApi
      .status()
      .then((status) => {
        if (mounted) setObsStatus(status);
      })
      .catch(() => undefined);

    // start/stop 이벤트 구독
    const unsubscribe = obsApi.onStatus((status) => {
      if (mounted) setObsStatus(status);
    });

    // clientCount는 connect/disconnect 이벤트가 없으므로 폴링 유지
    const interval = setInterval(async () => {
      try {
        const status = await obsApi.status();
        if (mounted) {
          setObsStatus((prev) =>
            prev.clientCount === status.clientCount ? prev : status,
          );
        }
      } catch {}
    }, 5000);

    return () => {
      mounted = false;
      unsubscribe();
      clearInterval(interval);
    };
  }, []);

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

  const handleAlwaysOnTopChange = async (): Promise<void> => {
    const next: boolean = !alwaysOnTop;
    setAlwaysOnTop(next);
    try {
      await window.api.settings.update({ alwaysOnTop: next });
    } catch (error) {
      console.error('Failed to toggle always-on-top', error);
    }
  };

  const handleOverlayLockChange = async (): Promise<void> => {
    const next: boolean = !overlayLocked;
    setOverlayLocked(next);
    try {
      await window.api.overlay.setLock(next);
    } catch (error) {
      console.error('Failed to toggle overlay lock', error);
    }
  };

  const handleToggleCustomCSS = async (): Promise<void> => {
    const next: boolean = !useCustomCSS;
    setUseCustomCSS(next);
    try {
      await window.api.css.toggle(next);
    } catch (error) {
      console.error('Failed to toggle custom CSS', error);
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

  const actionButtonClass = (enabled: boolean): string =>
    `dmn-inline-action ${enabled ? '' : 'is-disabled'}`;

  const handleNoteEffectChange = async (): Promise<void> => {
    const next: boolean = !noteEffect;
    setNoteEffect(next);
    try {
      await window.api.settings.update({ noteEffect: next });
    } catch (error) {
      console.error('Failed to toggle note effect', error);
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
    }
  };

  const handleObsToggle = async (): Promise<void> => {
    const next = !obsStatus.running;
    setObsStatus((prev) => ({ ...prev, running: next }));
    if (obsTogglingRef.current) return;
    obsTogglingRef.current = true;
    try {
      setObsStatus(await setObsModeRunning(next));
    } catch (error) {
      console.error('Failed to toggle OBS mode', error);
      setObsStatus((prev) => ({ ...prev, running: !next }));
      showAlert?.(
        next ? t('settings.obsStartFailed') : t('settings.obsStopFailed'),
      );
    } finally {
      obsTogglingRef.current = false;
    }
  };

  const handleObsCopyUrl = async (): Promise<void> => {
    const tokenParam = obsStatus.token ? `?token=${obsStatus.token}` : '';
    const host = obsStatus.localIp || 'localhost';
    const url = `http://${host}:${obsStatus.port}${tokenParam}`;
    try {
      await navigator.clipboard.writeText(url);
      showAlert?.(t('settings.obsCopied'));
    } catch {
      showAlert?.(url);
    }
  };

  const handleObsRegenerateToken = (): void => {
    showConfirm(
      t('settings.obsTokenRegenMessage'),
      async () => {
        try {
          const status = await obsApi.regenerateToken();
          setObsStatus(status);
        } catch (error) {
          console.error('Failed to regenerate OBS token', error);
        }
      },
      undefined,
      t('settings.obsTokenRegenConfirm'),
    );
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
    }
  };

  const handleKeyCounterToggle = async (): Promise<void> => {
    const next: boolean = !keyCounterEnabled;
    setKeyCounterEnabled(next);
    try {
      await window.api.settings.update({ keyCounterEnabled: next });
    } catch (error) {
      console.error('Failed to toggle key counter', error);
    }
  };

  const _handleResetCounters = async (
    event: React.MouseEvent,
  ): Promise<void> => {
    event.stopPropagation();
    try {
      const snapshot: KeyCounters = await window.api.keys.resetCounters();
      applyCounterSnapshot(snapshot);
      showAlert?.(t('settings.counterReset'));
    } catch (error) {
      console.error('Failed to reset key counters', error);
      showAlert?.(t('settings.counterResetFailed'));
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
      showConfirm(
        t('settings.resetAllConfirm'),
        reset,
        undefined,
        t('settings.initialize'),
      );
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

  return (
    <div className="dmn-settings relative h-full w-full overflow-hidden">
      <div
        ref={scrollContainerRef}
        className={`settings-content-scroll dmn-settings-list absolute inset-y-0 left-0 flex w-[372px] flex-col gap-[12px] overflow-y-auto p-[12px] ${
          isScrollHovered ? 'show-scrollbar' : ''
        }`}
        onMouseEnter={() => setIsScrollHovered(true)}
        onMouseLeave={() => setIsScrollHovered(false)}
      >
        {/* 설정 */}
        <div className="flex min-h-full flex-col">
          <div className="flex w-[348px] flex-col gap-[12px]">
            {/* 키뷰어 설정 */}
            <div className="dmn-setting-card flex flex-col">
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleOverlayLockChange}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.overlayLock')}
                </p>
                <Checkbox
                  checked={overlayLocked}
                  onChange={handleOverlayLockChange}
                />
              </div>
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleAlwaysOnTopChange}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.alwaysOnTop')}
                </p>
                <Checkbox
                  checked={alwaysOnTop}
                  onChange={handleAlwaysOnTopChange}
                />
              </div>
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleNoteEffectChange}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.noteEffect')}
                </p>
                <Checkbox
                  checked={noteEffect}
                  onChange={handleNoteEffectChange}
                />
              </div>
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleKeyCounterToggle}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.keyCounter')}
                </p>
                <div className="flex items-center gap-[8px]">
                  {/* <button
                    onClick={handleResetCounters}
                    className="py-[4px] px-[8px] bg-[#2A2A31] border-[1px] border-[#3A3944] rounded-[7px] text-style-2 text-[#DBDEE8] hover:bg-[#34343c]"
                  >
                    {t("settings.counterResetButton")}
                  </button> */}
                  <Checkbox
                    checked={keyCounterEnabled}
                    onChange={handleKeyCounterToggle}
                  />
                </div>
              </div>
              {/*
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onMouseEnter={() => setHoveredKey("laboratory")}
                onMouseLeave={() => setHoveredKey(null)}
                onClick={handleLaboratoryToggle}
              >
                <p className="text-label text-ui-fg-primary">
                  {t("settings.laboratory")}
                </p>
                <Checkbox
                  checked={laboratoryEnabled}
                  onChange={handleLaboratoryToggle}
                />
              </div>
              */}
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleTrayToggle}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.trayEnabled')}
                </p>
                <Checkbox checked={trayEnabled} onChange={handleTrayToggle} />
              </div>
              {null}
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p className="text-label text-ui-fg-primary">
                  {t('settings.resizeAnchor')}
                </p>
                <Dropdown
                  options={RESIZE_ANCHOR_OPTIONS.map((opt) => ({
                    value: opt.value,
                    label: t(`settings.${opt.key}`),
                  }))}
                  value={overlayResizeAnchor}
                  onChange={async (val: string) => {
                    setOverlayResizeAnchor(val as OverlayResizeAnchor);
                    try {
                      await window.api.overlay.setAnchor(val);
                    } catch (error) {
                      console.error('Failed to set overlay anchor', error);
                    }
                  }}
                  placeholder={t('settings.selectAnchor')}
                  align="right"
                />
              </div>
            </div>
            {/* 커스텀 CSS & JS 설정 */}
            <div className="dmn-setting-card flex flex-col">
              <div className="flex flex-col gap-[0px]">
                <div
                  className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                  onClick={handleToggleCustomCSS}
                >
                  <p className="text-label text-ui-fg-primary">
                    {t('settings.customCSS')}
                  </p>
                  <Checkbox
                    checked={useCustomCSS}
                    onChange={handleToggleCustomCSS}
                  />
                </div>
                <div className="flex flex-row justify-between items-center h-[40px]">
                  <p
                    className={
                      'text-[12px] truncate max-w-[150px] ' +
                      (useCustomCSS
                        ? 'text-ui-fg-muted'
                        : 'text-ui-fg-disabled')
                    }
                  >
                    {customCSSPath && customCSSPath.length > 0
                      ? customCSSPath
                      : t('settings.noCssFile')}
                  </p>
                  <button
                    onClick={handleLoadCustomCSS}
                    disabled={!useCustomCSS}
                    className={
                      'py-[5px] px-[9px] rounded-ui-inner text-style-2 ' +
                      (useCustomCSS
                        ? 'bg-ui-fill hover:bg-ui-fill-hover text-[#DBDEE8]'
                        : 'bg-ui-fill-faint text-ui-fg-disabled cursor-not-allowed')
                    }
                  >
                    {t('settings.loadCss')}
                  </button>
                </div>
              </div>
              <div className="flex flex-col gap-[0px]">
                <div
                  className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                  onClick={handleToggleCustomJS}
                >
                  <p className="text-label text-ui-fg-primary">
                    {t('settings.customJS')}
                  </p>
                  <Checkbox
                    checked={useCustomJS}
                    onChange={handleToggleCustomJS}
                  />
                </div>
                <div className="flex flex-row justify-between items-center h-[40px]">
                  <p
                    className={
                      'text-[12px] truncate max-w-[150px] ' +
                      (useCustomJS ? 'text-ui-fg-muted' : 'text-ui-fg-disabled')
                    }
                  >
                    {t('settings.pluginManageLabel')}
                  </p>
                  <div className="flex flex-row gap-[6px]">
                    <button
                      onClick={handleReloadPlugins}
                      disabled={!canReloadPlugins || isReloadingPlugins}
                      className={
                        'flex items-center justify-center px-[6px] py-[5px] rounded-ui-inner transition-none ' +
                        (canReloadPlugins && !isReloadingPlugins
                          ? 'bg-ui-fill hover:bg-ui-fill-hover'
                          : 'bg-ui-fill-faint cursor-not-allowed')
                      }
                      style={
                        isReloadingPlugins
                          ? { opacity: 0.65, pointerEvents: 'none' }
                          : undefined
                      }
                      title={t('settings.reloadPlugins')}
                    >
                      <ResetIcon
                        className={
                          'w-[13px] h-[13px] -scale-x-100 ' +
                          (canReloadPlugins && !isReloadingPlugins
                            ? '[&_path]:fill-[#DBDEE8]'
                            : '[&_path]:fill-[var(--ui-fg-disabled)]')
                        }
                      />
                    </button>
                    <button
                      onClick={() => setActiveSettingsPanel('plugins')}
                      className={actionButtonClass(true)}
                    >
                      {t('settings.managePlugins')}
                    </button>
                  </div>
                </div>
                <p className="pb-[8px] text-[11px] leading-[15px] text-ui-fg-muted">
                  {t('settings.pluginSecurityWarning')}
                </p>
              </div>
            </div>
            {/* OBS 모드 */}
            <div className="dmn-setting-card flex flex-col">
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleObsToggle}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.obsMode')}
                </p>
                <Checkbox
                  checked={obsStatus.running}
                  onChange={handleObsToggle}
                />
              </div>
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p
                  className={
                    'text-[12px] ' +
                    (obsStatus.running
                      ? 'text-ui-fg-muted'
                      : 'text-ui-fg-disabled')
                  }
                >
                  {obsStatus.running
                    ? obsStatus.clientCount > 0
                      ? `${t('settings.obsRunning')} · ${t(
                          'settings.obsClients',
                          { count: obsStatus.clientCount },
                        )}`
                      : t('settings.obsRunning')
                    : t('settings.obsStopped')}
                </p>
                <div className="flex items-center gap-[6px]">
                  <button
                    onClick={handleObsRegenerateToken}
                    disabled={!obsStatus.running}
                    className={
                      'flex items-center justify-center px-[6px] py-[5px] rounded-ui-inner transition-colors ' +
                      (obsStatus.running
                        ? 'bg-ui-fill hover:bg-ui-fill-hover'
                        : 'bg-ui-fill-faint cursor-not-allowed')
                    }
                  >
                    <ResetIcon
                      className={
                        'w-[13px] h-[13px] -scale-x-100 ' +
                        (obsStatus.running
                          ? '[&_path]:fill-[#DBDEE8]'
                          : '[&_path]:fill-[var(--ui-fg-disabled)]')
                      }
                    />
                  </button>
                  <button
                    onClick={handleObsCopyUrl}
                    disabled={!obsStatus.running}
                    className={actionButtonClass(obsStatus.running)}
                  >
                    {t('settings.obsCopyUrl')}
                  </button>
                </div>
              </div>
            </div>
            {/* 키음 출력 설정 */}
            <div className="dmn-setting-card flex flex-col">
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p className="text-label text-ui-fg-primary flex-1 min-w-0 truncate pr-[10px]">
                  {t('settings.keySoundOutput') || '키 사운드 출력'}
                </p>
                <div className="shrink-0">
                  <Dropdown
                    options={[
                      {
                        value: 'defaultDevice',
                        label:
                          keySoundOutputError &&
                          keySoundOutput?.requested.kind === 'defaultDevice'
                            ? `⚠ ${keySoundOutputError}`
                            : t('settings.keySoundOutputDefault') ||
                              '기본 재생 장치',
                      },
                      ...visibleSystemDevices.map((device) => ({
                        value: `device:${device.id}`,
                        label:
                          device.name.length > 20
                            ? `${device.name.slice(0, 20)}…`
                            : device.name,
                      })),
                      ...visibleAsioDrivers.map((name) => {
                        // 선택한 ASIO가 열기 실패하면 라벨에 ⚠ + 사유 표시 (인라인 경고 대신)
                        const failed =
                          name === requestedAsioDriver && !!keySoundOutputError;
                        return {
                          value: `asio:${name}`,
                          // 드라이버 이름이 길면 …로 축약 (기본 항목 라벨은 안 잘리게 max-w 여유, ASIO만 축약)
                          label: failed
                            ? `⚠ ${keySoundOutputError}`
                            : `ASIO: ${
                                name.length > 16
                                  ? `${name.slice(0, 16)}…`
                                  : name
                              }`,
                        };
                      }),
                    ]}
                    value={
                      keySoundOutput?.requested.kind === 'asio'
                        ? `asio:${keySoundOutput.requested.driverName}`
                        : keySoundOutput?.requested.kind === 'device'
                          ? `device:${keySoundOutput.requested.id}`
                          : 'defaultDevice'
                    }
                    onChange={handleKeySoundOutputChange}
                    placeholder={
                      t('settings.keySoundOutputDefault') || '기본 재생 장치'
                    }
                    align="right"
                    widthClass="max-w-[160px]"
                    disabled={
                      asioDriversLoaded &&
                      visibleAsioDrivers.length === 0 &&
                      visibleSystemDevices.length === 0
                    }
                  />
                </div>
              </div>
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p
                  className={`text-label ${
                    keySoundOutput?.requested.kind === 'asio'
                      ? 'text-ui-fg-primary'
                      : 'text-ui-fg-disabled'
                  }`}
                >
                  {t('settings.keySoundOutputBuffer') || 'ASIO 버퍼 크기'}
                </p>
                <Dropdown
                  options={visibleAsioBuffers.map((size) => ({
                    value: String(size),
                    label: String(size),
                  }))}
                  value={String(requestedAsioBuffer)}
                  onChange={handleAsioBufferChange}
                  placeholder={String(DEFAULT_ASIO_BUFFER)}
                  align="right"
                  widthClass="w-[70px]"
                  disabled={keySoundOutput?.requested.kind !== 'asio'}
                />
              </div>
            </div>
            {/* 기타 설정 */}
            <div className="dmn-setting-card flex flex-col">
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p className="text-label text-ui-fg-primary">
                  {t('settings.language')}
                </p>
                <Dropdown
                  options={LANGUAGE_OPTIONS}
                  value={language}
                  onChange={handleLanguageChange}
                  placeholder={t('settings.selectLanguage')}
                  align="right"
                />
              </div>
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p className="text-label text-ui-fg-primary">
                  {t('settings.shortcuts')}
                </p>
                <button
                  onClick={() => setActiveSettingsPanel('shortcuts')}
                  className={actionButtonClass(true)}
                >
                  {t('settings.configure')}
                </button>
              </div>
              <div className="flex flex-row justify-between items-center h-[40px]">
                <p className="text-label text-ui-fg-primary">
                  {t('settings.graphicsOption')}
                </p>
                <Dropdown
                  options={isMacOS ? macAngleOptions : ANGLE_OPTIONS}
                  value={isMacOS ? 'metal' : angleMode}
                  onChange={handleAngleModeChangeSelect}
                  placeholder={t('settings.renderMode')}
                  disabled={isMacOS}
                  align="right"
                />
              </div>
              <div
                className="flex flex-row justify-between items-center h-[40px] cursor-pointer"
                onClick={handleDeveloperModeToggle}
              >
                <p className="text-label text-ui-fg-primary">
                  {t('settings.developerMode')}
                </p>
                <Checkbox
                  checked={developerModeEnabled}
                  onChange={handleDeveloperModeToggle}
                />
              </div>
              {/* 버전 및 설정 초기화 */}
              <div className="flex justify-between items-center py-[14px] px-[12px] bg-ui-inset rounded-ui-surface mt-[7px] mb-[12px]">
                <p className="text-label text-ui-fg-primary">
                  Ver {__APP_VERSION__}
                </p>
                <div className="flex gap-[8px]">
                  <button
                    className="bg-[var(--ui-danger-soft)] hover:bg-[rgba(239,83,80,0.24)] rounded-ui-inner py-[4px] px-[9px] text-style-2 text-[#ffb4b2]"
                    onClick={handleResetAll}
                  >
                    {t('settings.resetData')}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div className="dmn-settings-detail absolute bottom-[12px] left-[372px] right-[12px] top-[12px] overflow-hidden">
        {!activeSettingsPanel && (
          <div className="dmn-settings-preview" aria-label="Settings overview">
            <div className="dmn-settings-preview__mark" aria-hidden="true">
              DM
            </div>
            <div>
              <h2>ImplDmNote</h2>
              <p>Manage the key viewer and editing environment in one place.</p>
            </div>
            <div className="dmn-settings-preview__actions">
              <button
                type="button"
                onClick={() => setActiveSettingsPanel('css')}
              >
                CSS
              </button>
              <button
                type="button"
                onClick={() => setActiveSettingsPanel('plugins')}
              >
                Plugins
              </button>
              <button
                type="button"
                onClick={() => setActiveSettingsPanel('shortcuts')}
              >
                Shortcuts
              </button>
            </div>
          </div>
        )}
        {activeSettingsPanel === 'css' && (
          <SettingsSidePanel
            title={t('settings.customCSS') || 'Custom CSS'}
            onClose={() => setActiveSettingsPanel(null)}
          >
            <div className="dmn-settings-panel-card">
              <div className="dmn-settings-panel-row">
                <div>
                  <strong>{t('settings.customCSS')}</strong>
                  <span>{customCSSPath || t('settings.noCssFile')}</span>
                </div>
                <Checkbox
                  checked={useCustomCSS}
                  onChange={handleToggleCustomCSS}
                />
              </div>
              <Button
                variant="secondary"
                block
                disabled={!useCustomCSS}
                onClick={handleLoadCustomCSS}
              >
                {t('settings.loadCss')}
              </Button>
              {cssHistory.length > 0 && (
                <div className="flex flex-col gap-[6px] pt-[4px]">
                  <strong>{t('settings.cssHistory')}</strong>
                  {cssHistory.map((entry) => (
                    <div
                      key={entry.path}
                      className="flex items-center gap-[6px]"
                    >
                      <button
                        type="button"
                        className="min-w-0 flex-1 truncate text-left text-style-1 text-[#DBDEE8] hover:text-white"
                        title={entry.path}
                        onClick={() => void activateCssHistory(entry.path)}
                      >
                        {entry.path.split(/[/\\]/).pop()}
                      </button>
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={async () => {
                          await window.api.css.history.remove(entry.path);
                          setCssHistory(await window.api.css.history.list());
                        }}
                      >
                        {t('tabCss.remove')}
                      </Button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </SettingsSidePanel>
        )}
        {activeSettingsPanel === 'plugins' && (
          <SettingsSidePanel
            title={t('settings.managePlugins') || 'Plugins'}
            badge={<span className="dmn-panel-badge">{jsPlugins.length}</span>}
            onClose={() => setActiveSettingsPanel(null)}
          >
            <div className="dmn-settings-panel-card">
              <div className="dmn-settings-panel-row">
                <div>
                  <strong>{t('settings.customJS')}</strong>
                  <span>{t('settings.pluginSecurityWarning')}</span>
                </div>
                <Checkbox
                  checked={useCustomJS}
                  onChange={handleToggleCustomJS}
                />
              </div>
              <div className="dmn-settings-panel-list">
                {jsPlugins.length === 0 ? (
                  <p className="dmn-panel-empty">
                    {t('settings.jsReloadNoPlugins')}
                  </p>
                ) : (
                  jsPlugins.map((plugin) => (
                    <div className="dmn-settings-panel-row" key={plugin.id}>
                      <div className="min-w-0">
                        <strong className="truncate">{plugin.name}</strong>
                        <span className="truncate">
                          {plugin.path || plugin.id}
                        </span>
                      </div>
                      <Checkbox
                        checked={plugin.enabled}
                        onChange={() =>
                          void handlePluginToggle(plugin.id, !plugin.enabled)
                        }
                      />
                    </div>
                  ))
                )}
              </div>
              <div className="flex gap-[8px]">
                <Button
                  variant="secondary"
                  block
                  loading={isReloadingPlugins}
                  disabled={!canReloadPlugins}
                  onClick={() => void handleReloadPlugins()}
                >
                  {t('settings.reloadPlugins')}
                </Button>
                <Button variant="primary" block onClick={handleOpenPluginModal}>
                  {t('settings.managePlugins')}
                </Button>
              </div>
            </div>
          </SettingsSidePanel>
        )}
        {activeSettingsPanel === 'shortcuts' && (
          <SettingsSidePanel
            title={t('settings.shortcuts') || 'Shortcuts'}
            badge={
              <span className="dmn-panel-badge">
                {Object.keys(shortcuts).length}
              </span>
            }
            onClose={() => setActiveSettingsPanel(null)}
          >
            <div className="dmn-settings-panel-card">
              <p className="dmn-panel-help">
                Review and edit the shortcuts used by the current profile.
              </p>
              <Button
                variant="primary"
                block
                onClick={() => setShortcutModalOpen(true)}
              >
                {t('settings.configure')}
              </Button>
            </div>
          </SettingsSidePanel>
        )}
      </div>
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
