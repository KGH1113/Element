import React, {
  useCallback,
  useEffect,
  useState,
  useRef,
  type ReactNode,
} from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useHistoryStore } from '@stores/data/useHistoryStore';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import { usePropertiesPanelStore } from '@stores/grid/usePropertiesPanelStore';
import { useLayerGroupStore } from '@stores/data/useLayerGroupStore';
import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';
import { translatePluginMessage } from '@utils/plugin/pluginI18n';
import {
  toRgbHexColor,
  parseAlphaPercent,
  hexWithAlphaPercent,
} from '@utils/color/colorUtils';
import type { GhostKey, KeyPosition } from '@src/types/key/keys';
import {
  createDefaultGhostSettings,
  mergeGhostSettings,
} from '@src/renderer/editor/model/ghostKeys';
import type { StatItemPosition, StatItemType } from '@src/types/key/statItems';
import type { GraphItemPosition } from '@src/types/key/graphItems';
import type { KnobItemPosition } from '@src/types/key/knobs';
import type {
  PluginSettingSchema,
  PluginMessages,
  RawInputPayload,
} from '@src/types/plugin/api';
import {
  createDefaultCounterSettings,
  normalizeCounterSettings,
} from '@src/types/key/keys';
import { useLenis } from '@hooks/useLenis';

// 분리된 컴포넌트들 및 훅
import {
  TABS,
  TabType,
  PropertyRow,
  NumberInput,
  ColorInput,
  TextInput,
  SectionDivider,
  PluginSelectionPanel,
  SingleGraphPanel,
  SingleKnobPanel,
  SingleKeyStatPanel,
  BatchKeyLikePanel,
  BatchGraphOnlyPanel,
  BatchKnobOnlyPanel,
  PluginSettingsPanelView,
  useBatchHandlers,
  usePanelScroll,
} from './PropertiesPanel/index';
import LayerTabContent from './PropertiesPanel/layer/LayerTabContent';
import SpritePanel from './PropertiesPanel/single/SpritePanel';
import {
  KeyCaptureButton,
  KnobAxisCapture,
  NumberField,
} from './PropertiesPanel/PartControls';
import { spriteElementParts } from './PropertiesPanel/single/spriteElementParts';
import { spriteKeyOptions } from './PropertiesPanel/single/spriteParts';
import { editSprite } from '@src/renderer/editor/runtime/spritePositions';
import { SPRITE_CONSTRAINTS } from '@src/types/key/sprites';
import type { InspectorAction } from './PropertiesPanel/types';
import ElementPages from '@components/main/Sidebar/ElementPages';
import type {
  ElementPart,
  InlinePart,
  PagePart,
} from '@components/main/Sidebar/elementParts';
import {
  RowText,
  SidebarPage,
  type PageDirection,
} from '@components/main/Sidebar/SidebarParts';
import {
  CountThumb,
  FaceThumb,
  GraphThumb,
  KeyThumb,
  KnobThumb,
  NoteThumb,
  TextThumb,
} from '@components/main/Sidebar/PartThumbs';
import { PART_ICONS } from '@components/main/Sidebar/sidebarIcons';
import type { PreviewPart } from '@src/renderer/editor/runtime/partPreview';
import { useSidebarStore } from '@components/main/Sidebar/sidebarStore';
import { InspectorPageContext } from './PropertiesPanel/inspectorPageContext';
import { GRAPH_COLORS, KEY_FACE_COLORS } from './PropertiesPanel/partDefaults';
import Checkbox from '@components/main/common/Checkbox';
import Dropdown from '@components/main/common/Dropdown';
import Toggle from '@components/main/common/Toggle';
import type { NoteColor } from '@src/types/key/keys';

const getStatTypeLabel = (statType?: StatItemType | null): string => {
  switch (statType) {
    case 'kpsAvg':
      return 'AVG';
    case 'kpsMax':
      return 'MAX';
    case 'total':
      return 'Total';
    case 'kps':
    default:
      return 'KPS';
  }
};

// Which settings content a page of a key (or stat) shows.
const KEY_PART_ACTION: Record<string, InspectorAction> = {
  sound: 'input',
  layout: 'layout',
  shape: 'style',
  text: 'style',
  note: 'note',
  counter: 'counter',
};

/** The page of a key's second face; the pages of that face open from it. */
const GHOST_PART = 'ghost';

const STAT_TYPES: StatItemType[] = ['kps', 'kpsAvg', 'kpsMax', 'total'];

const actionTab = (action: InspectorAction | undefined): TabType =>
  action === 'input'
    ? TABS.INPUT
    : action === 'note'
      ? TABS.NOTE
      : action === 'counter'
        ? TABS.COUNTER
        : TABS.STYLE;

/** What the sidebar shows for the selection; each kind has its own parts. */
type SelectionKind =
  | 'none'
  | 'batchKeyLike'
  | 'batchKnob'
  | 'batchGraph'
  | 'plugin'
  | 'knob'
  | 'sprite'
  | 'graph'
  | 'key'
  | 'stat'
  | 'unsupported';

export interface ElementListSlots {
  direction: PageDirection;
  /** The elements of the open tab. */
  list: ReactNode;
  /** How many are picked in the list without being opened yet. */
  selectionCount: number;
  onOpenSelection: () => void;
}

// ============================================================================
// 메인 컴포넌트 Props
// ============================================================================

interface PropertiesPanelProps {
  onPositionChange: (index: number, dx: number, dy: number) => void;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyBatchUpdate?: (
    updates: Array<{ index: number } & Partial<KeyPosition>>,
    options?: { skipHistory?: boolean },
  ) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  onKeyBatchPreview?: (
    updates: Array<{ index: number } & Partial<KeyPosition>>,
  ) => void;
  onKeyMappingChange?: (index: number, newKey: string) => void;
  /** The first page of the sidebar, shown while no element is opened. */
  renderElementList: (slots: ElementListSlots) => ReactNode;
}

// ============================================================================
// 메인 컴포넌트
// ============================================================================

