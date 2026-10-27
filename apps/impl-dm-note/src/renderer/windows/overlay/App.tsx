import React, { useEffect, useMemo, useState, useRef } from 'react';
import {
  currentMonitor,
  getCurrentWindow,
  Window as TauriWindow,
} from '@tauri-apps/api/window';
import { LogicalPosition, PhysicalPosition } from '@tauri-apps/api/dpi';
import { Menu } from '@tauri-apps/api/menu';
import { useTranslation } from '@contexts/useTranslation';
import { DEFAULT_NOTE_SETTINGS } from '@constants/overlayDefaults';
import { mergeNoteSettings } from '@src/types/settings/noteSettings';
import { useCustomCssInjection } from '@hooks/app/useCustomCssInjection';
import { useCustomJsInjection } from '@hooks/app/useCustomJsInjection';
import { useBlockBrowserShortcuts } from '@hooks/app/useBlockBrowserShortcuts';
import { useNoteSystem } from '@hooks/overlay/useNoteSystem';
import { useAppBootstrap } from '@hooks/app/useAppBootstrap';
import { useBuiltinStatsSubscription } from '@hooks/overlay/useBuiltinStatsSubscription';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import {
  setKeyActive as setKeyActiveSignal,
  resetAllKeySignals,
} from '@stores/signals/keySignals';
import { useSettingsStore } from '@stores/useSettingsStore';
import type { KeyPosition, KeyViewerKind } from '@src/types/key/keys';
import type { StatItemPosition } from '@src/types/key/statItems';
import type { GraphItemPosition } from '@src/types/key/graphItems';
import type { KnobItemPosition } from '@src/types/key/knobs';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import OverlayScene from '@components/shared/OverlayScene';
import { computeLayout } from '@hooks/shared/useLayoutComputation';
import { publishViewerLayout } from '@api/viewerLayout';
import { isKeyMappedToViewer } from './keyStateRouting';
import { expandGhostKeys } from '@src/renderer/editor/model/ghostKeys';
import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';

type KeyDelayTimerEntry = { timers: Set<ReturnType<typeof setTimeout>> };

// 입력 시각 보정용 age 상한(ms). 백엔드 stall/클럭 이상으로 비정상적으로 큰
// 값이 와도 노트가 화면 위로 튀지 않도록 제한
const MAX_EVENT_AGE_MS = 250;
const NO_KEYS: string[] = [];
const NO_POSITIONS: KeyPosition[] = [];
const keyLabel = (key: string) => getKeyInfoByGlobalKey(key).displayName;

