/**
 * Grid 컨텍스트 메뉴 관련 로직 훅
 * - 키 컨텍스트 메뉴 항목 생성
 * - 그리드 컨텍스트 메뉴 항목 생성
 */

import type { ListItem } from '@components/main/Modal/ListPopup';
import { usePluginMenuStore } from '@stores/plugin/usePluginMenuStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import { translatePluginMessage } from '@utils/plugin/pluginI18n';
import type { KeyPosition } from '@src/types/key/keys';
import type {
  PluginMenuItemInternal,
  KeyMenuContext,
  GridMenuContext,
  PluginMessages,
} from '@src/types/plugin/api';

interface MenuItem {
  id: string;
  label: string;
  disabled?: boolean;
  isPlugin?: boolean;
}

// 자식이 전부 잠겨 있으면 묶음 행도 잠근다. 열 수 있어 보이는 빈 서브메뉴를 막는다.
const groupRow = (
  id: string,
  label: string,
  children: MenuItem[],
): ListItem => ({
  id,
  label,
  children,
  disabled: children.every((child) => child.disabled),
});

interface KeyContext {
  keyCode: string;
  index: number;
  position: KeyPosition;
  mode: string;
}

interface GridContext {
  position: { dx: number; dy: number };
  mode: string;
}

interface UseGridContextMenuParams {
  selectedKeyType: string;
  keyMappings: Record<string, string[]>;
  positions: Record<string, KeyPosition[]>;
  locale: string;
  t: (key: string) => string;
  noteEffect?: boolean;
}

interface UseGridContextMenuReturn {
  getKeyMenuItems: (contextIndex: number | null) => MenuItem[];
  getStatMenuItems: (contextIndex: number | null) => MenuItem[];
  getGraphMenuItems: (contextIndex: number | null) => MenuItem[];
  getKnobMenuItems: (contextIndex: number | null) => MenuItem[];
  getGridMenuItems: (
    gridAddLocalPos: { dx: number; dy: number } | null,
  ) => ListItem[];
  pluginKeyMenuItems: PluginMenuItemInternal<KeyMenuContext>[];
  pluginGridMenuItems: PluginMenuItemInternal<GridMenuContext>[];
  resolvePluginLabel: (pluginId: string, rawLabel: string) => string;
}

/**
 * 컨텍스트 메뉴 훅
 */