const PropertiesPanel: React.FC<PropertiesPanelProps> = ({
  onPositionChange,
  onKeyUpdate,
  onKeyBatchUpdate,
  onKeyPreview,
  onKeyBatchPreview,
  onKeyMappingChange,
  renderElementList,
}) => {
  const { t, i18n } = useTranslation();
  const selectedElements = useGridSelectionStore(
    (state) => state.selectedElements,
  );
  const pushHistoryState = useHistoryStore((state) => state.pushState);
  const selectedKeyType = useKeyStore((state) => state.selectedKeyType);
  const positions = useKeyStore((state) => state.positions);
  const keyMappings = useKeyStore((state) => state.keyMappings);
  const statItemPositions = useStatItemStore((state) => state.positions);
  const graphItemPositions = useGraphItemStore((state) => state.positions);
  const { useCustomCSS } = useSettingsStore();
  const pluginElements = usePluginDisplayElementStore(
    (state) => state.elements,
  );
  const pluginDefinitions = usePluginDisplayElementStore(
    (state) => state.definitions,
  );
  const updatePluginElement = usePluginDisplayElementStore(
    (state) => state.updateElement,
  );
  const pluginSettingsPanel = usePropertiesPanelStore(
    (state) => state.pluginSettingsPanel,
  );
  const closePluginSettingsPanel = usePropertiesPanelStore(
    (state) => state.closePluginSettingsPanel,
  );
  const requestDelete = useSidebarStore((state) => state.requestDelete);
  const locale = i18n.language;

  // 선택된 키 요소 필터링
  const selectedKeyElements = selectedElements.filter(
    (el) => el.type === 'key',
  );
  const selectedStatElements = selectedElements.filter(
    (el) => el.type === 'stat',
  );
  const selectedGraphElements = selectedElements.filter(
    (el) => el.type === 'graph',
  );
  const selectedKnobElements = selectedElements.filter(
    (el) => el.type === 'knob',
  );
  const selectedSpriteElements = selectedElements.filter(
    (el) => el.type === 'sprite',
  );
  const selectedKeyLikeElements = selectedElements.filter(
    (el) => el.type === 'key' || el.type === 'stat',
  );
  const selectedBatchStyleElements = selectedElements.filter(
    (el) =>
      el.type === 'key' ||
      el.type === 'stat' ||
      el.type === 'graph' ||
      el.type === 'knob',
  );
  const selectedPluginElements = selectedElements.filter(
    (el) => el.type === 'plugin',
  );

  const selectedPluginElement = (() => {
    if (selectedPluginElements.length !== 1) return null;
    return (
      pluginElements.find((el) => el.fullId === selectedPluginElements[0].id) ||
      null
    );
  })();

  const selectedPluginDefinition = (() => {
    if (!selectedPluginElement?.definitionId) return null;
    return pluginDefinitions.get(selectedPluginElement.definitionId) || null;
  })();

  const pluginSettingsUI = selectedPluginDefinition?.settingsUI ?? 'panel';
  const hasSinglePluginSelection =
    selectedPluginElements.length === 1 && !!selectedPluginElement;
  const showModalHint =
    hasSinglePluginSelection && pluginSettingsUI === 'modal';
  const showSettings = hasSinglePluginSelection && pluginSettingsUI !== 'modal';
  const isPluginResizable =
    hasSinglePluginSelection && !!selectedPluginDefinition?.resizable;

  const pluginDisplaySize = (() => {
    const measured = selectedPluginElement?.measuredSize;
    const estimated = selectedPluginElement?.estimatedSize;
    return {
      width: measured?.width ?? estimated?.width ?? 200,
      height: measured?.height ?? estimated?.height ?? 150,
    };
  })();

  // 단일 키 선택인 경우의 데이터
  const singleKeyIndex =
    selectedKeyElements.length === 1 ? selectedKeyElements[0].index : null;
  const singleKeyPosition =
    singleKeyIndex !== null
      ? positions[selectedKeyType]?.[singleKeyIndex]
      : null;
  const singleKeyCode =
    singleKeyIndex !== null
      ? keyMappings[selectedKeyType]?.[singleKeyIndex]
      : null;
  const singleKeyInfo = singleKeyCode
    ? getKeyInfoByGlobalKey(singleKeyCode)
    : null;

  // 단일 통계 요소 선택인 경우의 데이터
  const singleStatIndex =
    selectedStatElements.length === 1 ? selectedStatElements[0].index : null;
  const singleStatPosition: StatItemPosition | null =
    singleStatIndex !== null
      ? (statItemPositions[selectedKeyType]?.[singleStatIndex] ?? null)
      : null;
  const singleGraphIndex =
    selectedGraphElements.length === 1 ? selectedGraphElements[0].index : null;
  const singleGraphPosition: GraphItemPosition | null =
    singleGraphIndex !== null
      ? (graphItemPositions[selectedKeyType]?.[singleGraphIndex] ?? null)
      : null;
  const knobItemPositions = useKnobItemStore((state) => state.positions);
  const singleKnobIndex =
    selectedKnobElements.length === 1 ? selectedKnobElements[0].index : null;
  const singleKnobPosition: KnobItemPosition | null =
    singleKnobIndex != null
      ? (knobItemPositions[selectedKeyType]?.[singleKnobIndex] ?? null)
      : null;
  const spritePositions = useSpriteStore((state) => state.positions);
  // One image and nothing else: its page. Images picked with anything else
  // are moved and resized on the canvas; they have no shared page.
  const singleSpriteIndex =
    selectedElements.length === 1 && selectedSpriteElements.length === 1
      ? selectedSpriteElements[0].index
      : null;
  const singleSpritePosition =
    singleSpriteIndex != null
      ? (spritePositions[selectedKeyType]?.[singleSpriteIndex] ?? null)
      : null;
  const spriteDefaultTitle =
    singleSpriteIndex != null
      ? `${t('sidebar.addSprite')} ${singleSpriteIndex + 1}`
      : '';
  const allLayerGroups = useLayerGroupStore((state) => state.layerGroups);
  const layerGroupsForMode = allLayerGroups[selectedKeyType] || [];
  const selectedGroupInfo = (() => {
    if (selectedElements.length < 2 || selectedPluginElements.length > 0) {
      return null;
    }

    const keyModePositions = positions[selectedKeyType] || [];
    const statModePositions = statItemPositions[selectedKeyType] || [];
    const graphModePositions = graphItemPositions[selectedKeyType] || [];
    const knobModePositions = knobItemPositions[selectedKeyType] || [];

    let groupId: string | undefined;

    for (const element of selectedElements) {
      let currentGroupId: string | undefined;
      if (element.type === 'key' && typeof element.index === 'number') {
        currentGroupId = keyModePositions[element.index]?.groupId;
      } else if (element.type === 'stat' && typeof element.index === 'number') {
        currentGroupId = statModePositions[element.index]?.groupId;
      } else if (
        element.type === 'graph' &&
        typeof element.index === 'number'
      ) {
        currentGroupId = graphModePositions[element.index]?.groupId;
      } else if (element.type === 'knob' && typeof element.index === 'number') {
        currentGroupId = knobModePositions[element.index]?.groupId;
      } else {
        return null;
      }

      if (!currentGroupId) return null;
      if (!groupId) {
        groupId = currentGroupId;
      } else if (groupId !== currentGroupId) {
        return null;
      }
    }

    if (!groupId) return null;

    const totalMembers =
      keyModePositions.filter((pos) => pos?.groupId === groupId).length +
      statModePositions.filter((pos) => pos?.groupId === groupId).length +
      graphModePositions.filter((pos) => pos?.groupId === groupId).length +
      knobModePositions.filter((pos) => pos?.groupId === groupId).length;

    if (totalMembers < 2 || totalMembers !== selectedElements.length) {
      return null;
    }

    const groupDef = layerGroupsForMode.find((group) => group.id === groupId);
    if (!groupDef) return null;

    return { id: groupDef.id, name: groupDef.name, memberCount: totalMembers };
  })();

  // 로컬 상태 (실시간 편집용)
  const [localState, setLocalState] = useState<
    Partial<KeyPosition> & { dx?: number; dy?: number }
  >({});
  const pluginSettingsHistoryRef = useRef<string | null>(null);
  const pluginTransformHistoryRef = useRef<string | null>(null);

  // preview가 store를 직접 변경하므로, commit이 아닌 preview 시작 시 히스토리 저장
  const statPreviewHistorySavedRef = useRef(false);
  const graphPreviewHistorySavedRef = useRef(false);
  const knobPreviewHistorySavedRef = useRef(false);
  const [pluginPanelSettings, setPluginPanelSettings] = useState<
    Record<string, unknown>
  >({});

  // 레이어 이름 변경 상태
  const [renameValue, setRenameValue] = useState('');
  const renameInputRef = useRef<HTMLInputElement>(null);
  const renameCancelledRef = useRef(false);

  // 키 리스닝 상태
  const [isListening, setIsListening] = useState(false);
  // Which binding the next key press assigns: the key itself or its ghost.
  const [listenTarget, setListenTarget] = useState<'default' | 'ghost'>(
    'default',
  );
  const justAssignedRef = useRef(false);

  // Ghost edits always merge into the latest stored ghost, so rapid partial
  // updates from the tabs never overwrite each other.
  const readKey = (index: number): KeyPosition | undefined => {
    const state = useKeyStore.getState();
    return state.positions[state.selectedKeyType]?.[index];
  };

  const writeGhost = (index: number, next: (ghost: GhostKey) => GhostKey) => {
    const owner = readKey(index);
    if (!owner) return;
    const current = owner.ghost ?? {
      enabled: false,
      key: null,
      settings: createDefaultGhostSettings(owner),
    };
    onKeyUpdate({ index, ghost: next(current) });
  };

  const listeningFlagTimerRef = useRef<ReturnType<typeof setTimeout> | null>(
    null,
  );

  // 이미지 픽커 상태
  const [showImagePicker, setShowImagePicker] = useState(false);
  const imageButtonRef = useRef<HTMLButtonElement>(null);
  const [showGraphImagePicker, setShowGraphImagePicker] = useState(false);
  const graphImageButtonRef = useRef<HTMLButtonElement>(null);
  const [graphClassNameDraft, setGraphClassNameDraft] = useState('');

  // 다중 선택용 이미지 픽커 상태
  const [showBatchImagePicker, setShowBatchImagePicker] = useState(false);
  const batchImageButtonRef = useRef<HTMLButtonElement>(null);

  // 일괄 편집용 컬러 버튼 refs
  const batchNoteColorButtonRef = useRef<HTMLButtonElement>(null);
  const batchGlowColorButtonRef = useRef<HTMLButtonElement>(null);
  const batchBorderColorButtonRef = useRef<HTMLButtonElement>(null);
  const batchCounterFillButtonRef = useRef<HTMLDivElement>(null);
  const batchCounterStrokeButtonRef = useRef<HTMLDivElement>(null);

  // 패널 ref (컬러픽커/이미지픽커 위치 기준)
  const [panelElement, setPanelElement] = useState<HTMLDivElement | null>(null);

  // What the sidebar shows: the element list, or the selected element.
  const [view, setView] = useState<'list' | 'element'>(() =>
    useGridSelectionStore.getState().selectedElements.length > 0
      ? 'element'
      : 'list',
  );
  // The way the last page change went, for the page's entry motion.
  const [direction, setDirection] = useState<PageDirection>('none');
  // The open pages of the element, from its own page down: empty on the
  // element's page. It stays while the selection changes, so the same page
  // can be set on one element after another.
  const [partPath, setPartPath] = useState<string[]>([]);
  const activePart = partPath[partPath.length - 1] ?? null;
  // Which face of a key is edited: the key itself or its ghost.
  const binding: 'default' | 'ghost' =
    partPath[0] === GHOST_PART ? 'ghost' : 'default';
  // A selection made in the element list keeps the list in view.
  const selectionFromLayerPanelRef = useRef(false);

  const focusedAction: InspectorAction | undefined = activePart
    ? KEY_PART_ACTION[activePart]
    : undefined;
  const activeTab = actionTab(focusedAction);
  const setActiveTab = (_tab: TabType) => {};
  const selectionIdentity =
    selectedKeyType +
    ':' +
    selectedElements
      .map((item) => `${item.type}:${item.id}`)
      .sort()
      .join(',');

  // 레이어 이름 변경: 현재 선택된 요소의 layerName 가져오기
  const getCurrentLayerName = (): string => {
    if (selectedGroupInfo) return selectedGroupInfo.name || '';
    if (singleKeyPosition) return singleKeyPosition.layerName || '';
    if (singleStatPosition) return singleStatPosition.layerName || '';
    if (singleGraphPosition) return singleGraphPosition.layerName || '';
    if (singleKnobPosition) return singleKnobPosition.layerName || '';
    if (singleSpritePosition) return singleSpritePosition.layerName || '';
    return '';
  };

  // 레이어 이름 변경: 현재 선택된 요소의 기본 표시 이름 가져오기
  const getCurrentDefaultTitle = (): string => {
    if (selectedGroupInfo) return selectedGroupInfo.name;
    if (singleKeyPosition) {
      return singleKeyInfo?.displayName || singleKeyCode || 'Key';
    }
    if (singleStatPosition) {
      return getStatTypeLabel(singleStatPosition.statType ?? null);
    }
    if (singleGraphPosition) {
      return `${getStatTypeLabel(singleGraphPosition.statType ?? null)} Graph`;
    }
    if (singleKnobPosition) return 'Knob';
    if (singleSpritePosition) return spriteDefaultTitle;
    return '';
  };

  const handleGroupRenameCommit = async (groupId: string, value: string) => {
    const trimmed = value.trim();
    if (trimmed === '') return;

    const currentGroups = useLayerGroupStore.getState().layerGroups;
    const currentModeGroups = currentGroups[selectedKeyType] || [];
    const currentGroup = currentModeGroups.find(
      (group) => group.id === groupId,
    );
    if (!currentGroup || currentGroup.name === trimmed) return;

    const { keyMappings: km, positions: pos } = useKeyStore.getState();
    const statPos = useStatItemStore.getState().positions;
    const graphPos = useGraphItemStore.getState().positions;
    const pluginEls = usePluginDisplayElementStore.getState().elements;

    useHistoryStore.getState().pushState({
      keyMappings: km,
      positions: pos,
      statPositions: statPos,
      graphPositions: graphPos,
      pluginElements: pluginEls,
      layerGroups: currentGroups,
    });

    const updated = {
      ...currentGroups,
      [selectedKeyType]: currentModeGroups.map((group) =>
        group.id === groupId ? { ...group, name: trimmed } : group,
      ),
    };

    useLayerGroupStore.getState().setLayerGroups(updated);
    try {
      await window.api.layerGroups.update(updated);
    } catch (error) {
      console.error('Failed to rename group', error);
    }
  };

  // 레이어 이름 변경 시작
  const handleRenameStartImpl = useRef<() => void>(() => {});
  handleRenameStartImpl.current = () => {
    const current = getCurrentLayerName();
    setRenameValue(current || getCurrentDefaultTitle());
    requestAnimationFrame(() => {
      renameInputRef.current?.focus();
      renameInputRef.current?.select();
    });
  };
  const handleRenameStart = () => {
    handleRenameStartImpl.current();
  };

  // 레이어 이름 변경 커밋
  const handleRenameCommit = async (value: string) => {
    if (selectedGroupInfo) {
      await handleGroupRenameCommit(selectedGroupInfo.id, value);
      return;
    }

    const trimmed = value.trim();
    const defaultTitle = getCurrentDefaultTitle();
    const newLayerName =
      trimmed === defaultTitle || trimmed === '' ? undefined : trimmed;

    if (singleKeyIndex !== null && singleKeyPosition) {
      onKeyUpdate({
        index: singleKeyIndex,
        layerName: newLayerName,
      } as Partial<KeyPosition> & { index: number });
    } else if (singleStatIndex !== null && singleStatPosition) {
      const mode = selectedKeyType;
      const current = useStatItemStore.getState().positions;
      const list = current[mode] || [];
      if (list[singleStatIndex]) {
        const nextList = list.map((pos, i) =>
          i === singleStatIndex ? { ...pos, layerName: newLayerName } : pos,
        );
        const nextPositions = { ...current, [mode]: nextList };
        useStatItemStore.getState().setLocalUpdateInProgress(true);
        useStatItemStore.getState().setPositions(nextPositions);
        try {
          await window.api.statItems.updatePositions(nextPositions);
        } finally {
          useStatItemStore.getState().setLocalUpdateInProgress(false);
        }
      }
    } else if (singleGraphIndex !== null && singleGraphPosition) {
      const mode = selectedKeyType;
      const current = useGraphItemStore.getState().positions;
      const list = current[mode] || [];
      if (list[singleGraphIndex]) {
        const nextList = list.map((pos, i) =>
          i === singleGraphIndex ? { ...pos, layerName: newLayerName } : pos,
        );
        const nextPositions = { ...current, [mode]: nextList };
        useGraphItemStore.getState().setLocalUpdateInProgress(true);
        useGraphItemStore.getState().setPositions(nextPositions);
        try {
          await window.api.graphItems.updatePositions(nextPositions);
        } finally {
          useGraphItemStore.getState().setLocalUpdateInProgress(false);
        }
      }
    } else if (singleKnobIndex !== null && singleKnobPosition) {
      const mode = selectedKeyType;
      const current = useKnobItemStore.getState().positions;
      const list = current[mode] || [];
      if (list[singleKnobIndex]) {
        const nextList = list.map((pos, i) =>
          i === singleKnobIndex ? { ...pos, layerName: newLayerName } : pos,
        );
        const nextPositions = { ...current, [mode]: nextList };
        useKnobItemStore.getState().setLocalUpdateInProgress(true);
        useKnobItemStore.getState().setPositions(nextPositions);
        try {
          await window.api.knobItems.updatePositions(nextPositions);
          window.api.bridge.sendTo('overlay', 'knobPositions:sync', {
            positions: nextPositions,
          });
        } finally {
          useKnobItemStore.getState().setLocalUpdateInProgress(false);
        }
      }
    } else if (singleSpritePosition) {
      editSprite(selectedKeyType, singleSpritePosition.id, (sprite) => ({
        ...sprite,
        layerName: newLayerName,
      }));
    }
  };

  // 레이어 이름 변경 취소
  const handleRenameCancel = () => {
    renameCancelledRef.current = true;
  };

  // 스크롤 훅 사용
  const {
    batchScrollRefFor,
    batchThumbRefFor,
    singleScrollRefFor,
    singleThumbRefFor,
  } = usePanelScroll(activeTab, selectedElements.length);

  // 플러그인 패널 스크롤
  const pluginScrollElementRef = useRef<HTMLDivElement | null>(null);
  const pluginThumbRef = useRef<HTMLDivElement | null>(null);

  const calculatePluginThumb = (el: HTMLDivElement) => {
    const { scrollTop, scrollHeight, clientHeight } = el;
    const canScroll = scrollHeight > clientHeight + 1;
    if (!canScroll) return { top: 0, height: 0, visible: false };

    const minThumbHeight = 16;
    const height = Math.max(
      minThumbHeight,
      (clientHeight / scrollHeight) * clientHeight,
    );
    const maxTop = clientHeight - height;
    const top =
      maxTop <= 0 ? 0 : (scrollTop / (scrollHeight - clientHeight)) * maxTop;

    return { top, height, visible: true };
  };

  const updatePluginThumbDOM = () => {
    if (!pluginThumbRef.current || !pluginScrollElementRef.current) return;
    const thumb = calculatePluginThumb(pluginScrollElementRef.current);
    pluginThumbRef.current.style.top = `${thumb.top}px`;
    pluginThumbRef.current.style.height = `${thumb.height}px`;
    pluginThumbRef.current.style.display = thumb.visible ? 'block' : 'none';
  };

  const { scrollContainerRef: pluginLenisRef } = useLenis({
    onScroll: updatePluginThumbDOM,
  });

  const setPluginScrollRef = (node: HTMLDivElement | null) => {
    pluginScrollElementRef.current = node;
    pluginLenisRef(node);
  };

  const setPluginThumbRef = (node: HTMLDivElement | null) => {
    pluginThumbRef.current = node;
  };

  useEffect(() => {
    const hasPluginPanel =
      !!pluginSettingsPanel ||
      (selectedPluginElements.length > 0 &&
        selectedKeyLikeElements.length === 0 &&
        selectedGraphElements.length === 0);

    if (!hasPluginPanel) return;

    const raf = requestAnimationFrame(() => {
      updatePluginThumbDOM();
    });
    return () => cancelAnimationFrame(raf);
  });

  // 배치 편집용 로컬 ColorPicker 상태
  type BatchPickerTarget =
    'noteColor' | 'glowColor' | 'borderColor' | 'fill' | 'stroke' | null;
  const [batchPickerFor, setBatchPickerFor] = useState<BatchPickerTarget>(null);
  const closeTransientEditors = useCallback(() => {
    setShowImagePicker(false);
    setShowBatchImagePicker(false);
    setShowGraphImagePicker(false);
    setBatchPickerFor(null);
    setIsListening(false);
  }, []);
  const [batchCounterColorState, setBatchCounterColorState] = useState<
    'idle' | 'active'
  >('idle');

  const [batchLocalColors, setBatchLocalColors] = useState<{
    noteColor: NoteColor;
    glowColor: NoteColor;
    borderColor: string;
    borderOpacity: number;
    fillIdle: string;
    fillActive: string;
    strokeIdle: string;
    strokeActive: string;
  }>({
    noteColor: '#FFFFFF',
    glowColor: '#FFFFFF',
    borderColor: '#FFFFFF',
    borderOpacity: 100,
    fillIdle: '#FFFFFF',
    fillActive: '#FFFFFF',
    strokeIdle: '#000000',
    strokeActive: '#000000',
  });

  const [batchLocalOpacities, setBatchLocalOpacities] = useState<{
    noteOpacity: number;
    glowOpacity: number;
  }>({
    noteOpacity: 80,
    glowOpacity: 70,
  });

  // 선택이 변경되면 로컬 상태 초기화
  useEffect(() => {
    const targetPosition = singleKeyPosition || singleStatPosition;
    if (targetPosition) {
      setLocalState({
        dx: targetPosition.dx,
        dy: targetPosition.dy,
        width: targetPosition.width || 60,
        height: targetPosition.height || 60,
      });
    } else {
      setLocalState({});
    }
  }, [singleKeyPosition, singleStatPosition]);

  useEffect(() => {
    setGraphClassNameDraft(singleGraphPosition?.className || '');
  }, [selectedKeyType, singleGraphIndex, singleGraphPosition?.className]);

  useEffect(() => {
    if (pluginSettingsPanel) {
      setPluginPanelSettings(pluginSettingsPanel.settings || {});
    }
  }, [pluginSettingsPanel]);

  useEffect(() => {
    pluginSettingsHistoryRef.current = null;
    pluginTransformHistoryRef.current = null;
  }, [selectedPluginElement?.fullId]);

  // The sidebar follows the selection: choosing something on the canvas opens
  // its page, and clearing the selection returns to the list. Picking several
  // in the list itself stays on the list until they are opened.
  const previousPanelSelectionRef = useRef(selectionIdentity);
  useEffect(() => {
    if (previousPanelSelectionRef.current === selectionIdentity) return;
    previousPanelSelectionRef.current = selectionIdentity;

    const selectionStore = useGridSelectionStore.getState();
    const fromList =
      selectionFromLayerPanelRef.current || selectionStore._skipPanelModeSwitch;
    selectionFromLayerPanelRef.current = false;
    if (selectionStore._skipPanelModeSwitch) {
      selectionStore.setSkipPanelModeSwitch(false);
    }

    closeTransientEditors();
    // Another element opens on its own face, not on its ghost.
    setPartPath((path) => (path[0] === GHOST_PART ? path.slice(1) : path));

    if (selectedElements.length === 0) {
      if (view === 'element') {
        setDirection('back');
        setView('list');
      }
    } else if (!fromList && view !== 'element') {
      setDirection('forward');
      setView('element');
    }
  }, [selectionIdentity, selectedElements.length, view, closeTransientEditors]);

  // 언마운트 시 키보드 플래그 오염 방지
  useEffect(() => {
    return () => {
      useGridSelectionStore.getState().setSkipPanelModeSwitch(false);
    };
  }, []);

  useEffect(() => {
    if (pluginSettingsPanel) closeTransientEditors();
  }, [pluginSettingsPanel, closeTransientEditors]);

  // 키 리스닝 상태를 전역으로 노출
  useEffect(() => {
    if (listeningFlagTimerRef.current !== null) {
      clearTimeout(listeningFlagTimerRef.current);
      listeningFlagTimerRef.current = null;
    }

    if (isListening) {
      window.__dmn_isKeyListening = true;
    } else {
      listeningFlagTimerRef.current = setTimeout(() => {
        window.__dmn_isKeyListening = false;
        listeningFlagTimerRef.current = null;
      }, 150);
    }

    return () => {
      if (listeningFlagTimerRef.current !== null) {
        clearTimeout(listeningFlagTimerRef.current);
        listeningFlagTimerRef.current = null;
      }
    };
  }, [isListening]);

  // 컴포넌트 언마운트 시 반드시 플래그 해제
  useEffect(() => {
    return () => {
      window.__dmn_isKeyListening = false;
      if (listeningFlagTimerRef.current !== null) {
        clearTimeout(listeningFlagTimerRef.current);
        listeningFlagTimerRef.current = null;
      }
    };
  }, []);

  // 키 리스닝 중 브라우저 기본 동작 차단
  useEffect(() => {
    if (!isListening) return undefined;

    const blockKeyboardEvents = (e: KeyboardEvent) => {
      e.preventDefault();
      e.stopPropagation();
    };

    const blockMouseEvents = (e: MouseEvent) => {
      e.preventDefault();
      e.stopPropagation();
    };

    const blockContextMenu = (e: Event) => {
      e.preventDefault();
      e.stopPropagation();
    };

    window.addEventListener('keydown', blockKeyboardEvents, true);
    window.addEventListener('keyup', blockKeyboardEvents, true);
    window.addEventListener('keypress', blockKeyboardEvents, true);
    window.addEventListener('mousedown', blockMouseEvents, true);
    window.addEventListener('contextmenu', blockContextMenu, true);

    return () => {
      window.removeEventListener('keydown', blockKeyboardEvents, true);
      window.removeEventListener('keyup', blockKeyboardEvents, true);
      window.removeEventListener('keypress', blockKeyboardEvents, true);
      window.removeEventListener('mousedown', blockMouseEvents, true);
      window.removeEventListener('contextmenu', blockContextMenu, true);
    };
  }, [isListening]);

  // 키 리스닝 effect
  useEffect(() => {
    if (!isListening) return undefined;
    if (typeof window === 'undefined' || !window.api?.keys?.onRawInput) {
      return undefined;
    }

    const unsubscribe = window.api.keys.onRawInput(
      (payload: RawInputPayload) => {
        if (!payload || payload.state !== 'DOWN') return;
        const targetLabel =
          payload.label ||
          (Array.isArray(payload.labels) ? payload.labels[0] : null);
        if (!targetLabel) return;

        const info = getKeyInfoByGlobalKey(targetLabel);

        justAssignedRef.current = true;
        setTimeout(() => {
          justAssignedRef.current = false;
        }, 100);

        setIsListening(false);

        if (singleKeyIndex === null) return;
        if (listenTarget === 'ghost') {
          writeGhost(singleKeyIndex, (ghost) => ({
            ...ghost,
            key: info.globalKey,
          }));
        } else if (onKeyMappingChange) {
          onKeyMappingChange(singleKeyIndex, info.globalKey);
        }
      },
    );

    return () => {
      try {
        unsubscribe?.();
      } catch (error) {
        console.error('Failed to unsubscribe raw input listener', error);
      }
    };
    // writeGhost reads the latest store state on every call.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isListening, listenTarget, singleKeyIndex, onKeyMappingChange]);

  // ============================================================================
  // 핸들러
  // ============================================================================

  /** A row opens its page; a back button returns to the page before it. */
  const openPath = (path: string[], back: boolean) => {
    closeTransientEditors();
    setDirection(back ? 'back' : 'forward');
    setPartPath(path);
  };

  /**
   * A page opened from inside a page (a pose, from the list of poses) returns
   * to it; naming a page that is already open goes back to it.
   */
  const openPartFromPart = (part: string) => {
    const at = partPath.indexOf(part);
    openPath(
      at >= 0 ? partPath.slice(0, at + 1) : [...partPath, part],
      at >= 0,
    );
  };

  /** Opens the page of what is selected. */
  const openSelection = () => {
    if (useGridSelectionStore.getState().selectedElements.length === 0) return;
    closeTransientEditors();
    setDirection('forward');
    setView('element');
  };

  /** Returns to the element list; the canvas no longer has a selection. */
  const backToList = () => {
    closeTransientEditors();
    useGridSelectionStore.getState().clearSelection();
    setDirection('back');
    setView('list');
  };

  // The panels below still take these; their own header that used them is
  // replaced by the sidebar's page head.
  const handleTogglePanel = () => {};
  const handleToggleMode = () => {};

  const pluginDefaultSettings = (() => {
    const defaults: Record<string, unknown> = {};
    if (selectedPluginDefinition?.settings) {
      Object.entries(selectedPluginDefinition.settings).forEach(
        ([key, schema]) => {
          const schemaValue = schema as PluginSettingSchema;
          if (schemaValue.type === 'divider' || schemaValue.type === 'section')
            return;
          defaults[key] = schemaValue.default;
        },
      );
    }
    return defaults;
  })();

  const resolvedPluginSettings = {
    ...pluginDefaultSettings,
    ...(selectedPluginElement?.settings || {}),
  };

  const ensurePluginSettingsHistory = () => {
    if (!selectedPluginElement) return;
    if (pluginSettingsHistoryRef.current === selectedPluginElement.fullId) {
      return;
    }
    pushHistoryState({
      keyMappings,
      positions,
      statPositions: statItemPositions,
      graphPositions: graphItemPositions,
      pluginElements,
    });
    pluginSettingsHistoryRef.current = selectedPluginElement.fullId;
  };

  const ensurePluginTransformHistory = () => {
    if (!selectedPluginElement) return;
    if (pluginTransformHistoryRef.current === selectedPluginElement.fullId) {
      return;
    }
    pushHistoryState({
      keyMappings,
      positions,
      statPositions: statItemPositions,
      graphPositions: graphItemPositions,
      pluginElements,
    });
    pluginTransformHistoryRef.current = selectedPluginElement.fullId;
  };

  const handlePluginPositionXChange = (value: number) => {
    if (!selectedPluginElement) return;
    ensurePluginTransformHistory();
    updatePluginElement(selectedPluginElement.fullId, {
      position: {
        x: value,
        y: selectedPluginElement.position.y,
      },
    });
  };

  const handlePluginPositionYChange = (value: number) => {
    if (!selectedPluginElement) return;
    ensurePluginTransformHistory();
    updatePluginElement(selectedPluginElement.fullId, {
      position: {
        x: selectedPluginElement.position.x,
        y: value,
      },
    });
  };

  const handlePluginWidthChange = (value: number) => {
    if (!selectedPluginElement) return;
    ensurePluginTransformHistory();
    const baseHeight =
      selectedPluginElement.measuredSize?.height ??
      selectedPluginElement.estimatedSize?.height ??
      150;
    updatePluginElement(selectedPluginElement.fullId, {
      measuredSize: {
        width: value,
        height: baseHeight,
      },
    });
  };

  const handlePluginHeightChange = (value: number) => {
    if (!selectedPluginElement) return;
    ensurePluginTransformHistory();
    const baseWidth =
      selectedPluginElement.measuredSize?.width ??
      selectedPluginElement.estimatedSize?.width ??
      200;
    updatePluginElement(selectedPluginElement.fullId, {
      measuredSize: {
        width: baseWidth,
        height: value,
      },
    });
  };

  const handlePluginSettingChange = (key: string, value: unknown) => {
    if (!selectedPluginElement) return;
    ensurePluginSettingsHistory();
    updatePluginElement(selectedPluginElement.fullId, {
      settings: {
        ...resolvedPluginSettings,
        [key]: value,
      },
    });
  };

  const handlePluginSettingsPanelChange = (key: string, value: unknown) => {
    if (!pluginSettingsPanel) return;
    setPluginPanelSettings((prev) => {
      const next = { ...prev, [key]: value };
      pluginSettingsPanel.onChange(next);
      return next;
    });
  };

  const handlePluginSettingsPanelConfirm = async () => {
    const panel = pluginSettingsPanel;
    if (!panel) return;
    try {
      await panel.onConfirm(pluginPanelSettings, panel.originalSettings);
      if (usePropertiesPanelStore.getState().pluginSettingsPanel === panel) {
        panel.resolve(true);
      }
    } catch (error) {
      console.error('[Plugin Settings] Failed to apply settings:', error);
      if (usePropertiesPanelStore.getState().pluginSettingsPanel === panel) {
        panel.resolve(false);
      }
    } finally {
      if (usePropertiesPanelStore.getState().pluginSettingsPanel === panel) {
        closePluginSettingsPanel();
        closeTransientEditors();
      }
    }
  };

  const handlePluginSettingsPanelCancelImpl = useRef<() => void>(() => {});
  handlePluginSettingsPanelCancelImpl.current = () => {
    const panel = usePropertiesPanelStore.getState().pluginSettingsPanel;
    if (!panel) return;
    closePluginSettingsPanel();
    closeTransientEditors();
    try {
      panel.onCancel(panel.originalSettings);
    } catch (error) {
      console.error('[Plugin Settings] Failed to cancel settings:', error);
    } finally {
      panel.resolve(false);
    }
  };
  const handlePluginSettingsPanelCancel = () => {
    handlePluginSettingsPanelCancelImpl.current();
  };

  const handleKeyListen = () => {
    if (justAssignedRef.current) return;
    setListenTarget('default');
    setIsListening(true);
  };

  const handleGhostKeyListen = () => {
    if (justAssignedRef.current) return;
    setListenTarget('ghost');
    setIsListening(true);
  };

  const handleGhostUpdate = (
    data: Partial<KeyPosition> & { index: number },
  ) => {
    const { index, ghost: _ghost, ...updates } = data;
    const owner = readKey(index);
    if (!owner) return;
    onKeyUpdate({ index, ghost: mergeGhostSettings(owner, updates) });
  };

  const handleGhostPreview = (index: number, updates: Partial<KeyPosition>) => {
    const owner = readKey(index);
    if (!owner || !onKeyPreview) return;
    const { ghost: _ghost, ...settings } = updates;
    onKeyPreview(index, { ghost: mergeGhostSettings(owner, settings) });
  };

  const handleGhostEnabledChange = (index: number, enabled: boolean) => {
    writeGhost(index, (ghost) => ({ ...ghost, enabled }));
    if (enabled && !readKey(index)?.ghost?.key) handleGhostKeyListen();
    if (!enabled && listenTarget === 'ghost') setIsListening(false);
  };

  const handleStatUpdate = (
    data: Partial<StatItemPosition> & { index: number },
  ) => {
    const { index, ...updates } = data;
    const mode = selectedKeyType;
    const current = useStatItemStore.getState().positions;
    const list = current[mode] || [];
    if (!list[index]) return;

    if (!statPreviewHistorySavedRef.current) {
      const currentPositions = useKeyStore.getState().positions;
      const currentPluginElements =
        usePluginDisplayElementStore.getState().elements;
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: currentPositions,
        statPositions: current,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: currentPluginElements,
      });
    }
    statPreviewHistorySavedRef.current = false;

    const nextList = list.map((pos, i) =>
      i === index ? ({ ...pos, ...updates } as StatItemPosition) : pos,
    );
    const nextPositions = { ...current, [mode]: nextList };

    useStatItemStore.getState().setLocalUpdateInProgress(true);
    useStatItemStore.getState().setPositions(nextPositions);
    window.api.statItems
      .updatePositions(nextPositions)
      .catch((error) => {
        console.error('Failed to update stat item', error);
      })
      .finally(() => {
        useStatItemStore.getState().setLocalUpdateInProgress(false);
      });
    try {
      window.api.bridge.sendTo('overlay', 'statPositions:sync', {
        positions: nextPositions,
      });
    } catch {
      // ignore
    }
  };

  const handleStatPreview = (
    index: number,
    updates: Partial<StatItemPosition>,
  ) => {
    const mode = selectedKeyType;
    const current = useStatItemStore.getState().positions;
    const list = current[mode] || [];
    if (!list[index]) return;

    // preview가 store를 변경하므로, 첫 preview 시 히스토리 저장
    if (!statPreviewHistorySavedRef.current) {
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: useKeyStore.getState().positions,
        statPositions: current,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: usePluginDisplayElementStore.getState().elements,
      });
      statPreviewHistorySavedRef.current = true;
    }

    const nextList = list.map((pos, i) =>
      i === index ? ({ ...pos, ...updates } as StatItemPosition) : pos,
    );
    const nextPositions = { ...current, [mode]: nextList };
    useStatItemStore.getState().setPositions(nextPositions);
  };

  const handleStatBatchPreview = (
    updates: Array<{ index: number } & Partial<StatItemPosition>>,
  ) => {
    if (updates.length === 0) return;

    const mode = selectedKeyType;
    const current = useStatItemStore.getState().positions;
    const list = current[mode] || [];
    if (list.length === 0) return;

    const updateMap = new Map<number, Partial<StatItemPosition>>();
    for (const { index, ...rest } of updates) {
      if (list[index]) {
        updateMap.set(index, rest);
      }
    }
    if (updateMap.size === 0) return;

    // preview가 store를 변경하므로, 첫 preview 시 히스토리 저장
    if (!statPreviewHistorySavedRef.current) {
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: useKeyStore.getState().positions,
        statPositions: current,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: usePluginDisplayElementStore.getState().elements,
      });
      statPreviewHistorySavedRef.current = true;
    }

    const nextList = list.map((pos, i) => {
      const update = updateMap.get(i);
      return update ? ({ ...pos, ...update } as StatItemPosition) : pos;
    });
    const nextPositions = { ...current, [mode]: nextList };
    useStatItemStore.getState().setPositions(nextPositions);
  };

  const handleStatBatchUpdate = (
    updates: Array<{ index: number } & Partial<StatItemPosition>>,
    options?: { skipHistory?: boolean },
  ) => {
    if (updates.length === 0) return;

    const mode = selectedKeyType;
    const current = useStatItemStore.getState().positions;
    const list = current[mode] || [];
    if (list.length === 0) return;

    const updateMap = new Map<number, Partial<StatItemPosition>>();
    for (const { index, ...rest } of updates) {
      if (list[index]) {
        updateMap.set(index, rest);
      }
    }
    if (updateMap.size === 0) return;

    if (!options?.skipHistory && !statPreviewHistorySavedRef.current) {
      const currentPositions = useKeyStore.getState().positions;
      const currentPluginElements =
        usePluginDisplayElementStore.getState().elements;
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: currentPositions,
        statPositions: current,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: currentPluginElements,
      });
    }
    statPreviewHistorySavedRef.current = false;

    const nextList = list.map((pos, i) => {
      const update = updateMap.get(i);
      return update ? ({ ...pos, ...update } as StatItemPosition) : pos;
    });
    const nextPositions = { ...current, [mode]: nextList };

    useStatItemStore.getState().setLocalUpdateInProgress(true);
    useStatItemStore.getState().setPositions(nextPositions);
    window.api.statItems
      .updatePositions(nextPositions)
      .catch((error) => {
        console.error('Failed to batch update stat items', error);
      })
      .finally(() => {
        useStatItemStore.getState().setLocalUpdateInProgress(false);
      });
    try {
      window.api.bridge.sendTo('overlay', 'statPositions:sync', {
        positions: nextPositions,
      });
    } catch {
      // ignore
    }
  };

  const handleGraphUpdate = (
    data: Partial<GraphItemPosition> & { index: number },
  ) => {
    const { index, ...updates } = data;
    const mode = selectedKeyType;
    const current = useGraphItemStore.getState().positions;
    const list = current[mode] || [];
    if (!list[index]) return;

    if (!graphPreviewHistorySavedRef.current) {
      const currentPositions = useKeyStore.getState().positions;
      const currentPluginElements =
        usePluginDisplayElementStore.getState().elements;
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: currentPositions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: current,
        pluginElements: currentPluginElements,
      });
    }
    graphPreviewHistorySavedRef.current = false;

    const nextList = list.map((pos, i) =>
      i === index ? ({ ...pos, ...updates } as GraphItemPosition) : pos,
    );
    const nextPositions = { ...current, [mode]: nextList };

    useGraphItemStore.getState().setLocalUpdateInProgress(true);
    useGraphItemStore.getState().setPositions(nextPositions);
    window.api.graphItems
      .updatePositions(nextPositions)
      .catch((error) => {
        console.error('Failed to update graph item', error);
      })
      .finally(() => {
        useGraphItemStore.getState().setLocalUpdateInProgress(false);
      });
    try {
      window.api.bridge.sendTo('overlay', 'graphPositions:sync', {
        positions: nextPositions,
      });
    } catch {
      // ignore
    }
  };

  const handleKnobUpdate = (
    data: Partial<KnobItemPosition> & { index: number },
  ) => {
    const { index, ...updates } = data;
    const mode = selectedKeyType;
    const current = useKnobItemStore.getState().positions;
    const list = current[mode] || [];
    if (!list[index]) return;

    if (!knobPreviewHistorySavedRef.current) {
      const currentPositions = useKeyStore.getState().positions;
      const currentPluginElements =
        usePluginDisplayElementStore.getState().elements;
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: currentPositions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: currentPluginElements,
      });
    }
    knobPreviewHistorySavedRef.current = false;

    const nextList = list.map((pos, i) =>
      i === index ? ({ ...pos, ...updates } as KnobItemPosition) : pos,
    );
    const nextPositions = { ...current, [mode]: nextList };

    useKnobItemStore.getState().setLocalUpdateInProgress(true);
    useKnobItemStore.getState().setPositions(nextPositions);
    window.api.knobItems
      .updatePositions(nextPositions)
      .catch((error) => {
        console.error('Failed to update knob item', error);
      })
      .finally(() => {
        useKnobItemStore.getState().setLocalUpdateInProgress(false);
      });
    try {
      window.api.bridge.sendTo('overlay', 'knobPositions:sync', {
        positions: nextPositions,
      });
    } catch {
      // ignore
    }
  };

  const handleKnobPreview = (
    index: number,
    updates: Partial<KnobItemPosition>,
  ) => {
    const mode = selectedKeyType;
    const current = useKnobItemStore.getState().positions;
    const list = current[mode] || [];
    if (!list[index]) return;

    if (!knobPreviewHistorySavedRef.current) {
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: useKeyStore.getState().positions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: usePluginDisplayElementStore.getState().elements,
      });
      knobPreviewHistorySavedRef.current = true;
    }

    const nextList = list.map((pos, i) =>
      i === index ? ({ ...pos, ...updates } as KnobItemPosition) : pos,
    );
    const nextPositions = { ...current, [mode]: nextList };
    useKnobItemStore.getState().setPositions(nextPositions);
  };

  const handleKnobBatchPreview = (
    updates: Array<{ index: number } & Partial<KnobItemPosition>>,
  ) => {
    if (updates.length === 0) return;

    const mode = selectedKeyType;
    const current = useKnobItemStore.getState().positions;
    const list = current[mode] || [];
    if (list.length === 0) return;

    const updateMap = new Map<number, Partial<KnobItemPosition>>();
    for (const { index, ...rest } of updates) {
      if (list[index]) {
        updateMap.set(index, rest);
      }
    }
    if (updateMap.size === 0) return;

    if (!knobPreviewHistorySavedRef.current) {
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: useKeyStore.getState().positions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: usePluginDisplayElementStore.getState().elements,
      });
      knobPreviewHistorySavedRef.current = true;
    }

    const nextList = list.map((pos, i) => {
      const update = updateMap.get(i);
      return update ? ({ ...pos, ...update } as KnobItemPosition) : pos;
    });
    const nextPositions = { ...current, [mode]: nextList };
    useKnobItemStore.getState().setPositions(nextPositions);
  };

  const handleKnobBatchUpdate = (
    updates: Array<{ index: number } & Partial<KnobItemPosition>>,
    options?: { skipHistory?: boolean },
  ) => {
    if (updates.length === 0) return;

    const mode = selectedKeyType;
    const current = useKnobItemStore.getState().positions;
    const list = current[mode] || [];
    if (list.length === 0) return;

    const updateMap = new Map<number, Partial<KnobItemPosition>>();
    for (const { index, ...rest } of updates) {
      if (list[index]) {
        updateMap.set(index, rest);
      }
    }
    if (updateMap.size === 0) return;

    if (!options?.skipHistory && !knobPreviewHistorySavedRef.current) {
      const currentPositions = useKeyStore.getState().positions;
      const currentPluginElements =
        usePluginDisplayElementStore.getState().elements;
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: currentPositions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: useGraphItemStore.getState().positions,
        pluginElements: currentPluginElements,
      });
    }
    knobPreviewHistorySavedRef.current = false;

    const nextList = list.map((pos, i) => {
      const update = updateMap.get(i);
      return update ? ({ ...pos, ...update } as KnobItemPosition) : pos;
    });
    const nextPositions = { ...current, [mode]: nextList };

    useKnobItemStore.getState().setLocalUpdateInProgress(true);
    useKnobItemStore.getState().setPositions(nextPositions);
    window.api.knobItems
      .updatePositions(nextPositions)
      .catch((error) => {
        console.error('Failed to batch update knob items', error);
      })
      .finally(() => {
        useKnobItemStore.getState().setLocalUpdateInProgress(false);
      });
    try {
      window.api.bridge.sendTo('overlay', 'knobPositions:sync', {
        positions: nextPositions,
      });
    } catch {
      // ignore
    }
  };

  const handleGraphPreview = (
    index: number,
    updates: Partial<GraphItemPosition>,
  ) => {
    const mode = selectedKeyType;
    const current = useGraphItemStore.getState().positions;
    const list = current[mode] || [];
    if (!list[index]) return;

    // preview가 store를 변경하므로, 첫 preview 시 히스토리 저장
    if (!graphPreviewHistorySavedRef.current) {
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: useKeyStore.getState().positions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: current,
        pluginElements: usePluginDisplayElementStore.getState().elements,
      });
      graphPreviewHistorySavedRef.current = true;
    }

    const nextList = list.map((pos, i) =>
      i === index ? ({ ...pos, ...updates } as GraphItemPosition) : pos,
    );
    const nextPositions = { ...current, [mode]: nextList };
    useGraphItemStore.getState().setPositions(nextPositions);
  };

  const handleGraphBatchPreview = (
    updates: Array<{ index: number } & Partial<GraphItemPosition>>,
  ) => {
    if (updates.length === 0) return;

    const mode = selectedKeyType;
    const current = useGraphItemStore.getState().positions;
    const list = current[mode] || [];
    if (list.length === 0) return;

    const updateMap = new Map<number, Partial<GraphItemPosition>>();
    for (const { index, ...rest } of updates) {
      if (list[index]) {
        updateMap.set(index, rest);
      }
    }
    if (updateMap.size === 0) return;

    // preview가 store를 변경하므로, 첫 preview 시 히스토리 저장
    if (!graphPreviewHistorySavedRef.current) {
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: useKeyStore.getState().positions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: current,
        pluginElements: usePluginDisplayElementStore.getState().elements,
      });
      graphPreviewHistorySavedRef.current = true;
    }

    const nextList = list.map((pos, i) => {
      const update = updateMap.get(i);
      return update ? ({ ...pos, ...update } as GraphItemPosition) : pos;
    });
    const nextPositions = { ...current, [mode]: nextList };
    useGraphItemStore.getState().setPositions(nextPositions);
  };

  const handleGraphBatchUpdate = (
    updates: Array<{ index: number } & Partial<GraphItemPosition>>,
    options?: { skipHistory?: boolean },
  ) => {
    if (updates.length === 0) return;

    const mode = selectedKeyType;
    const current = useGraphItemStore.getState().positions;
    const list = current[mode] || [];
    if (list.length === 0) return;

    const updateMap = new Map<number, Partial<GraphItemPosition>>();
    for (const { index, ...rest } of updates) {
      if (list[index]) {
        updateMap.set(index, rest);
      }
    }
    if (updateMap.size === 0) return;

    if (!options?.skipHistory && !graphPreviewHistorySavedRef.current) {
      const currentPositions = useKeyStore.getState().positions;
      const currentPluginElements =
        usePluginDisplayElementStore.getState().elements;
      const { keyMappings: km } = useKeyStore.getState();
      pushHistoryState({
        keyMappings: km,
        positions: currentPositions,
        statPositions: useStatItemStore.getState().positions,
        graphPositions: current,
        pluginElements: currentPluginElements,
      });
    }
    graphPreviewHistorySavedRef.current = false;

    const nextList = list.map((pos, i) => {
      const update = updateMap.get(i);
      return update ? ({ ...pos, ...update } as GraphItemPosition) : pos;
    });
    const nextPositions = { ...current, [mode]: nextList };

    useGraphItemStore.getState().setLocalUpdateInProgress(true);
    useGraphItemStore.getState().setPositions(nextPositions);
    window.api.graphItems
      .updatePositions(nextPositions)
      .catch((error) => {
        console.error('Failed to batch update graph items', error);
      })
      .finally(() => {
        useGraphItemStore.getState().setLocalUpdateInProgress(false);
      });
    try {
      window.api.bridge.sendTo('overlay', 'graphPositions:sync', {
        positions: nextPositions,
      });
    } catch {
      // ignore
    }
  };

  // 크기 변경 완료 (blur 시 저장)
  const handleSizeBlur = () => {
    if (singleKeyIndex === null && singleStatIndex === null) return;
    const updates: Partial<KeyPosition> = {};
    if (localState.width !== undefined) updates.width = localState.width;
    if (localState.height !== undefined) updates.height = localState.height;
    if (Object.keys(updates).length > 0) {
      if (singleKeyIndex !== null) {
        onKeyUpdate({ index: singleKeyIndex, ...updates });
      } else if (singleStatIndex !== null) {
        handleStatUpdate({
          index: singleStatIndex,
          ...(updates as Partial<StatItemPosition>),
        });
      }
    }
  };

  // ============================================================================
  // 다중 선택 헬퍼 함수들
  // ============================================================================

  const getSelectedKeysData = () => {
    return selectedKeyLikeElements
      .map((el) => {
        const index = el.index!;
        if (el.type === 'key') {
          const position = positions[selectedKeyType]?.[index];
          const keyCode = keyMappings[selectedKeyType]?.[index] ?? null;
          const keyInfo = keyCode ? getKeyInfoByGlobalKey(keyCode) : null;
          return { index, position, keyCode, keyInfo };
        }
        const position = statItemPositions[selectedKeyType]?.[index];
        const statLabel =
          (position?.displayText || '').trim() ||
          getStatTypeLabel(position?.statType ?? null);
        const keyInfo = { globalKey: statLabel, displayName: statLabel };
        return { index, position, keyCode: null, keyInfo };
      })
      .filter((data) => data.position !== undefined);
  };

  const getSelectedGraphsData = () => {
    return selectedGraphElements
      .map((el) => {
        const index = el.index!;
        const position = graphItemPositions[selectedKeyType]?.[index];
        const graphLabel = `${getStatTypeLabel(
          position?.statType ?? null,
        )} Graph`;
        const keyInfo = { globalKey: graphLabel, displayName: graphLabel };
        return { index, position, keyCode: null, keyInfo };
      })
      .filter((data) => data.position !== undefined);
  };

  const getSelectedKnobsData = () => {
    return selectedKnobElements
      .map((el) => {
        const index = el.index!;
        const position = knobItemPositions[selectedKeyType]?.[index];
        const knobLabel = (position?.displayText || '').trim() || 'Knob';
        const keyInfo = { globalKey: knobLabel, displayName: knobLabel };
        return { index, position, keyCode: null, keyInfo };
      })
      .filter((data) => data.position !== undefined);
  };

  const getSelectedBatchStyleData = () => {
    return selectedBatchStyleElements
      .map((el) => {
        const index = el.index!;
        if (el.type === 'key') {
          const position = positions[selectedKeyType]?.[index];
          const keyCode = keyMappings[selectedKeyType]?.[index] ?? null;
          const keyInfo = keyCode ? getKeyInfoByGlobalKey(keyCode) : null;
          return { index, position, keyCode, keyInfo };
        }
        if (el.type === 'stat') {
          const position = statItemPositions[selectedKeyType]?.[index];
          const statLabel =
            (position?.displayText || '').trim() ||
            getStatTypeLabel(position?.statType ?? null);
          const keyInfo = { globalKey: statLabel, displayName: statLabel };
          return { index, position, keyCode: null, keyInfo };
        }
        if (el.type === 'knob') {
          const position = knobItemPositions[selectedKeyType]?.[index];
          const knobLabel = (position?.displayText || '').trim() || 'Knob';
          const keyInfo = { globalKey: knobLabel, displayName: knobLabel };
          return { index, position, keyCode: null, keyInfo };
        }
        const position = graphItemPositions[selectedKeyType]?.[index];
        const graphLabel = `${getStatTypeLabel(
          position?.statType ?? null,
        )} Graph`;
        const keyInfo = { globalKey: graphLabel, displayName: graphLabel };
        return { index, position, keyCode: null, keyInfo };
      })
      .filter((data) => data.position !== undefined);
  };

  const getMixedValue = <T,>(
    getter: (pos: KeyPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    const keysData = getSelectedKeysData();
    if (keysData.length === 0) return { isMixed: false, value: defaultValue };

    const firstValue = getter(keysData[0].position!) ?? defaultValue;
    const isMixed = keysData.some((data) => {
      const val = getter(data.position!) ?? defaultValue;
      if (typeof val === 'object' && typeof firstValue === 'object') {
        return JSON.stringify(val) !== JSON.stringify(firstValue);
      }
      return val !== firstValue;
    });

    return { isMixed, value: firstValue };
  };

  const getMixedValueGraphs = <T,>(
    getter: (pos: GraphItemPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    const graphsData = getSelectedGraphsData();
    if (graphsData.length === 0) return { isMixed: false, value: defaultValue };

    const firstValue = getter(graphsData[0].position!) ?? defaultValue;
    const isMixed = graphsData.some((data) => {
      const val = getter(data.position!) ?? defaultValue;
      if (typeof val === 'object' && typeof firstValue === 'object') {
        return JSON.stringify(val) !== JSON.stringify(firstValue);
      }
      return val !== firstValue;
    });

    return { isMixed, value: firstValue };
  };

  const getMixedValueGraphsAsKey = <T,>(
    getter: (pos: KeyPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    return getMixedValueGraphs((pos) => getter(pos), defaultValue);
  };

  const getMixedValueKnobs = <T,>(
    getter: (pos: KnobItemPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    const knobsData = getSelectedKnobsData();
    if (knobsData.length === 0) return { isMixed: false, value: defaultValue };

    const firstValue = getter(knobsData[0].position!) ?? defaultValue;
    const isMixed = knobsData.some((data) => {
      const val = getter(data.position!) ?? defaultValue;
      if (typeof val === 'object' && typeof firstValue === 'object') {
        return JSON.stringify(val) !== JSON.stringify(firstValue);
      }
      return val !== firstValue;
    });

    return { isMixed, value: firstValue };
  };

  const getMixedValueKnobsAsKey = <T,>(
    getter: (pos: KeyPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    return getMixedValueKnobs((pos) => getter(pos), defaultValue);
  };

  const getMixedValueBatch = <T,>(
    getter: (pos: KeyPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    const batchData = getSelectedBatchStyleData();
    if (batchData.length === 0) return { isMixed: false, value: defaultValue };

    const firstValue =
      getter(batchData[0].position as KeyPosition) ?? defaultValue;
    const isMixed = batchData.some((data) => {
      const val = getter(data.position as KeyPosition) ?? defaultValue;
      if (typeof val === 'object' && typeof firstValue === 'object') {
        return JSON.stringify(val) !== JSON.stringify(firstValue);
      }
      return val !== firstValue;
    });

    return { isMixed, value: firstValue };
  };

  // ============================================================================
  // 다중 선택 일괄 편집 핸들러 (훅 사용)
  // ============================================================================

  const {
    handleBatchStyleChange,
    handleBatchStyleChangeComplete,
    handleKeyOnlyStyleChangeComplete,
    handleBatchAlign,
    handleBatchDistribute,
    handleBatchSpacing,
    handleBatchSpacingPreview,
    handleBatchSpacingCommit,
    getBatchSpacingValue,
    handleBatchResize,
    handleBatchCounterUpdate,
    handleBatchNoteColorChange,
    handleBatchNoteColorChangeComplete,
    handleBatchGlowColorChange,
    handleBatchGlowColorChangeComplete,
  } = useBatchHandlers({
    selectedKeyLikeElements: selectedBatchStyleElements as {
      type: 'key' | 'stat' | 'graph' | 'knob';
      id: string;
      index?: number;
    }[],
    keyPositions: positions,
    statPositions: statItemPositions,
    graphPositions: graphItemPositions,
    selectedKeyType,
    onKeyUpdate,
    onKeyBatchUpdate,
    onKeyPreview,
    onKeyBatchPreview,
    onStatUpdate: handleStatUpdate,
    onStatBatchUpdate: handleStatBatchUpdate,
    onStatPreview: handleStatPreview,
    onStatBatchPreview: handleStatBatchPreview,
    onGraphUpdate: handleGraphUpdate,
    onGraphBatchUpdate: handleGraphBatchUpdate,
    onGraphPreview: handleGraphPreview,
    onGraphBatchPreview: handleGraphBatchPreview,
    knobPositions: knobItemPositions,
    onKnobUpdate: handleKnobUpdate,
    onKnobBatchUpdate: handleKnobBatchUpdate,
    onKnobPreview: handleKnobPreview,
    onKnobBatchPreview: handleKnobBatchPreview,
  });

  // NOTE 탭은 "키"에만 적용되어야 함
  const getSelectedKeyOnlyPositions = () => {
    return selectedKeyElements
      .map((el) => {
        const index = el.index ?? -1;
        const position = positions[selectedKeyType]?.[index];
        return position ? { index, position } : null;
      })
      .filter((v): v is { index: number; position: KeyPosition } => v !== null);
  };

  const getMixedValueKeysOnly = <T,>(
    getter: (pos: KeyPosition) => T | undefined,
    defaultValue: T,
  ): { isMixed: boolean; value: T } => {
    const data = getSelectedKeyOnlyPositions();
    if (data.length === 0) return { isMixed: false, value: defaultValue };

    const firstValue = getter(data[0].position) ?? defaultValue;
    const isMixed = data.some(({ position }) => {
      const val = getter(position) ?? defaultValue;
      if (typeof val === 'object' && typeof firstValue === 'object') {
        return JSON.stringify(val) !== JSON.stringify(firstValue);
      }
      return val !== firstValue;
    });

    return { isMixed, value: firstValue };
  };

  const dispatchKeyOnlyBatchUpdates = (
    updates: Array<{ index: number } & Partial<KeyPosition>>,
    kind: 'preview' | 'commit',
  ) => {
    if (updates.length === 0) return;
    if (kind === 'preview') {
      if (onKeyBatchPreview) {
        onKeyBatchPreview(updates);
        return;
      }
      if (onKeyPreview) {
        updates.forEach(({ index, ...rest }) => onKeyPreview(index, rest));
        return;
      }
      return;
    }

    if (onKeyBatchUpdate) {
      onKeyBatchUpdate(updates);
      return;
    }
    updates.forEach((update) => onKeyUpdate(update));
  };

  const handleBatchKeyOnlyStyleChangeComplete = (
    property: keyof KeyPosition,
    value: KeyPosition[keyof KeyPosition],
  ) => {
    const updates = getSelectedKeyOnlyPositions().map(({ index }) => ({
      index,
      [property]: value,
    })) as Array<{ index: number } & Partial<KeyPosition>>;
    dispatchKeyOnlyBatchUpdates(updates, 'commit');
  };

  const handleBatchKeyOnlyStyleChange = (
    property: keyof KeyPosition,
    value: KeyPosition[keyof KeyPosition],
  ) => {
    const updates = getSelectedKeyOnlyPositions().map(({ index }) => ({
      index,
      [property]: value,
    })) as Array<{ index: number } & Partial<KeyPosition>>;
    dispatchKeyOnlyBatchUpdates(updates, 'preview');
  };

  const handleBatchNoteColorChangeKeysOnly = (value: NoteColor) => {
    const updates = getSelectedKeyOnlyPositions().map(({ index }) => ({
      index,
      noteColor: value,
    }));
    dispatchKeyOnlyBatchUpdates(updates, 'preview');
  };

  const handleBatchNoteColorChangeCompleteKeysOnly = (value: NoteColor) => {
    const updates = getSelectedKeyOnlyPositions().map(({ index }) => ({
      index,
      noteColor: value,
    }));
    dispatchKeyOnlyBatchUpdates(updates, 'commit');
  };

  const handleBatchGlowColorChangeKeysOnly = (value: NoteColor) => {
    const updates = getSelectedKeyOnlyPositions().map(({ index }) => ({
      index,
      noteGlowColor: value,
    }));
    dispatchKeyOnlyBatchUpdates(updates, 'preview');
  };

  const handleBatchGlowColorChangeCompleteKeysOnly = (value: NoteColor) => {
    const updates = getSelectedKeyOnlyPositions().map(({ index }) => ({
      index,
      noteGlowColor: value,
    }));
    dispatchKeyOnlyBatchUpdates(updates, 'commit');
  };

  const handleGraphBatchSharedSetting = (
    updates: Partial<GraphItemPosition>,
  ) => {
    const batchUpdates = selectedGraphElements
      .filter((el) => el.index !== undefined)
      .map((el) => ({ index: el.index!, ...updates })) as Array<
      { index: number } & Partial<GraphItemPosition>
    >;
    handleGraphBatchUpdate(batchUpdates);
  };

  const handleKnobBatchSharedSetting = (updates: Partial<KnobItemPosition>) => {
    const batchUpdates = selectedKnobElements
      .filter((el) => el.index !== undefined)
      .map((el) => ({ index: el.index!, ...updates })) as Array<
      { index: number } & Partial<KnobItemPosition>
    >;
    handleKnobBatchUpdate(batchUpdates);
  };

  const renderPluginSettingsForm = (
    schema: Record<string, PluginSettingSchema> | undefined,
    values: Record<string, unknown>,
    messages: PluginMessages | undefined,
    colorIdPrefix: string,
    onChange: (key: string, value: unknown) => void,
    options?: { wrap?: boolean },
  ) => {
    if (!schema || Object.keys(schema).length === 0) {
      return (
        <p className="text-[#6B6D75] text-style-4 text-center">
          {t('propertiesPanel.pluginNoSettings') || '설정할 항목이 없습니다.'}
        </p>
      );
    }

    const translate = (key?: string, fallback?: string) => {
      if (!key) return fallback || '';
      return translatePluginMessage({
        messages,
        locale,
        key,
        fallback,
      });
    };

    const getPluginInputWidth = (
      type: 'string' | 'number',
      value: unknown,
    ): string => {
      if (type === 'number') {
        return '60px';
      }
      const strVal = String(value ?? '');
      if (strVal.length <= 4) return '60px';
      if (strVal.length <= 10) return '100px';
      return '200px';
    };

    const wrap = options?.wrap !== false;
    const rows = Object.entries(schema).map(([key, setting]) => {
      const schemaValue = setting as PluginSettingSchema;

      if (schemaValue.visible !== undefined) {
        const vis =
          typeof schemaValue.visible === 'function'
            ? schemaValue.visible(values)
            : schemaValue.visible;
        if (!vis) return null;
      }

      if (schemaValue.type === 'divider' || schemaValue.type === 'section') {
        return (
          <div key={`section-${key}`} className="w-full">
            {schemaValue.type === 'section' && schemaValue.label ? (
              <div className="mb-2 text-[12px] font-semibold">
                {translate(schemaValue.label, schemaValue.label)}
              </div>
            ) : null}
            <SectionDivider />
          </div>
        );
      }
      const rawValue =
        values[key] !== undefined ? values[key] : schemaValue.default;
      const labelText = translate(schemaValue.label, schemaValue.label);
      const placeholderText =
        typeof schemaValue.placeholder === 'string'
          ? translate(schemaValue.placeholder, schemaValue.placeholder)
          : schemaValue.placeholder;

      let control: React.ReactNode = null;

      if (schemaValue.type === 'boolean') {
        const checked = !!rawValue;
        control = (
          <Checkbox
            checked={checked}
            onChange={() => onChange(key, !checked)}
          />
        );
      } else if (schemaValue.type === 'color') {
        const colorValue =
          typeof rawValue === 'string'
            ? rawValue
            : (schemaValue.default as string) || '#FFFFFF';
        control = (
          <ColorInput
            value={colorValue}
            onChange={(color) => onChange(key, color)}
            colorId={`${colorIdPrefix}-${key}`}
            panelElement={panelElement}
            solidOnly={true}
          />
        );
      } else if (schemaValue.type === 'number') {
        const numericValue = Number(rawValue);
        const normalizedValue = Number.isFinite(numericValue)
          ? numericValue
          : typeof schemaValue.default === 'number'
            ? schemaValue.default
            : 0;
        // step 값에서 소수 자릿수 자동 추론
        const stepStr =
          schemaValue.step != null ? String(schemaValue.step) : '';
        const dotIdx = stepStr.indexOf('.');
        const hasDecimal = dotIdx !== -1;
        const decimalScale = hasDecimal ? stepStr.length - dotIdx - 1 : 0;
        control = (
          <NumberInput
            value={normalizedValue}
            min={schemaValue.min}
            max={schemaValue.max}
            allowDecimal={hasDecimal}
            decimalScale={decimalScale}
            onChange={(nextValue) => onChange(key, nextValue)}
            width={getPluginInputWidth('number', rawValue)}
          />
        );
      } else if (schemaValue.type === 'string') {
        const stringValue =
          rawValue === undefined || rawValue === null ? '' : String(rawValue);
        control = (
          <TextInput
            value={stringValue}
            onChange={(nextValue) => onChange(key, nextValue)}
            placeholder={
              typeof placeholderText === 'string' ? placeholderText : undefined
            }
            width={getPluginInputWidth('string', stringValue)}
          />
        );
      } else if (schemaValue.type === 'select') {
        const options = (schemaValue.options || []).map((option) => ({
          label: translate(option.label, option.label),
          value: String(option.value),
        }));
        const optionMap = new Map(
          (schemaValue.options || []).map((option) => [
            String(option.value),
            option.value,
          ]),
        );
        const selectedValue = optionMap.has(String(rawValue))
          ? String(rawValue)
          : String(schemaValue.default ?? '');
        control = (
          <Dropdown
            value={selectedValue}
            options={options}
            placeholder={
              typeof placeholderText === 'string' &&
              placeholderText.trim().length > 0
                ? placeholderText
                : undefined
            }
            onChange={(nextValue) =>
              onChange(key, optionMap.get(nextValue) ?? nextValue)
            }
          />
        );
      }

      if (schemaValue.type === 'boolean') {
        return (
          <div
            key={key}
            className="dmn-property-row flex min-h-[32px] w-full items-center justify-between"
          >
            <p className="dmn-property-row__label">{labelText}</p>
            <div className="flex items-center gap-[10.5px]">{control}</div>
          </div>
        );
      }

      return (
        <PropertyRow key={key} label={labelText}>
          {control}
        </PropertyRow>
      );
    });

    const filtered = rows.filter(Boolean);
    const fields = <>{filtered}</>;

    if (!wrap) {
      return fields;
    }

    return <div className="flex flex-col gap-[12px]">{fields}</div>;
  };

  // 배치 편집용 interactiveRefs
  const batchColorPickerInteractiveRefs = [
    batchNoteColorButtonRef,
    batchGlowColorButtonRef,
    batchBorderColorButtonRef,
    batchCounterFillButtonRef,
    batchCounterStrokeButtonRef,
  ];

  // 배치 피커 토글
  const handleBatchPickerToggle = (target: BatchPickerTarget) => {
    if (target && target !== batchPickerFor) {
      const keysData = getSelectedKeysData();
      const keyOnly = getSelectedKeyOnlyPositions();
      const isNoteTabPicker =
        target === 'noteColor' ||
        target === 'glowColor' ||
        target === 'borderColor';
      const firstPos =
        isNoteTabPicker && keyOnly.length > 0
          ? keyOnly[0].position
          : keysData[0]?.position;
      if (firstPos) {
        const counterSettings = normalizeCounterSettings(firstPos.counter);
        setBatchLocalColors({
          noteColor: (() => {
            const nc = firstPos.noteColor;
            if (
              nc &&
              typeof nc === 'object' &&
              'type' in nc &&
              nc.type === 'gradient'
            ) {
              return { type: 'gradient', top: nc.top, bottom: nc.bottom };
            }
            return typeof nc === 'string' ? nc : '#FFFFFF';
          })(),
          glowColor: (() => {
            const gc = firstPos.noteGlowColor ?? firstPos.noteColor;
            if (
              gc &&
              typeof gc === 'object' &&
              'type' in gc &&
              gc.type === 'gradient'
            ) {
              return { type: 'gradient', top: gc.top, bottom: gc.bottom };
            }
            return typeof gc === 'string' ? gc : '#FFFFFF';
          })(),
          borderColor: firstPos.noteBorderColor ?? '#FFFFFF',
          borderOpacity: firstPos.noteBorderOpacity ?? 100,
          fillIdle: counterSettings.fill.idle,
          fillActive: counterSettings.fill.active,
          strokeIdle: counterSettings.stroke.idle,
          strokeActive: counterSettings.stroke.active,
        });
        setBatchLocalOpacities({
          noteOpacity:
            typeof firstPos.noteOpacity === 'number'
              ? firstPos.noteOpacity
              : 80,
          glowOpacity:
            typeof firstPos.noteGlowOpacity === 'number'
              ? firstPos.noteGlowOpacity
              : 70,
        });
      }
    }
    setBatchPickerFor((prev) => (prev === target ? null : target));
  };

  const getBatchPickerColor = (): NoteColor | string => {
    switch (batchPickerFor) {
      case 'noteColor':
        return batchLocalColors.noteColor;
      case 'glowColor':
        return batchLocalColors.glowColor;
      case 'borderColor':
        return hexWithAlphaPercent(
          batchLocalColors.borderColor,
          batchLocalColors.borderOpacity,
        );
      case 'fill':
        return batchCounterColorState === 'active'
          ? batchLocalColors.fillActive
          : batchLocalColors.fillIdle;
      case 'stroke':
        return batchCounterColorState === 'active'
          ? batchLocalColors.strokeActive
          : batchLocalColors.strokeIdle;
      default:
        return '#FFFFFF';
    }
  };

  const getBatchPickerRef = () => {
    switch (batchPickerFor) {
      case 'noteColor':
        return batchNoteColorButtonRef;
      case 'glowColor':
        return batchGlowColorButtonRef;
      case 'borderColor':
        return batchBorderColorButtonRef;
      case 'fill':
        return batchCounterFillButtonRef;
      case 'stroke':
        return batchCounterStrokeButtonRef;
      default:
        return null;
    }
  };

  const handleBatchPickerColorChange = (newColor: NoteColor) => {
    if (!batchPickerFor) return;

    if (batchPickerFor === 'noteColor' || batchPickerFor === 'glowColor') {
      setBatchLocalColors((prev) => ({
        ...prev,
        [batchPickerFor]: newColor,
      }));
    } else if (batchPickerFor === 'borderColor') {
      const raw = typeof newColor === 'string' ? newColor : undefined;
      setBatchLocalColors((prev) => ({
        ...prev,
        borderColor: toRgbHexColor(raw),
        borderOpacity: parseAlphaPercent(raw, prev.borderOpacity),
      }));
    } else if (batchPickerFor === 'fill') {
      const solidColor = typeof newColor === 'string' ? newColor : '#FFFFFF';
      const key =
        batchCounterColorState === 'active' ? 'fillActive' : 'fillIdle';
      setBatchLocalColors((prev) => ({
        ...prev,
        [key]: solidColor,
      }));
    } else if (batchPickerFor === 'stroke') {
      const solidColor = typeof newColor === 'string' ? newColor : '#FFFFFF';
      const key =
        batchCounterColorState === 'active' ? 'strokeActive' : 'strokeIdle';
      setBatchLocalColors((prev) => ({
        ...prev,
        [key]: solidColor,
      }));
    }

    const isGradientNoteLikeColor =
      !!newColor &&
      typeof newColor === 'object' &&
      newColor.type === 'gradient';

    if (
      isGradientNoteLikeColor &&
      (batchPickerFor === 'noteColor' || batchPickerFor === 'glowColor')
    ) {
      return;
    }

    if (batchPickerFor === 'noteColor') {
      if (selectedKeyElements.length > 0 && selectedStatElements.length > 0) {
        handleBatchNoteColorChangeKeysOnly(newColor);
      } else {
        handleBatchNoteColorChange(newColor);
      }
    } else if (batchPickerFor === 'glowColor') {
      if (selectedKeyElements.length > 0 && selectedStatElements.length > 0) {
        handleBatchGlowColorChangeKeysOnly(newColor);
      } else {
        handleBatchGlowColorChange(newColor);
      }
    } else if (batchPickerFor === 'borderColor') {
      // noteBorderColor는 #RRGGBB 계약 — 색은 hex로 정규화, 알파는 noteBorderOpacity로 분리
      const raw = typeof newColor === 'string' ? newColor : undefined;
      const solidColor = toRgbHexColor(raw);
      const opacity = parseAlphaPercent(raw, batchLocalColors.borderOpacity);
      if (selectedKeyElements.length > 0 && selectedStatElements.length > 0) {
        handleBatchKeyOnlyStyleChange('noteBorderColor', solidColor);
        handleBatchKeyOnlyStyleChange('noteBorderOpacity', opacity);
      } else {
        handleBatchStyleChange('noteBorderColor', solidColor);
        handleBatchStyleChange('noteBorderOpacity', opacity);
      }
    }
  };

  const handleBatchPickerColorChangeComplete = (newColor: NoteColor) => {
    if (!batchPickerFor) return;

    if (batchPickerFor === 'noteColor' || batchPickerFor === 'glowColor') {
      setBatchLocalColors((prev) => ({
        ...prev,
        [batchPickerFor]: newColor,
      }));
    } else if (batchPickerFor === 'borderColor') {
      const raw = typeof newColor === 'string' ? newColor : undefined;
      setBatchLocalColors((prev) => ({
        ...prev,
        borderColor: toRgbHexColor(raw),
        borderOpacity: parseAlphaPercent(raw, prev.borderOpacity),
      }));
    } else if (batchPickerFor === 'fill') {
      const solidColor = typeof newColor === 'string' ? newColor : '#FFFFFF';
      const key =
        batchCounterColorState === 'active' ? 'fillActive' : 'fillIdle';
      setBatchLocalColors((prev) => ({
        ...prev,
        [key]: solidColor,
      }));
    } else if (batchPickerFor === 'stroke') {
      const solidColor = typeof newColor === 'string' ? newColor : '#FFFFFF';
      const key =
        batchCounterColorState === 'active' ? 'strokeActive' : 'strokeIdle';
      setBatchLocalColors((prev) => ({
        ...prev,
        [key]: solidColor,
      }));
    }

    const keysData = getSelectedKeysData();
    const firstCounter = keysData[0]?.position
      ? normalizeCounterSettings(keysData[0].position.counter)
      : createDefaultCounterSettings();

    if (batchPickerFor === 'noteColor') {
      if (selectedKeyElements.length > 0 && selectedStatElements.length > 0) {
        handleBatchNoteColorChangeCompleteKeysOnly(newColor);
      } else {
        handleBatchNoteColorChangeComplete(newColor);
      }
    } else if (batchPickerFor === 'glowColor') {
      if (selectedKeyElements.length > 0 && selectedStatElements.length > 0) {
        handleBatchGlowColorChangeCompleteKeysOnly(newColor);
      } else {
        handleBatchGlowColorChangeComplete(newColor);
      }
    } else if (batchPickerFor === 'borderColor') {
      // noteBorderColor는 #RRGGBB 계약 — 색은 hex로 정규화(이슈 #73), 알파는 noteBorderOpacity로 분리
      const raw = typeof newColor === 'string' ? newColor : undefined;
      const solidColor = toRgbHexColor(raw);
      const opacity = parseAlphaPercent(raw, batchLocalColors.borderOpacity);
      if (selectedKeyElements.length > 0 && selectedStatElements.length > 0) {
        handleBatchKeyOnlyStyleChangeComplete('noteBorderColor', solidColor);
        handleBatchKeyOnlyStyleChangeComplete('noteBorderOpacity', opacity);
      } else {
        handleBatchStyleChangeComplete('noteBorderColor', solidColor);
        handleBatchStyleChangeComplete('noteBorderOpacity', opacity);
      }
    } else if (batchPickerFor === 'fill') {
      const fillColor = typeof newColor === 'string' ? newColor : '#FFFFFF';
      if (batchCounterColorState === 'active') {
        handleBatchCounterUpdate({
          fill: { ...firstCounter.fill, active: fillColor },
        });
      } else {
        handleBatchCounterUpdate({
          fill: { ...firstCounter.fill, idle: fillColor },
        });
      }
    } else if (batchPickerFor === 'stroke') {
      const strokeColor = typeof newColor === 'string' ? newColor : '#FFFFFF';
      if (batchCounterColorState === 'active') {
        handleBatchCounterUpdate({
          stroke: { ...firstCounter.stroke, active: strokeColor },
        });
      } else {
        handleBatchCounterUpdate({
          stroke: { ...firstCounter.stroke, idle: strokeColor },
        });
      }
    }
  };

  // ============================================================================
  // 렌더링
  // ============================================================================

  const selectionKind: SelectionKind = (() => {
    if (selectedElements.length === 0) return 'none';
    if (selectedSpriteElements.length > 0) {
      return singleSpritePosition ? 'sprite' : 'unsupported';
    }
    const noPlugin = selectedPluginElements.length === 0;
    const noKeyLike = selectedKeyLikeElements.length === 0;
    const noGraph = selectedGraphElements.length === 0;
    const noKnob = selectedKnobElements.length === 0;
    if (
      selectedBatchStyleElements.length > 1 &&
      noPlugin &&
      (!noKeyLike || (!noGraph && !noKnob))
    ) {
      return 'batchKeyLike';
    }
    if (selectedKnobElements.length > 1 && noKeyLike && noGraph && noPlugin) {
      return 'batchKnob';
    }
    if (selectedGraphElements.length > 1 && noKeyLike && noKnob && noPlugin) {
      return 'batchGraph';
    }
    if (!noPlugin && noKeyLike && noGraph) return 'plugin';
    if (
      selectedKnobElements.length === 1 &&
      !!singleKnobPosition &&
      noKeyLike &&
      noGraph &&
      noPlugin
    ) {
      return 'knob';
    }
    if (
      selectedGraphElements.length === 1 &&
      !!singleGraphPosition &&
      noKeyLike &&
      noPlugin
    ) {
      return 'graph';
    }
    if (singleKeyPosition) return 'key';
    if (singleStatPosition) return 'stat';
    return 'unsupported';
  })();

  /** The settings of the open part, for the selected kind of element. */
  const renderPanel = () => {
    // 다중 선택인 경우 (키/통계 포함, 또는 그래프+노브 혼합)
    if (selectionKind === 'batchKeyLike') {
      return (
        <BatchKeyLikePanel
          inspectorAction={focusedAction ?? 'style'}
          setPanelElement={setPanelElement}
          selectedBatchStyleElements={selectedBatchStyleElements}
          selectedKeyElements={selectedKeyElements}
          selectedStatElements={selectedStatElements}
          selectedGraphElements={selectedGraphElements}
          selectedKeyLikeElements={selectedKeyLikeElements}
          selectedGroupInfo={selectedGroupInfo}
          isRenaming={false}
          renameInputRef={renameInputRef}
          renameValue={renameValue}
          setRenameValue={setRenameValue}
          renameCancelledRef={renameCancelledRef}
          handleRenameCommit={handleRenameCommit}
          handleRenameCancel={handleRenameCancel}
          handleRenameStart={handleRenameStart}
          handleToggleMode={handleToggleMode}
          handleTogglePanel={handleTogglePanel}
          activeTab={activeTab}
          setActiveTab={setActiveTab}
          handleBatchAlign={handleBatchAlign}
          handleBatchDistribute={handleBatchDistribute}
          handleBatchSpacing={handleBatchSpacing}
          handleBatchSpacingPreview={handleBatchSpacingPreview}
          handleBatchSpacingCommit={handleBatchSpacingCommit}
          getBatchSpacingValue={getBatchSpacingValue}
          handleBatchResize={handleBatchResize}
          handleBatchStyleChange={handleBatchStyleChange}
          handleBatchStyleChangeComplete={handleBatchStyleChangeComplete}
          handleKeyOnlyStyleChangeComplete={handleKeyOnlyStyleChangeComplete}
          handleBatchCounterUpdate={handleBatchCounterUpdate}
          handleBatchNoteColorChange={handleBatchNoteColorChange}
          handleBatchNoteColorChangeComplete={
            handleBatchNoteColorChangeComplete
          }
          handleBatchGlowColorChange={handleBatchGlowColorChange}
          handleBatchGlowColorChangeComplete={
            handleBatchGlowColorChangeComplete
          }
          handleGraphBatchSharedSetting={handleGraphBatchSharedSetting}
          getMixedValue={getMixedValue}
          getMixedValueBatch={getMixedValueBatch}
          getMixedValueGraphs={getMixedValueGraphs}
          getMixedValueGraphsAsKey={getMixedValueGraphsAsKey}
          getMixedValueKeysOnly={getMixedValueKeysOnly}
          getSelectedKeysData={getSelectedKeysData}
          getSelectedGraphsData={getSelectedGraphsData}
          getSelectedBatchStyleData={getSelectedBatchStyleData}
          getSelectedKeyOnlyPositions={getSelectedKeyOnlyPositions}
          handleBatchKeyOnlyStyleChangeComplete={
            handleBatchKeyOnlyStyleChangeComplete
          }
          handleBatchNoteColorChangeKeysOnly={
            handleBatchNoteColorChangeKeysOnly
          }
          handleBatchNoteColorChangeCompleteKeysOnly={
            handleBatchNoteColorChangeCompleteKeysOnly
          }
          handleBatchGlowColorChangeKeysOnly={
            handleBatchGlowColorChangeKeysOnly
          }
          handleBatchGlowColorChangeCompleteKeysOnly={
            handleBatchGlowColorChangeCompleteKeysOnly
          }
          batchScrollRefFor={batchScrollRefFor}
          batchThumbRefFor={batchThumbRefFor}
          batchNoteColorButtonRef={batchNoteColorButtonRef}
          batchGlowColorButtonRef={batchGlowColorButtonRef}
          batchBorderColorButtonRef={batchBorderColorButtonRef}
          batchCounterFillButtonRef={batchCounterFillButtonRef}
          batchCounterStrokeButtonRef={batchCounterStrokeButtonRef}
          batchImageButtonRef={batchImageButtonRef}
          showBatchImagePicker={showBatchImagePicker}
          setShowBatchImagePicker={setShowBatchImagePicker}
          batchPickerFor={batchPickerFor}
          setBatchPickerFor={setBatchPickerFor}
          batchCounterColorState={batchCounterColorState}
          setBatchCounterColorState={setBatchCounterColorState}
          batchLocalColors={batchLocalColors}
          setBatchLocalColors={setBatchLocalColors}
          batchLocalOpacities={batchLocalOpacities}
          setBatchLocalOpacities={setBatchLocalOpacities}
          handleBatchPickerToggle={handleBatchPickerToggle}
          handleBatchPickerColorChange={handleBatchPickerColorChange}
          handleBatchPickerColorChangeComplete={
            handleBatchPickerColorChangeComplete
          }
          getBatchPickerColor={getBatchPickerColor}
          getBatchPickerRef={getBatchPickerRef}
          batchColorPickerInteractiveRefs={batchColorPickerInteractiveRefs}
          panelElement={null}
          useCustomCSS={useCustomCSS}
          selectedKeyType={selectedKeyType}
          t={t}
        />
      );
    }

    // 다중 선택인 경우 (노브 요소만)
    if (selectionKind === 'batchKnob') {
      return (
        <BatchKnobOnlyPanel
          setPanelElement={setPanelElement}
          selectedKnobElements={selectedKnobElements}
          selectedGroupInfo={selectedGroupInfo}
          isRenaming={false}
          renameInputRef={renameInputRef}
          renameValue={renameValue}
          setRenameValue={setRenameValue}
          renameCancelledRef={renameCancelledRef}
          handleRenameCommit={handleRenameCommit}
          handleRenameCancel={handleRenameCancel}
          handleRenameStart={handleRenameStart}
          handleToggleMode={handleToggleMode}
          handleTogglePanel={handleTogglePanel}
          handleBatchAlign={handleBatchAlign}
          handleBatchDistribute={handleBatchDistribute}
          handleBatchSpacing={handleBatchSpacing}
          handleBatchSpacingPreview={handleBatchSpacingPreview}
          handleBatchSpacingCommit={handleBatchSpacingCommit}
          getBatchSpacingValue={getBatchSpacingValue}
          handleBatchResize={handleBatchResize}
          handleBatchStyleChange={handleBatchStyleChange}
          handleBatchStyleChangeComplete={handleBatchStyleChangeComplete}
          handleKnobBatchSharedSetting={handleKnobBatchSharedSetting}
          getMixedValueKnobs={getMixedValueKnobs}
          getMixedValueKnobsAsKey={getMixedValueKnobsAsKey}
          getSelectedKnobsData={getSelectedKnobsData}
          batchScrollRefFor={batchScrollRefFor}
          batchThumbRefFor={batchThumbRefFor}
          batchImageButtonRef={batchImageButtonRef}
          showBatchImagePicker={showBatchImagePicker}
          setShowBatchImagePicker={setShowBatchImagePicker}
          panelElement={null}
          useCustomCSS={useCustomCSS}
          selectedKeyType={selectedKeyType}
          t={t}
        />
      );
    }

    // 다중 선택인 경우 (그래프 요소만)
    if (selectionKind === 'batchGraph') {
      return (
        <BatchGraphOnlyPanel
          setPanelElement={setPanelElement}
          selectedGraphElements={selectedGraphElements}
          selectedGroupInfo={selectedGroupInfo}
          isRenaming={false}
          renameInputRef={renameInputRef}
          renameValue={renameValue}
          setRenameValue={setRenameValue}
          renameCancelledRef={renameCancelledRef}
          handleRenameCommit={handleRenameCommit}
          handleRenameCancel={handleRenameCancel}
          handleRenameStart={handleRenameStart}
          handleToggleMode={handleToggleMode}
          handleTogglePanel={handleTogglePanel}
          handleBatchAlign={handleBatchAlign}
          handleBatchDistribute={handleBatchDistribute}
          handleBatchSpacing={handleBatchSpacing}
          handleBatchSpacingPreview={handleBatchSpacingPreview}
          handleBatchSpacingCommit={handleBatchSpacingCommit}
          getBatchSpacingValue={getBatchSpacingValue}
          handleBatchResize={handleBatchResize}
          handleBatchStyleChange={handleBatchStyleChange}
          handleBatchStyleChangeComplete={handleBatchStyleChangeComplete}
          handleGraphBatchSharedSetting={handleGraphBatchSharedSetting}
          getMixedValueGraphs={getMixedValueGraphs}
          getMixedValueGraphsAsKey={getMixedValueGraphsAsKey}
          getSelectedGraphsData={getSelectedGraphsData}
          batchScrollRefFor={batchScrollRefFor}
          batchThumbRefFor={batchThumbRefFor}
          batchImageButtonRef={batchImageButtonRef}
          showBatchImagePicker={showBatchImagePicker}
          setShowBatchImagePicker={setShowBatchImagePicker}
          panelElement={null}
          useCustomCSS={useCustomCSS}
          selectedKeyType={selectedKeyType}
          t={t}
        />
      );
    }

    // 플러그인 요소가 선택된 경우
    if (selectionKind === 'plugin') {
      const pluginTitle =
        selectedPluginDefinition?.name ||
        selectedPluginElement?.definitionId ||
        t('propertiesPanel.pluginElement') ||
        'Plugin';

      return (
        <PluginSelectionPanel
          setPanelElement={setPanelElement}
          pluginTitle={pluginTitle}
          handleToggleMode={handleToggleMode}
          handleTogglePanel={handleTogglePanel}
          setPluginScrollRef={setPluginScrollRef}
          setPluginThumbRef={setPluginThumbRef}
          selectedPluginElement={selectedPluginElement}
          hasSinglePluginSelection={hasSinglePluginSelection}
          showModalHint={showModalHint}
          showSettings={showSettings}
          renderPluginSettingsForm={renderPluginSettingsForm}
          selectedPluginDefinition={selectedPluginDefinition}
          resolvedPluginSettings={resolvedPluginSettings}
          handlePluginSettingChange={handlePluginSettingChange}
          t={t}
        />
      );
    }

    // 단일 노브 요소 선택인 경우
    if (selectionKind === 'knob' && singleKnobPosition) {
      return (
        <SingleKnobPanel
          setPanelElement={setPanelElement}
          singleKnobPosition={singleKnobPosition}
          singleKnobIndex={singleKnobIndex!}
          selectedKeyType={selectedKeyType}
          isRenaming={false}
          renameInputRef={renameInputRef}
          renameValue={renameValue}
          setRenameValue={setRenameValue}
          renameCancelledRef={renameCancelledRef}
          handleRenameCommit={handleRenameCommit}
          handleRenameCancel={handleRenameCancel}
          handleRenameStart={handleRenameStart}
          handleKnobUpdate={handleKnobUpdate}
          handleToggleMode={handleToggleMode}
          handleTogglePanel={handleTogglePanel}
          singleScrollRefFor={singleScrollRefFor}
          singleThumbRefFor={singleThumbRefFor}
          panelElement={null}
          useCustomCSS={useCustomCSS}
          t={t}
        />
      );
    }

    if (selectionKind === 'sprite' && singleSpritePosition) {
      return (
        <SpritePanel
          mode={selectedKeyType}
          sprite={singleSpritePosition}
          useCustomCSS={useCustomCSS}
          onLeavePage={() => openPath([], true)}
          t={t}
        />
      );
    }

    // 단일 그래프 요소 선택인 경우
    if (selectionKind === 'graph' && singleGraphPosition) {
      return (
        <SingleGraphPanel
          setPanelElement={setPanelElement}
          singleGraphPosition={singleGraphPosition}
          singleGraphIndex={singleGraphIndex!}
          selectedKeyType={selectedKeyType}
          isRenaming={false}
          renameInputRef={renameInputRef}
          renameValue={renameValue}
          setRenameValue={setRenameValue}
          renameCancelledRef={renameCancelledRef}
          handleRenameCommit={handleRenameCommit}
          handleRenameCancel={handleRenameCancel}
          handleRenameStart={handleRenameStart}
          handleToggleMode={handleToggleMode}
          handleTogglePanel={handleTogglePanel}
          handleGraphUpdate={handleGraphUpdate}
          singleScrollRefFor={singleScrollRefFor}
          singleThumbRefFor={singleThumbRefFor}
          showGraphImagePicker={showGraphImagePicker}
          setShowGraphImagePicker={setShowGraphImagePicker}
          graphImageButtonRef={graphImageButtonRef}
          graphClassNameDraft={graphClassNameDraft}
          setGraphClassNameDraft={setGraphClassNameDraft}
          panelElement={null}
          useCustomCSS={useCustomCSS}
          t={t}
        />
      );
    }

    // 단일 키/통계 요소 선택인 경우
    const isSingleStat = selectionKind === 'stat';
    const isSingleKey = selectionKind === 'key';
    if (!isSingleKey && !isSingleStat) {
      return null;
    }

    return (
      <SingleKeyStatPanel
        inspectorAction={focusedAction ?? 'style'}
        binding={binding}
        setPanelElement={setPanelElement}
        isSingleStat={isSingleStat}
        isSingleKey={isSingleKey}
        singleKeyIndex={singleKeyIndex}
        singleStatIndex={singleStatIndex}
        singleKeyPosition={singleKeyPosition}
        singleStatPosition={singleStatPosition}
        singleKeyCode={singleKeyCode}
        singleKeyInfo={singleKeyInfo}
        selectedKeyType={selectedKeyType}
        isRenaming={false}
        renameInputRef={renameInputRef}
        renameValue={renameValue}
        setRenameValue={setRenameValue}
        renameCancelledRef={renameCancelledRef}
        handleRenameCommit={handleRenameCommit}
        handleRenameCancel={handleRenameCancel}
        handleRenameStart={handleRenameStart}
        handleToggleMode={handleToggleMode}
        handleTogglePanel={handleTogglePanel}
        activeTab={activeTab}
        setActiveTab={setActiveTab}
        onKeyUpdate={onKeyUpdate}
        onKeyPreview={onKeyPreview}
        handleStatUpdate={handleStatUpdate}
        handleStatPreview={handleStatPreview}
        onGhostUpdate={handleGhostUpdate}
        onGhostPreview={handleGhostPreview}
        showImagePicker={showImagePicker}
        setShowImagePicker={setShowImagePicker}
        imageButtonRef={imageButtonRef}
        panelElement={null}
        useCustomCSS={useCustomCSS}
        singleScrollRefFor={singleScrollRefFor}
        singleThumbRefFor={singleThumbRefFor}
        t={t}
      />
    );
  };
  // ── The selection as sidebar parts ────────────────────────────────────────

  const ghost = singleKeyPosition?.ghost;
  // Geometry always follows the owner, so the ghost shows the owner's box.
  const ghostFace: KeyPosition | null = singleKeyPosition
    ? {
        ...(ghost?.settings ?? createDefaultGhostSettings(singleKeyPosition)),
        dx: singleKeyPosition.dx,
        dy: singleKeyPosition.dy,
        width: singleKeyPosition.width,
        height: singleKeyPosition.height,
      }
    : null;

  /**
   * Where an element is and how big: four numbers, edited in two rows of the
   * element's page.
   */
  const geometryParts = (box: {
    x: number;
    y: number;
    width: number;
    height: number;
    /** Tenths are kept; otherwise whole numbers. */
    decimal?: boolean;
    size: { min: number; max: number };
    onX: (value: number) => void;
    onY: (value: number) => void;
    onWidth: (value: number) => void;
    onHeight: (value: number) => void;
    onSizeBlur?: () => void;
  }): InlinePart[] => {
    const digits = box.decimal ? 1 : 0;
    const positionLabel = t('propertiesPanel.position');
    const sizeLabel = t('propertiesPanel.size');
    return [
      {
        kind: 'inline',
        id: 'position',
        label: positionLabel,
        icon: PART_ICONS.position,
        control: (
          <>
            <NumberField
              prefix="X"
              ariaLabel={`${positionLabel} X`}
              value={box.x}
              digits={digits}
              min={-9999}
              max={9999}
              onChange={box.onX}
            />
            <NumberField
              prefix="Y"
              ariaLabel={`${positionLabel} Y`}
              value={box.y}
              digits={digits}
              min={-9999}
              max={9999}
              onChange={box.onY}
            />
          </>
        ),
      },
      {
        kind: 'inline',
        id: 'size',
        label: sizeLabel,
        icon: PART_ICONS.size,
        control: (
          <>
            <NumberField
              prefix="W"
              ariaLabel={`${sizeLabel} W`}
              value={box.width}
              digits={digits}
              min={box.size.min}
              max={box.size.max}
              onChange={box.onWidth}
              onBlur={box.onSizeBlur}
            />
            <NumberField
              prefix="H"
              ariaLabel={`${sizeLabel} H`}
              value={box.height}
              digits={digits}
              min={box.size.min}
              max={box.size.max}
              onChange={box.onHeight}
              onBlur={box.onSizeBlur}
            />
          </>
        ),
      },
    ];
  };

  /** The box of a key or a stat: shown while typed, saved on the way out. */
  const keyLikeGeometry = (
    index: number,
    position: KeyPosition,
    move: (index: number, dx: number, dy: number) => void,
    preview: (index: number, updates: Partial<KeyPosition>) => void,
  ) =>
    geometryParts({
      x: localState.dx ?? position.dx,
      y: localState.dy ?? position.dy,
      width: localState.width ?? position.width ?? 60,
      height: localState.height ?? position.height ?? 60,
      decimal: true,
      size: { min: 1, max: 999 },
      onX: (dx) => {
        setLocalState((prev) => ({ ...prev, dx }));
        move(index, dx, localState.dy ?? position.dy);
      },
      onY: (dy) => {
        setLocalState((prev) => ({ ...prev, dy }));
        move(index, localState.dx ?? position.dx, dy);
      },
      onWidth: (width) => {
        setLocalState((prev) => ({ ...prev, width }));
        preview(index, { width });
      },
      onHeight: (height) => {
        setLocalState((prev) => ({ ...prev, height }));
        preview(index, { height });
      },
      onSizeBlur: handleSizeBlur,
    });

  /**
   * The pages every face of a key has; a stat has the first two and a count.
   * Each row's picture is the face with that part on it, so its colors need
   * no swatches beside it.
   */
  const lookParts = (face: KeyPosition, textShown: string): PagePart[] => [
    {
      kind: 'page',
      id: 'shape',
      label: t('sidebar.partShape'),
      icon: <FaceThumb position={face} />,
      groups: ['style-shape', 'style-form', 'style-image', 'style-css'],
    },
    {
      kind: 'page',
      id: 'text',
      label: t('sidebar.partText'),
      icon: <TextThumb position={face} />,
      groups: ['style-text'],
      value: <RowText>{face.displayText || textShown}</RowText>,
    },
  ];

  const counterPart = (
    face: KeyPosition,
    label: string,
    update: (updates: Partial<KeyPosition>) => void,
  ): PagePart => {
    const counter = normalizeCounterSettings(face.counter);
    return {
      kind: 'page',
      id: 'counter',
      label,
      icon: <CountThumb position={face} />,
      groups: null,
      toggle: {
        checked: counter.enabled,
        onChange: (enabled) => update({ counter: { ...counter, enabled } }),
      },
    };
  };

  /** One face of a key: the key it is bound to, then its pages. */
  const keyFaceParts = (
    face: KeyPosition,
    target: 'default' | 'ghost',
    keyName: string,
  ): { input: InlinePart; pages: PagePart[] } => {
    const index = singleKeyIndex!;
    const update = (updates: Partial<KeyPosition>) =>
      (target === 'ghost' ? handleGhostUpdate : onKeyUpdate)({
        index,
        ...updates,
      });
    // The canvas shows the key's own face working; the ghost face has no
    // key of its own on the canvas to show it on.
    const preview = (part: PreviewPart) =>
      target === 'default' ? part : undefined;
    return {
      input: {
        kind: 'inline',
        id: 'input',
        label: t('propertiesPanel.tabInput'),
        icon: PART_ICONS.input,
        control: (
          <KeyCaptureButton
            keyName={keyName}
            listening={isListening && listenTarget === target}
            onListen={
              target === 'ghost' ? handleGhostKeyListen : handleKeyListen
            }
            t={t}
          />
        ),
      },
      pages: [
        ...lookParts(face, keyName).map((part) => ({
          ...part,
          preview: preview('look'),
        })),
        {
          kind: 'page',
          id: 'note',
          label: t('propertiesPanel.tabNote'),
          icon: <NoteThumb position={face} />,
          groups: null,
          preview: preview('note'),
          toggle: {
            checked: face.noteEffectEnabled ?? true,
            onChange: (enabled) => update({ noteEffectEnabled: enabled }),
          },
        },
        {
          ...counterPart(face, t('propertiesPanel.tabCounter'), update),
          preview: preview('counter'),
        },
        {
          kind: 'page',
          id: 'sound',
          label: t('propertiesPanel.sectionSound'),
          icon: PART_ICONS.sound,
          groups: ['input-sound'],
          toggle: {
            checked: face.soundEnabled ?? false,
            onLabel:
              face.soundPath?.split(/[\\/]/).pop() ||
              t('sidebar.soundNotChosen'),
            onChange: (enabled) => update({ soundEnabled: enabled }),
          },
        },
      ],
    };
  };

  /** Settings that have no page beside them are shown on the element's page. */
  const inPlacePart = (groups: readonly string[] | null): PagePart => ({
    kind: 'page',
    id: 'details',
    label: t('sidebar.partDetails'),
    groups,
    inPlace: true,
  });

  const parts: ElementPart[][] = (() => {
    const shapeLabel = t('sidebar.partShape');

    if (selectionKind === 'key' && singleKeyPosition && ghostFace) {
      const index = singleKeyIndex!;
      const main = keyFaceParts(
        singleKeyPosition,
        'default',
        singleKeyInfo?.displayName || singleKeyCode || '',
      );
      const ghostKeyName = ghost?.key
        ? getKeyInfoByGlobalKey(ghost.key).displayName
        : '';
      const second = keyFaceParts(ghostFace, 'ghost', ghostKeyName);
      return [
        [
          main.input,
          ...keyLikeGeometry(
            index,
            singleKeyPosition,
            onPositionChange,
            (at, updates) => onKeyPreview?.(at, updates),
          ),
        ],
        main.pages,
        [
          {
            kind: 'page',
            id: GHOST_PART,
            label: t('sidebar.ghostKey'),
            icon: <KeyThumb position={ghostFace} label={ghostKeyName} />,
            groups: null,
            toggle: {
              checked: !!ghost?.enabled,
              onLabel: ghostKeyName || t('sidebar.inputNone'),
              onChange: (enabled) => handleGhostEnabledChange(index, enabled),
            },
            // The ghost is drawn where its key is: it has no box of its own.
            parts: [[second.input], second.pages],
            hint: t('propertiesPanel.ghostKeyHint'),
          },
        ],
      ];
    }

    if (selectionKind === 'stat' && singleStatPosition) {
      const index = singleStatIndex!;
      const face = singleStatPosition as KeyPosition;
      const statType = (singleStatPosition.statType as StatItemType) || 'kps';
      const statLabel = t('propertiesPanel.statType');
      return [
        [
          {
            kind: 'inline',
            id: 'source',
            label: statLabel,
            icon: PART_ICONS.source,
            control: (
              <Dropdown
                ariaLabel={statLabel}
                align="right"
                options={STAT_TYPES.map((value) => ({
                  value,
                  label: getStatTypeLabel(value),
                }))}
                value={statType}
                onChange={(value) =>
                  handleStatUpdate({ index, statType: value as StatItemType })
                }
              />
            ),
          },
          ...keyLikeGeometry(
            index,
            face,
            (at, dx, dy) => handleStatUpdate({ index: at, dx, dy }),
            (at, updates) =>
              handleStatPreview(at, updates as Partial<StatItemPosition>),
          ),
        ],
        [
          ...lookParts(face, getStatTypeLabel(statType)),
          counterPart(face, t('sidebar.partNumber'), (updates) =>
            handleStatUpdate({
              index,
              ...updates,
            } as Partial<StatItemPosition> & { index: number }),
          ),
        ],
      ];
    }

    if (selectionKind === 'batchKeyLike') {
      const hasKeys = selectedKeyElements.length > 0;
      const hasKeyLike = selectedKeyLikeElements.length > 0;
      const noteShown = getMixedValueKeysOnly(
        (pos) => pos.noteEffectEnabled,
        true,
      );
      const countShown = getMixedValue(
        (pos) => normalizeCounterSettings(pos.counter).enabled,
        true,
      );
      const soundShown = getMixedValueKeysOnly(
        (pos) => pos.soundEnabled,
        false,
      );
      const pages: PagePart[] = [
        {
          kind: 'page',
          id: 'layout',
          label: t('propertiesPanel.sectionLayout'),
          icon: PART_ICONS.layout,
          groups: null,
        },
        {
          kind: 'page',
          id: 'shape',
          label: shapeLabel,
          icon: PART_ICONS.shape,
          groups: ['style-graph', 'style-shape', 'style-css'],
        },
      ];
      if (hasKeyLike) {
        pages.push({
          kind: 'page',
          id: 'text',
          label: t('sidebar.partText'),
          icon: PART_ICONS.text,
          groups: ['style-text'],
        });
      }
      if (hasKeys) {
        pages.push({
          kind: 'page',
          id: 'note',
          label: t('propertiesPanel.tabNote'),
          icon: PART_ICONS.note,
          groups: null,
          toggle: {
            checked: noteShown.value,
            mixed: noteShown.isMixed,
            onChange: (enabled) =>
              handleBatchKeyOnlyStyleChangeComplete(
                'noteEffectEnabled',
                enabled,
              ),
          },
        });
      }
      if (hasKeyLike) {
        pages.push({
          kind: 'page',
          id: 'counter',
          label: t('propertiesPanel.tabCounter'),
          icon: PART_ICONS.counter,
          groups: null,
          toggle: {
            checked: countShown.value,
            mixed: countShown.isMixed,
            onChange: (enabled) => handleBatchCounterUpdate({ enabled }),
          },
        });
      }
      if (hasKeys) {
        pages.push({
          kind: 'page',
          id: 'sound',
          label: t('propertiesPanel.sectionSound'),
          icon: PART_ICONS.sound,
          groups: null,
          toggle: {
            checked: soundShown.value ?? false,
            mixed: soundShown.isMixed,
            onChange: (enabled) =>
              handleKeyOnlyStyleChangeComplete('soundEnabled', enabled),
          },
        });
      }
      return [pages];
    }

    if (selectionKind === 'graph' && singleGraphPosition) {
      const index = singleGraphIndex!;
      const graphBackground =
        singleGraphPosition.backgroundColor || GRAPH_COLORS.background;
      const graphBorder =
        singleGraphPosition.borderColor || GRAPH_COLORS.border;
      return [
        geometryParts({
          x: singleGraphPosition.dx || 0,
          y: singleGraphPosition.dy || 0,
          width: singleGraphPosition.width || 200,
          height: singleGraphPosition.height || 100,
          size: { min: 20, max: 9999 },
          onX: (dx) => handleGraphUpdate({ index, dx }),
          onY: (dy) => handleGraphUpdate({ index, dy }),
          onWidth: (width) => handleGraphUpdate({ index, width }),
          onHeight: (height) => handleGraphUpdate({ index, height }),
        }),
        [
          {
            kind: 'page',
            id: 'g-chart',
            label: t('propertiesPanel.sectionGraph'),
            icon: (
              <GraphThumb
                background={graphBackground}
                border={graphBorder}
                line={singleGraphPosition.graphColor || GRAPH_COLORS.line}
              />
            ),
            groups: ['graph-chart'],
          },
          {
            kind: 'page',
            id: 'g-shape',
            label: shapeLabel,
            icon: (
              <GraphThumb background={graphBackground} border={graphBorder} />
            ),
            groups: ['graph-appearance', 'graph-css'],
          },
        ],
      ];
    }

    if (selectionKind === 'knob' && singleKnobPosition) {
      const index = singleKnobIndex!;
      const sensitivityLabel = t('propertiesPanel.knobSensitivity');
      const reverseLabel = t('propertiesPanel.knobReverse');
      return [
        [
          {
            kind: 'inline',
            id: 'k-input',
            label: t('propertiesPanel.tabInput'),
            icon: PART_ICONS.input,
            control: (
              <KnobAxisCapture
                axisId={singleKnobPosition.axisId}
                onBind={(axisId) => handleKnobUpdate({ index, axisId })}
                t={t}
              />
            ),
          },
          ...geometryParts({
            x: singleKnobPosition.dx || 0,
            y: singleKnobPosition.dy || 0,
            width: singleKnobPosition.width || 60,
            height: singleKnobPosition.height || 60,
            size: { min: 20, max: 9999 },
            onX: (dx) => handleKnobUpdate({ index, dx }),
            onY: (dy) => handleKnobUpdate({ index, dy }),
            onWidth: (width) => handleKnobUpdate({ index, width }),
            onHeight: (height) => handleKnobUpdate({ index, height }),
          }),
        ],
        [
          {
            kind: 'inline',
            id: 'k-sensitivity',
            label: sensitivityLabel,
            icon: PART_ICONS.speed,
            control: (
              // 1 turns the picture once for one turn of the knob.
              <NumberField
                suffix="×"
                ariaLabel={sensitivityLabel}
                value={Number(singleKnobPosition.sensitivity ?? 1)}
                digits={2}
                min={0}
                max={100}
                onChange={(sensitivity) =>
                  handleKnobUpdate({ index, sensitivity })
                }
              />
            ),
          },
          {
            kind: 'inline',
            id: 'k-reverse',
            label: reverseLabel,
            icon: PART_ICONS.reverse,
            control: (
              <Toggle
                checked={singleKnobPosition.reverse ?? false}
                ariaLabel={reverseLabel}
                onChange={(reverse) => handleKnobUpdate({ index, reverse })}
              />
            ),
          },
        ],
        [
          {
            kind: 'page',
            id: 'k-shape',
            label: shapeLabel,
            icon: (
              <KnobThumb
                background={
                  singleKnobPosition.backgroundColor ||
                  KEY_FACE_COLORS.background
                }
                border={
                  singleKnobPosition.borderColor || KEY_FACE_COLORS.border
                }
              />
            ),
            groups: ['knob-appearance', 'knob-css'],
          },
        ],
      ];
    }

    if (selectionKind === 'sprite' && singleSpritePosition) {
      const sprite = singleSpritePosition;
      const edit = (
        change: (current: typeof sprite) => typeof sprite,
      ): void => {
        editSprite(selectedKeyType, sprite.id, change);
      };
      const rotationLabel = t('sprite.rotation');
      const visibleLabel = t('sprite.visible');
      // What the image itself is made of; its box is the group in between.
      const own = spriteElementParts({
        mode: selectedKeyType,
        sprite,
        keys: spriteKeyOptions(
          positions[selectedKeyType] ?? [],
          keyMappings[selectedKeyType] ?? [],
        ),
        language: locale,
        useCustomCSS,
        t,
        onOpenPart: openPartFromPart,
      });
      return [
        own.image,
        [
          ...geometryParts({
            x: sprite.dx,
            y: sprite.dy,
            width: sprite.width,
            height: sprite.height,
            size: { min: 1, max: 9999 },
            onX: (dx) => edit((current) => ({ ...current, dx })),
            onY: (dy) => edit((current) => ({ ...current, dy })),
            onWidth: (width) => edit((current) => ({ ...current, width })),
            onHeight: (height) => edit((current) => ({ ...current, height })),
          }),
          {
            kind: 'inline',
            id: 'sp-rotation',
            label: rotationLabel,
            icon: PART_ICONS.rotation,
            control: (
              <NumberField
                suffix="°"
                ariaLabel={rotationLabel}
                value={sprite.rotation}
                digits={1}
                min={SPRITE_CONSTRAINTS.rotation.min}
                max={SPRITE_CONSTRAINTS.rotation.max}
                onChange={(rotation) =>
                  edit((current) => ({ ...current, rotation }))
                }
              />
            ),
          },
          {
            kind: 'inline',
            id: 'sp-visible',
            label: visibleLabel,
            icon: PART_ICONS.visible,
            control: (
              <Toggle
                checked={!sprite.hidden}
                ariaLabel={visibleLabel}
                onChange={(visible) =>
                  edit((current) => ({ ...current, hidden: !visible }))
                }
              />
            ),
          },
        ],
        own.looks,
        own.motion,
        own.more,
      ];
    }

    if (selectionKind === 'plugin') {
      return [
        isPluginResizable && selectedPluginElement
          ? geometryParts({
              x: selectedPluginElement.position.x,
              y: selectedPluginElement.position.y,
              width: pluginDisplaySize.width,
              height: pluginDisplaySize.height,
              decimal: true,
              size: { min: 10, max: 9999 },
              onX: handlePluginPositionXChange,
              onY: handlePluginPositionYChange,
              onWidth: handlePluginWidthChange,
              onHeight: handlePluginHeightChange,
            })
          : [],
        [inPlacePart(['plugin-settings'])],
      ];
    }

    // Groups of knobs or of graphs have one page of settings.
    return [[inPlacePart(null)]];
  })();

  const selectionTitle =
    selectedGroupInfo?.name ||
    (selectedElements.length > 1
      ? t('sidebar.selectedMany', { count: selectedElements.length })
      : getCurrentLayerName() ||
        getCurrentDefaultTitle() ||
        selectedPluginDefinition?.name ||
        selectedPluginElement?.definitionId ||
        '');
  if (pluginSettingsPanel) {
    return (
      <SidebarPage
        pageKey="plugin-settings"
        direction="forward"
        back={{
          label: t('common.cancel'),
          onBack: handlePluginSettingsPanelCancel,
        }}
        title={t('propertiesPanel.pluginSettings')}
        kind={pluginSettingsPanel.pluginId}
      >
        <InspectorPageContext.Provider
          value={{ activePage: 'plugin-settings', groups: null }}
        >
          <div className="en-part-content">
            <PluginSettingsPanelView
              setPanelElement={setPanelElement}
              pluginSettingsPanel={pluginSettingsPanel}
              pluginPanelSettings={pluginPanelSettings}
              handlePluginSettingsPanelChange={handlePluginSettingsPanelChange}
              handlePluginSettingsPanelConfirm={
                handlePluginSettingsPanelConfirm
              }
              handlePluginSettingsPanelCancel={handlePluginSettingsPanelCancel}
              setPluginScrollRef={setPluginScrollRef}
              setPluginThumbRef={setPluginThumbRef}
              renderPluginSettingsForm={renderPluginSettingsForm}
              t={t}
            />
          </div>
        </InspectorPageContext.Provider>
      </SidebarPage>
    );
  }

  const canOpenSelection =
    selectionKind !== 'none' && selectionKind !== 'unsupported';

  if (view === 'list' || !canOpenSelection) {
    return (
      <>
        {renderElementList({
          direction,
          list: (
            <LayerTabContent
              onSwitchToProperty={openSelection}
              onSelectionFromPanel={() => {
                selectionFromLayerPanelRef.current = true;
              }}
            />
          ),
          selectionCount: canOpenSelection ? selectedElements.length : 0,
          onOpenSelection: openSelection,
        })}
      </>
    );
  }

  return (
    <ElementPages
      direction={direction}
      listLabel={t('sidebar.elements')}
      onBackToList={backToList}
      title={selectionTitle}
      onDelete={requestDelete}
      identity={selectionIdentity}
      parts={parts}
      path={partPath}
      onNavigate={openPath}
      hint={selectedElements.length > 1 ? t('sidebar.batchHint') : ''}
      t={t}
    >
      {renderPanel()}
    </ElementPages>
  );
};

export default PropertiesPanel;