export default function App() {
  const windowLabel = getCurrentWindow().label;
  const viewerKind: KeyViewerKind | undefined =
    windowLabel === 'hand-overlay'
      ? 'hand'
      : windowLabel === 'foot-overlay'
        ? 'foot'
        : undefined;
  useCustomCssInjection();
  useCustomJsInjection();
  useAppBootstrap(viewerKind);
  useBuiltinStatsSubscription();
  useBlockBrowserShortcuts();
  const { t } = useTranslation();
  const developerModeEnabled = useSettingsStore(
    (state) => state.developerModeEnabled,
  );

  // 개발자 모드 비활성 시 DevTools 단축키 차단
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const isDevtoolsCombo =
        ((e.ctrlKey || e.metaKey) &&
          e.shiftKey &&
          e.key.toLowerCase() === 'i') ||
        e.key === 'F12';
      if (!developerModeEnabled && isDevtoolsCombo) {
        e.preventDefault();
        e.stopPropagation();
      }
    };
    window.addEventListener('keydown', handler, true);
    return () => window.removeEventListener('keydown', handler, true);
  }, [developerModeEnabled]);

  // 윈도우 타입
  useEffect(() => {
    try {
      window.__dmn_window_type = 'overlay';
      window.__dmn_viewer_kind = viewerKind;
    } catch {
      // 무시
    }
    return () => {
      try {
        window.__dmn_window_type = undefined;
        window.__dmn_viewer_kind = undefined;
      } catch {
        // 무시
      }
    };
  }, [viewerKind]);

  // 메인에서 bridge를 통한 positions 동기화 수신
  useEffect(() => {
    const unsubscribe = window.api.bridge.on<{
      positions: Record<string, KeyPosition[]>;
    }>('positions:sync', (data) => {
      if (data?.positions) {
        useKeyStore.setState((state) => ({
          ...state,
          positions: data.positions,
        }));
      }
    });
    return () => unsubscribe();
  }, []);

  // 메인에서 bridge를 통한 statPositions 동기화 수신
  useEffect(() => {
    const unsubscribe = window.api.bridge.on<{
      positions: Record<string, StatItemPosition[]>;
    }>('statPositions:sync', (data) => {
      if (data?.positions) {
        useStatItemStore.setState((state) => ({
          ...state,
          positions: data.positions,
        }));
      }
    });
    return () => unsubscribe();
  }, []);

  // 메인에서 bridge를 통한 graphPositions 동기화 수신
  useEffect(() => {
    const unsubscribe = window.api.bridge.on<{
      positions: Record<string, GraphItemPosition[]>;
    }>('graphPositions:sync', (data) => {
      if (data?.positions) {
        useGraphItemStore.setState((state) => ({
          ...state,
          positions: data.positions,
        }));
      }
    });
    return () => unsubscribe();
  }, []);

  // 메인에서 bridge를 통한 knobPositions 동기화 수신
  useEffect(() => {
    const unsubscribe = window.api.bridge.on<{
      positions: Record<string, KnobItemPosition[]>;
    }>('knobPositions:sync', (data) => {
      if (data?.positions) {
        useKnobItemStore.setState((state) => ({
          ...state,
          positions: data.positions,
        }));
      }
    });
    return () => unsubscribe();
  }, []);

  const selectedKeyType = useKeyStore((state) => state.selectedKeyType);
  const keyMappings = useKeyStore((state) => state.keyMappings);
  const positions = useKeyStore((state) => state.positions);
  // Ghost keys render as extra virtual keys on top of their owners.
  const viewerKeys = useMemo(
    () =>
      expandGhostKeys(
        keyMappings[selectedKeyType] ?? NO_KEYS,
        positions[selectedKeyType] ?? NO_POSITIONS,
        keyLabel,
      ),
    [keyMappings, positions, selectedKeyType],
  );
  const statPositions = useStatItemStore((state) => state.positions);
  const graphPositions = useGraphItemStore((state) => state.positions);
  const knobPositions = useKnobItemStore((state) => state.positions);
  const pluginElements = usePluginDisplayElementStore(
    (state) => state.elements,
  );

  const backgroundColor = useSettingsStore((state) => state.backgroundColor);
  const alwaysOnTop = useSettingsStore((state) => state.alwaysOnTop);
  const trayEnabled = useSettingsStore((state) => state.trayEnabled);
  const setAlwaysOnTop = useSettingsStore((state) => state.setAlwaysOnTop);
  const globalNoteSettings = useSettingsStore((state) => state.noteSettings);
  const tabNoteOverrides = useSettingsStore((state) => state.tabNoteOverrides);
  const noteSettings = mergeNoteSettings(
    globalNoteSettings,
    tabNoteOverrides?.[selectedKeyType],
  );
  const noteEffect = useSettingsStore((state) => state.noteEffect);
  const overlayPadding = useSettingsStore(
    (state) => state.gridSettings.overlayPadding ?? 30,
  );
  const overlayAnchor = useSettingsStore((state) => state.overlayResizeAnchor);
  const keyCounterEnabled = useSettingsStore(
    (state) => state.keyCounterEnabled,
  );
  const tabs = useKeyStore((state) => state.tabs);
  const resolveCanOpenMainSettings = async () => {
    if (!trayEnabled) {
      return false;
    }

    try {
      const mainWindow = await TauriWindow.getByLabel('main');
      if (!mainWindow) {
        return false;
      }
      const isMainVisible = await mainWindow.isVisible();
      return !isMainVisible;
    } catch (error) {
      console.error('Failed to resolve main window visibility', error);
      return false;
    }
  };

  const handleOverlayMouseDownCapture = (
    e: React.MouseEvent<HTMLDivElement>,
  ) => {
    // 좌클릭은 창 전체 드래그 유지
    if (e.button !== 0) return;

    getCurrentWindow()
      .startDragging()
      .catch((error) => {
        console.error('Failed to start overlay dragging', error);
      });
  };

  const closeOverlayWindow = async () => {
    try {
      await window.api.overlay.setVisible(false);
    } catch (error) {
      console.error('Failed to close overlay window', error);
    }
  };

  const toggleAlwaysOnTop = async () => {
    const next = !alwaysOnTop;
    setAlwaysOnTop(next);
    try {
      await window.api.settings.update({ alwaysOnTop: next });
    } catch (error) {
      console.error('Failed to toggle always-on-top', error);
      setAlwaysOnTop(!next);
    }
  };

  const openSettingsWindow = async () => {
    try {
      await window.api.window.showMain();
    } catch (error) {
      console.error('Failed to open settings window', error);
    }
  };

  const quitApplication = async () => {
    try {
      await window.api.app.quit();
    } catch (error) {
      console.error('Failed to quit application', error);
    }
  };

  const snapToNearestEdge = async () => {
    try {
      const win = getCurrentWindow();
      const [monitor, pos, size] = await Promise.all([
        currentMonitor(),
        win.outerPosition(),
        win.outerSize(),
      ]);
      if (!monitor) return;

      const monitorPos = monitor.position;
      const monitorSize = monitor.size;

      // 창의 중심 좌표
      const centerX = pos.x + size.width / 2;
      const centerY = pos.y + size.height / 2;

      // 모니터의 중심 좌표
      const monitorCenterX = monitorPos.x + monitorSize.width / 2;
      const monitorCenterY = monitorPos.y + monitorSize.height / 2;

      // 가장 가까운 모서리 결정
      const snapLeft = centerX < monitorCenterX;
      const snapTop = centerY < monitorCenterY;

      const newX = snapLeft
        ? monitorPos.x
        : monitorPos.x + monitorSize.width - size.width;
      const newY = snapTop
        ? monitorPos.y
        : monitorPos.y + monitorSize.height - size.height;

      await win.setPosition(new PhysicalPosition(newX, newY));
    } catch (error) {
      console.error('Failed to snap overlay to edge', error);
    }
  };

  const openOverlayContextMenuAtImpl = useRef<
    (x: number, y: number) => Promise<void>
  >(async () => {});
  openOverlayContextMenuAtImpl.current = async (x: number, y: number) => {
    const canOpenMainSettings = await resolveCanOpenMainSettings();
    const allTabs = tabs
      .filter((tab) => !viewerKind || tab.viewerKind === viewerKind)
      .map((tab) => ({
        id: tab.id,
        viewerKind: tab.viewerKind,
        name: viewerKind
          ? tab.name
          : `${t(`tabs.${tab.viewerKind}Viewer`)} · ${tab.name}`,
      }));

    let menu: Menu | null = null;
    try {
      menu = await Menu.new({
        items: [
          {
            id: 'toggleAlwaysOnTop',
            text: t('settings.alwaysOnTop'),
            checked: alwaysOnTop,
            action: () => {
              void toggleAlwaysOnTop();
            },
          },
          { item: 'Separator' },
          {
            id: 'selectTab',
            text: t('contextMenu.selectTab'),
            items: allTabs.map((tab) => ({
              id: `selectTab-${tab.id}`,
              text: tab.name,
              checked: tab.id === selectedKeyType,
              action: () => {
                void window.api.keys.tabs.select(tab.viewerKind, tab.id);
              },
            })),
          },
          {
            id: 'closeOverlay',
            text: t('tooltip.overlayClose'),
            action: () => {
              void closeOverlayWindow();
            },
          },
          {
            id: 'snapToEdge',
            text: t('contextMenu.snapToEdge'),
            action: () => {
              void snapToNearestEdge();
            },
          },
          { item: 'Separator' },
          {
            id: 'openSettingsWindow',
            text: t('tooltip.settings'),
            enabled: canOpenMainSettings,
            action: () => {
              void openSettingsWindow();
            },
          },
          { item: 'Separator' },
          {
            id: 'quitApplication',
            text: t('contextMenu.quitApp'),
            action: () => {
              void quitApplication();
            },
          },
        ],
      });

      await menu.popup(
        new LogicalPosition(Math.round(x), Math.round(y)),
        getCurrentWindow(),
      );
    } catch (error) {
      console.error('Failed to open native overlay context menu', error);
    } finally {
      if (menu) {
        await menu.close().catch(() => {});
      }
    }
  };
  const openOverlayContextMenuAt = async (x: number, y: number) => {
    await openOverlayContextMenuAtImpl.current(x, y);
  };

  useEffect(() => {
    const handleWindowContextMenu = (event: MouseEvent) => {
      event.preventDefault();
      event.stopPropagation();
      void openOverlayContextMenuAt(event.clientX, event.clientY);
    };

    window.addEventListener('contextmenu', handleWindowContextMenu, true);
    return () => {
      window.removeEventListener('contextmenu', handleWindowContextMenu, true);
    };
  }, []);

  const {
    notesRef,
    subscribe,
    handleKeyDown,
    handleKeyUp,
    finalizeAllActive,
    noteBuffer,
    updateTrackLayouts,
  } = useNoteSystem({
    noteEffect,
    noteSettings,
  });

  const trackHeight =
    noteSettings?.trackHeight ?? DEFAULT_NOTE_SETTINGS.trackHeight;

  // 키 딜레이 설정
  const keyDisplayDelayMs = Number(noteSettings?.keyDisplayDelayMs ?? 0);

  // 키 딜레이 값을 ref로 관리하여 클로저 문제 방지
  const keyDisplayDelayMsRef = useRef(keyDisplayDelayMs);
  useEffect(() => {
    keyDisplayDelayMsRef.current = keyDisplayDelayMs;
  }, [keyDisplayDelayMs]);

  // 키 딜레이 타이머 관리 (down/up 별도 관리)
  const keyDelayTimersRef = useRef<Map<string, KeyDelayTimerEntry>>(new Map());

  // 키 활성 상태는 signals로 관리하여 App 리렌더를 방지
  const [_layoutVersion, setLayoutVersion] = useState(0);

  useEffect(() => {
    const onResize = () => setLayoutVersion((value) => value + 1);
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  useEffect(() => {
    setLayoutVersion((value) => value + 1);
  }, [trackHeight]);

  // 탭 전환 시 진행 중인 모든 노트 강제 완료
  useEffect(() => {
    finalizeAllActive();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedKeyType]);

  // 이벤트 구독은 오버레이 윈도우 수명 동안 한 번만 유지한다. 다른 viewer의
  // 탭 변경도 tabs 스토어를 갱신해 이 컴포넌트를 다시 렌더하므로, 렌더마다 새로
  // 만들어지는 note handler를 effect dependency로 사용하면 hand 신호까지
  // cleanup에서 초기화된다. 구독 콜백은 ref를 통해 최신 컨텍스트만 읽는다.
  const keyEventContextRef = useRef({
    noteEffect,
    keyMappings,
    positions,
    viewerKeys,
    selectedKeyType,
    handleKeyDown,
    handleKeyUp,
  });
  useEffect(() => {
    keyEventContextRef.current = {
      noteEffect,
      keyMappings,
      positions,
      viewerKeys,
      selectedKeyType,
      handleKeyDown,
      handleKeyUp,
    };
  });

  // 이 viewer가 실제로 다른 탭으로 전환될 때만 예약된 표시와 눌림 신호를
  // 초기화한다. foot 전환으로 hand가 단순 재렌더되는 경우에는 실행되지 않는다.
  const keySignalResetArmedRef = useRef(false);
  useEffect(() => {
    if (!keySignalResetArmedRef.current) {
      keySignalResetArmedRef.current = true;
      return;
    }
    const keyDelayTimers = keyDelayTimersRef.current;
    keyDelayTimers.forEach((timerEntry) => {
      timerEntry.timers.forEach((timer) => clearTimeout(timer));
      timerEntry.timers.clear();
    });
    keyDelayTimers.clear();
    resetAllKeySignals();
  }, [selectedKeyType]);

  useEffect(() => {
    // 키 딜레이 적용된 신호 업데이트
    const updateKeySignalWithDelay = (key: string, isDown: boolean) => {
      const delayMs = keyDisplayDelayMsRef.current;

      let timerEntry = keyDelayTimersRef.current.get(key);
      if (!timerEntry) {
        timerEntry = { timers: new Set() };
        keyDelayTimersRef.current.set(key, timerEntry);
      }

      if (delayMs <= 0) {
        timerEntry.timers.forEach((timer) => clearTimeout(timer));
        timerEntry.timers.clear();
        setKeyActiveSignal(key, isDown);
        return;
      }

      const timer = setTimeout(() => {
        setKeyActiveSignal(key, isDown);
        timerEntry?.timers.delete(timer);
      }, delayMs);
      timerEntry.timers.add(timer);
    };

    // HID 축 이벤트 버스 초기화 (input:axis 구독 → axisSignals 누적)
    import('@utils/core/axisEventBus').then(({ axisEventBus }) => {
      axisEventBus.initialize();
    });

    let disposed = false;
    // 버스를 통해 키 이벤트 수신. 구독을 먼저 등록하고 initialize하여 초기화
    // 직후 들어오는 입력도 놓치지 않는다.
    const unsubscribe = import('@utils/core/keyEventBus').then(
      ({ keyEventBus }) => {
        if (disposed) return undefined;
        const unsubscribeKeyEvents = keyEventBus.subscribe(
          ({ key, state, eventAgeMs }) => {
            const context = keyEventContextRef.current;
            // 전용 viewer의 실제 선택 탭 매핑을 최종 라우팅 기준으로 삼는다.
            // 전역 편집 mode는 마지막으로 선택한 hand/foot 탭을 가리키므로,
            // payload mode를 검사하면 foot 선택 중 hand 입력 전체가 차단된다.
            const currentKeys = context.viewerKeys.keys;
            if (
              !isKeyMappedToViewer(
                key,
                context.selectedKeyType,
                context.keyMappings,
                context.positions,
              )
            )
              return;
            const isDown = state === 'DOWN';
            // 키 UI 업데이트 (딜레이 적용)
            updateKeySignalWithDelay(key, isDown);
            // 노트 이펙트는 즉시 처리 (딜레이 없음)
            if (context.noteEffect) {
              // 개별 키의 noteEffectEnabled 확인
              const currentPositions = context.viewerKeys.positions;
              const keyIndex = currentKeys.indexOf(key);
              const keyPosition = currentPositions[keyIndex];
              const keyNoteEffectEnabled =
                keyPosition?.noteEffectEnabled !== false;

              if (isDown ? keyNoteEffectEnabled : true) {
                // 실제 입력 시각을 복원해 노트 시작 위치를 보정 (프레임 양자화 방지).
                // requestAnimationFrame 래핑 시 노트 생성 시각이 프레임 경계로 양자화돼
                // 주사율/OBS fps에 시간 해상도가 종속되던 문제 해결.
                // age는 0~MAX_EVENT_AGE_MS로 clamp — 백엔드 stall/클럭 이상 시 노트가
                // 화면 위로 튀는 것을 방지
                const age = Math.min(
                  Math.max(eventAgeMs ?? 0, 0),
                  MAX_EVENT_AGE_MS,
                );
                const inputTime = performance.now() - age;
                if (isDown) context.handleKeyDown(key, inputTime);
                else context.handleKeyUp(key, inputTime);
              }
            }
          },
        );
        keyEventBus.initialize();
        return unsubscribeKeyEvents;
      },
    );

    const keyDelayTimers = keyDelayTimersRef.current;

    return () => {
      disposed = true;
      unsubscribe.then((unsub) => {
        try {
          unsub?.();
        } catch (error) {
          console.error('Failed to remove key state listener', error);
        }
      });
      // 키 딜레이 타이머 정리
      keyDelayTimers.forEach((timerEntry) => {
        timerEntry.timers.forEach((timer) => clearTimeout(timer));
        timerEntry.timers.clear();
      });
      keyDelayTimers.clear();
      // 마운트 1회 effect이므로 실제 윈도우 언마운트에서만 실행된다.
      resetAllKeySignals();
    };
  }, []);

  const currentKeys = viewerKeys.keys;
  const currentPositions = viewerKeys.positions;
  const currentStatPositions = statPositions[selectedKeyType] ?? [];
  const currentGraphPositions = graphPositions[selectedKeyType] ?? [];
  const currentKnobPositions = knobPositions[selectedKeyType] ?? [];

  const {
    bounds,
    displayPositions,
    displayStatPositions,
    displayGraphPositions,
    displayKnobPositions,
    positionOffset,
    webglTracks,
  } = computeLayout({
    currentKeys,
    currentPositions,
    currentStatPositions,
    currentGraphPositions,
    currentKnobPositions,
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
      displayPositions={displayPositions}
      currentPositions={currentPositions}
      displayStatPositions={displayStatPositions}
      displayGraphPositions={displayGraphPositions}
      displayKnobPositions={displayKnobPositions}
      selectedKeyType={selectedKeyType}
      noteEffect={noteEffect}
      noteSettings={noteSettings}
      webglTracks={webglTracks}
      notesRef={notesRef}
      subscribe={subscribe}
      noteBuffer={noteBuffer}
      backgroundColor={backgroundColor}
      keyCounterEnabled={keyCounterEnabled}
      positionOffset={positionOffset}
      onMouseDownCapture={handleOverlayMouseDownCapture}
      showPluginElements={true}
    />
  );
}