export function useGridContextMenu({
  selectedKeyType,
  keyMappings,
  positions,
  locale,
  t,
  noteEffect,
}: UseGridContextMenuParams): UseGridContextMenuReturn {
  // 전역 CSS 활성화 여부
  const useCustomCSS = useSettingsStore((state) => state.useCustomCSS);
  // OBS 모드는 데스크톱 오버레이 창을 없애므로 되돌릴 위치가 없다
  const obsModeEnabled = useSettingsStore((state) => state.obsModeEnabled);

  // 플러그인 메뉴 아이템
  const pluginKeyMenuItems = usePluginMenuStore((state) => state.keyMenuItems);
  const pluginGridMenuItems = usePluginMenuStore(
    (state) => state.gridMenuItems,
  );
  const pluginDefinitions = usePluginDisplayElementStore(
    (state) => state.definitions,
  );

  const pluginMessagesById = (() => {
    const map = new Map<string, PluginMessages>();
    pluginDefinitions.forEach((def) => {
      if (!map.has(def.pluginId)) {
        map.set(def.pluginId, def.messages);
      }
    });
    return map;
  })();

  const resolvePluginLabel = (pluginId: string, rawLabel: string) =>
    translatePluginMessage({
      messages: pluginMessagesById.get(pluginId),
      locale,
      key: rawLabel,
      fallback: rawLabel,
    });

  // 키 메뉴 아이템 생성 (기본 + 플러그인)
  const getKeyMenuItems = (contextIndex: number | null): MenuItem[] => {
    const baseItems: MenuItem[] = [
      { id: 'delete', label: t('contextMenu.deleteKey') },
      { id: 'duplicate', label: t('contextMenu.duplicateKey') },
      { id: 'counterReset', label: t('contextMenu.counterReset') },
      { id: 'bringToFront', label: t('contextMenu.bringToFront') },
      // { id: "bringForward", label: t("contextMenu.bringForward") },
      // { id: "sendBackward", label: t("contextMenu.sendBackward") },
      { id: 'sendToBack', label: t('contextMenu.sendToBack') },
    ];

    // 플러그인 메뉴 필터링 (조건부 표시)
    const keyPosition =
      contextIndex !== null
        ? positions[selectedKeyType]?.[contextIndex]
        : undefined;
    const context: KeyContext | null =
      contextIndex !== null && keyPosition
        ? {
            keyCode: keyMappings[selectedKeyType]?.[contextIndex] || '',
            index: contextIndex,
            position: keyPosition,
            mode: selectedKeyType,
          }
        : null;

    const filterPluginItems = (
      items: PluginMenuItemInternal<unknown>[],
    ): MenuItem[] => {
      if (!context) return [];
      return items
        .filter((item) => {
          // visible 체크
          if (item.visible === false) return false;
          if (typeof item.visible === 'function' && !item.visible(context))
            return false;
          return true;
        })
        .map((item) => ({
          id: item.fullId,
          label: resolvePluginLabel(item.pluginId, item.label),
          disabled:
            typeof item.disabled === 'function'
              ? item.disabled(context)
              : item.disabled || false,
          isPlugin: true,
        }));
    };

    const topPluginItems = filterPluginItems(
      pluginKeyMenuItems.filter((i) => i.position === 'top'),
    );
    const bottomPluginItems = filterPluginItems(
      pluginKeyMenuItems.filter((i) => i.position !== 'top'),
    );

    return [...topPluginItems, ...baseItems, ...bottomPluginItems];
  };

  // 그리드 메뉴 아이템 생성 (기본 + 플러그인)
  const getStatMenuItems = (_contextIndex: number | null): MenuItem[] => [
    { id: 'delete', label: t('contextMenu.deleteStat') },
    { id: 'duplicate', label: t('contextMenu.duplicateStat') },
    { id: 'bringToFront', label: t('contextMenu.bringToFront') },
    { id: 'sendToBack', label: t('contextMenu.sendToBack') },
  ];

  const getGraphMenuItems = (_contextIndex: number | null): MenuItem[] => [
    { id: 'delete', label: t('contextMenu.deleteGraph') },
    { id: 'duplicate', label: t('contextMenu.duplicateGraph') },
    { id: 'bringToFront', label: t('contextMenu.bringToFront') },
    { id: 'sendToBack', label: t('contextMenu.sendToBack') },
  ];

  const getKnobMenuItems = (_contextIndex: number | null): MenuItem[] => [
    { id: 'delete', label: t('contextMenu.deleteKnob') },
    { id: 'duplicate', label: t('contextMenu.duplicateKnob') },
    { id: 'bringToFront', label: t('contextMenu.bringToFront') },
    { id: 'sendToBack', label: t('contextMenu.sendToBack') },
  ];

  const getGridMenuItems = (
    gridAddLocalPos: { dx: number; dy: number } | null,
  ): ListItem[] => {
    const addItems: MenuItem[] = [
      { id: 'add', label: t('contextMenu.addKey') },
      { id: 'addStat', label: t('contextMenu.addStat') },
      { id: 'addGraph', label: t('contextMenu.addGraph') },
      { id: 'addKnob', label: t('contextMenu.addKnob') },
    ];
    // 묶음 안에서는 부모 행이 맥락을 주므로 라벨을 줄인다
    const tabItems: MenuItem[] = [
      {
        id: 'tabCss',
        label: t('contextMenu.cssSetting'),
        disabled: !useCustomCSS,
      },
      {
        id: 'tabNote',
        label: t('contextMenu.trackSetting'),
        disabled: !noteEffect,
      },
    ];
    // 화면 밖으로 끌려가 잡을 수 없는 오버레이를 메인 창에서 되찾는 길
    const resetItems: MenuItem[] = obsModeEnabled
      ? []
      : [
          {
            id: 'resetOverlayPosition',
            label: t('contextMenu.overlayTarget'),
          },
        ];

    // 플러그인 메뉴 필터링
    const context: GridContext | null = gridAddLocalPos
      ? {
          position: gridAddLocalPos,
          mode: selectedKeyType,
        }
      : null;

    const filterPluginItems = (
      items: PluginMenuItemInternal<unknown>[],
    ): MenuItem[] => {
      if (!context) return [];
      return items
        .filter((item) => {
          if (item.visible === false) return false;
          if (typeof item.visible === 'function' && !item.visible(context))
            return false;
          return true;
        })
        .map((item) => ({
          id: item.fullId,
          label: resolvePluginLabel(item.pluginId, item.label),
          disabled:
            typeof item.disabled === 'function'
              ? item.disabled(context)
              : item.disabled || false,
          isPlugin: true,
        }));
    };

    const topPluginItems = filterPluginItems(
      pluginGridMenuItems.filter((i) => i.position === 'top'),
    );
    const bottomPluginItems = filterPluginItems(
      pluginGridMenuItems.filter((i) => i.position !== 'top'),
    );

    // 추가 동작만 맨 위에 두고 설정·복구 동작은 묶음으로 접는다
    const groupRows: ListItem[] = [
      groupRow('tabGroup', t('contextMenu.currentTabGroup'), tabItems),
      ...(resetItems.length > 0
        ? [
            groupRow(
              'resetGroup',
              t('contextMenu.resetPositionGroup'),
              resetItems,
            ),
          ]
        : []),
    ];

    return [
      ...topPluginItems,
      ...addItems,
      ...bottomPluginItems,
      { id: 'separator', label: '', type: 'separator' },
      ...groupRows,
    ];
  };

  return {
    getKeyMenuItems,
    getStatMenuItems,
    getGraphMenuItems,
    getKnobMenuItems,
    getGridMenuItems,
    pluginKeyMenuItems,
    pluginGridMenuItems,
    resolvePluginLabel,
  };
}
