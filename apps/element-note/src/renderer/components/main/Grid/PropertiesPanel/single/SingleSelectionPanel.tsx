import { useChoicePreviewStore } from '@src/renderer/editor/runtime/choicePreview';
import React, { useEffect, useRef, useState } from 'react';
import { PANEL_ROOT_CLASS } from '../panelChrome';
import { useInspectorPages } from '../inspectorPageContext';
import PropertySection from '../PropertySection';
import StateSwatches from '../StateSwatches';
import type { InspectorAction } from '../types';
import type { ImageFit, KeyPosition } from '@src/types/key/keys';
import type { StatItemPosition, StatItemType } from '@src/types/key/statItems';
import type {
  GraphItemPosition,
  GraphItemType,
} from '@src/types/key/graphItems';
import type { KnobItemPosition } from '@src/types/key/knobs';
import type {
  PluginSettingSchema,
  PluginMessages,
  PluginDefinitionInternal,
  PluginDisplayElementInternal,
} from '@src/types/plugin/api';
import type { KeyInfo } from '@utils/core/KeyMaps';
import { getKeyInfoByGlobalKey } from '@utils/core/KeyMaps';
import { createDefaultGhostSettings } from '@src/renderer/editor/model/ghostKeys';
import {
  PropertyRow,
  NumberInput,
  TextInput,
  ColorInput,
  SidebarToggleIcon,
  ModeToggleIcon,
  Tabs,
  StyleTabContent,
  NoteTabContent,
  CounterTabContent,
  TABS,
  TabType,
} from '../index';
import Checkbox from '@components/main/common/Checkbox';
import Dropdown from '@components/main/common/Dropdown';
import ColorPicker from '@components/main/Modal/content/pickers/ColorPicker';
import ImagePicker from '@components/main/Modal/content/pickers/ImagePicker';
import InputTabContent from './InputTabContent';

const InspectorGroup: React.FC<React.ComponentProps<typeof PropertySection>> = (
  props,
) => {
  const inspectorPages = useInspectorPages();
  return inspectorPages ? (
    <PropertySection {...props} />
  ) : (
    <>{props.children}</>
  );
};

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

const RenameIcon: React.FC = () => (
  <svg
    width="12"
    height="12"
    viewBox="0 0 24 24"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="M12 20H21"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
    <path
      d="M16.5 3.5C17.3284 2.67157 18.6716 2.67157 19.5 3.5V3.5C20.3284 4.32843 20.3284 5.67157 19.5 6.5L7 19L3 20L4 16L16.5 3.5Z"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

// ============================================================================
// Plugin Selection Panel
// ============================================================================

interface PluginSelectionPanelProps {
  setPanelElement: (el: HTMLDivElement | null) => void;
  pluginTitle: string;
  handleToggleMode: () => void;
  handleTogglePanel: () => void;
  setPluginScrollRef: (node: HTMLDivElement | null) => void;
  setPluginThumbRef: (node: HTMLDivElement | null) => void;
  selectedPluginElement: PluginDisplayElementInternal | null;
  hasSinglePluginSelection: boolean;
  showModalHint: boolean;
  showSettings: boolean;
  renderPluginSettingsForm: (
    schema: Record<string, PluginSettingSchema> | undefined,
    values: Record<string, unknown>,
    messages: PluginMessages | undefined,
    colorIdPrefix: string,
    onChange: (key: string, value: unknown) => void,
    options?: { wrap?: boolean },
  ) => React.ReactNode;
  selectedPluginDefinition: PluginDefinitionInternal | null;
  resolvedPluginSettings: Record<string, unknown>;
  handlePluginSettingChange: (key: string, value: unknown) => void;
  t: (key: string) => string | undefined;
}

export const PluginSelectionPanel: React.FC<PluginSelectionPanelProps> = ({
  setPanelElement,
  pluginTitle,
  handleToggleMode,
  handleTogglePanel,
  setPluginScrollRef,
  setPluginThumbRef,
  selectedPluginElement,
  hasSinglePluginSelection,
  showModalHint,
  showSettings,
  renderPluginSettingsForm,
  selectedPluginDefinition,
  resolvedPluginSettings,
  handlePluginSettingChange,
  t,
}) => {
  const isFloating = Boolean(useInspectorPages());
  return (
    <div
      ref={setPanelElement}
      className={`${PANEL_ROOT_CLASS}${isFloating ? ' en-inspector-panel' : ''}`}
    >
      <div className="dmn-properties-panel__header flex items-center justify-between px-[12px]">
        <span className="text-[#DBDEE8] text-style-2 truncate max-w-[120px]">
          {pluginTitle}
        </span>
        <div className="flex items-center gap-[4px]">
          <button
            onClick={handleToggleMode}
            className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
            title={t('propertiesPanel.switchToLayer') || 'Switch to Layer'}
          >
            <ModeToggleIcon mode="layer" />
          </button>
          <button
            onClick={handleTogglePanel}
            className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
            title={t('propertiesPanel.closePanel') || '속성 패널 닫기'}
          >
            <SidebarToggleIcon isOpen={true} />
          </button>
        </div>
      </div>
      <div
        className={
          isFloating
            ? 'en-inspector-body'
            : 'flex-1 properties-panel-overlay-scroll'
        }
      >
        <div
          ref={isFloating ? undefined : setPluginScrollRef}
          className={
            isFloating
              ? 'en-inspector-page'
              : 'properties-panel-overlay-viewport'
          }
        >
          <div className="p-[12px] flex flex-col gap-[12px]">
            {!hasSinglePluginSelection && (
              <p className="text-[#6B6D75] text-style-4 text-center">
                {t('propertiesPanel.pluginMultiSelection') ||
                  '플러그인 요소는 한 번에 하나만 편집할 수 있습니다.'}
              </p>
            )}
            {hasSinglePluginSelection && showModalHint && (
              <p className="text-[#6B6D75] text-style-4 text-center">
                {t('propertiesPanel.pluginModalHint') ||
                  '이 플러그인은 설정 모달을 사용합니다. 요소를 클릭해 설정하세요.'}
              </p>
            )}
            {showSettings && (
              <InspectorGroup
                id="plugin-settings"
                title={t('sidebar.partDetails') || 'Settings'}
              >
                {renderPluginSettingsForm(
                  selectedPluginDefinition?.settings,
                  resolvedPluginSettings,
                  selectedPluginDefinition?.messages,
                  `plugin-element-${selectedPluginElement?.fullId ?? 'unknown'}`,
                  handlePluginSettingChange,
                  { wrap: false },
                )}
              </InspectorGroup>
            )}
          </div>
          {!isFloating && (
            <div className="properties-panel-overlay-bar">
              <div
                ref={setPluginThumbRef}
                className="properties-panel-overlay-thumb"
                style={{ display: 'none' }}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

// ============================================================================
// Single Graph Selection Panel
// ============================================================================

interface SingleGraphPanelProps {
  setPanelElement: (el: HTMLDivElement | null) => void;
  singleGraphPosition: GraphItemPosition;
  singleGraphIndex: number;
  selectedKeyType: string;
  isRenaming: boolean;
  renameInputRef: React.RefObject<HTMLInputElement | null>;
  renameValue: string;
  setRenameValue: (value: string) => void;
  renameCancelledRef: React.MutableRefObject<boolean>;
  handleRenameCommit: (value: string) => void;
  handleRenameCancel: () => void;
  handleRenameStart: () => void;
  handleToggleMode: () => void;
  handleTogglePanel: () => void;
  handleGraphUpdate: (
    data: Partial<GraphItemPosition> & { index: number },
  ) => void;
  singleScrollRefFor: (tab: TabType) => (node: HTMLDivElement | null) => void;
  singleThumbRefFor: (tab: TabType) => (node: HTMLDivElement | null) => void;
  showGraphImagePicker: boolean;
  setShowGraphImagePicker: (value: boolean) => void;
  graphImageButtonRef: React.RefObject<HTMLButtonElement | null>;
  graphClassNameDraft: string;
  setGraphClassNameDraft: (value: string) => void;
  panelElement: HTMLDivElement | null;
  useCustomCSS: boolean;
  t: (key: string) => string | undefined;
}

export const SingleGraphPanel: React.FC<SingleGraphPanelProps> = ({
  setPanelElement,
  singleGraphPosition,
  singleGraphIndex,
  selectedKeyType,
  isRenaming,
  renameInputRef,
  renameValue,
  setRenameValue,
  renameCancelledRef,
  handleRenameCommit,
  handleRenameCancel,
  handleRenameStart,
  handleToggleMode,
  handleTogglePanel,
  handleGraphUpdate,
  singleScrollRefFor,
  singleThumbRefFor,
  showGraphImagePicker,
  setShowGraphImagePicker,
  graphImageButtonRef,
  graphClassNameDraft,
  setGraphClassNameDraft,
  panelElement,
  useCustomCSS,
  t,
}) => {
  const isFloating = Boolean(useInspectorPages());
  const graphShapeOptions = [
    { label: t('propertiesPanel.graphShapeLine') || 'Line', value: 'line' },
    { label: t('propertiesPanel.graphShapeBar') || 'Bar', value: 'bar' },
  ];

  const resolvedGraphStatType =
    (singleGraphPosition.statType as StatItemType) || 'kps';
  const graphDefaultTitle = `${getStatTypeLabel(resolvedGraphStatType)} Graph`;
  const graphTitle = singleGraphPosition.layerName || graphDefaultTitle;

  return (
    <div
      ref={setPanelElement}
      className={`${PANEL_ROOT_CLASS}${isFloating ? ' en-inspector-panel' : ''}`}
    >
      <div className="dmn-properties-panel__header flex items-center justify-between px-[12px]">
        {isRenaming ? (
          <input
            ref={renameInputRef}
            type="text"
            className="text-[#DBDEE8] text-style-2 bg-transparent border-none p-0 outline-none w-[130px] caret-ui-accent"
            value={renameValue}
            onChange={(e) => setRenameValue(e.target.value)}
            onBlur={() => {
              if (!renameCancelledRef.current) {
                handleRenameCommit(renameValue);
              }
              renameCancelledRef.current = false;
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                (e.target as HTMLInputElement).blur();
              } else if (e.key === 'Escape') {
                e.preventDefault();
                handleRenameCancel();
              }
            }}
          />
        ) : (
          <div className="flex items-center gap-[4px] min-w-0">
            <span
              className="text-[#DBDEE8] text-style-2 truncate max-w-[100px] cursor-default"
              onDoubleClick={handleRenameStart}
              title={graphTitle}
            >
              {graphTitle}
            </span>
            <button
              onClick={handleRenameStart}
              className="w-[18px] h-[18px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors flex-shrink-0"
              title={t('contextMenu.rename') || 'Rename'}
            >
              <RenameIcon />
            </button>
          </div>
        )}
        <div className="flex items-center gap-[4px]">
          <button
            onClick={handleToggleMode}
            className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
            title={t('propertiesPanel.switchToLayer') || 'Switch to Layer'}
          >
            <ModeToggleIcon mode="layer" />
          </button>
          <button
            onClick={handleTogglePanel}
            className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
            title={t('propertiesPanel.closePanel') || 'Close'}
          >
            <SidebarToggleIcon isOpen={true} />
          </button>
        </div>
      </div>
      <div
        className={
          isFloating
            ? 'en-inspector-body'
            : 'flex-1 properties-panel-overlay-scroll'
        }
      >
        <div
          ref={isFloating ? undefined : singleScrollRefFor(TABS.STYLE)}
          className={
            isFloating
              ? 'en-inspector-page'
              : 'properties-panel-overlay-viewport'
          }
        >
          <div className="p-[12px] flex flex-col gap-[12px]">
            <InspectorGroup
              id="graph-chart"
              title={t('propertiesPanel.sectionGraph') || 'Graph'}
            >
              <PropertyRow
                label={t('propertiesPanel.graphShape') || 'Graph Shape'}
              >
                <Dropdown
                  options={graphShapeOptions}
                  value={singleGraphPosition.graphType || 'line'}
                  onChange={(value) =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      graphType: value as GraphItemType,
                    })
                  }
                />
              </PropertyRow>

              {(singleGraphPosition.graphType || 'line') === 'line' && (
                <div className="dmn-property-row flex min-h-[32px] w-full items-center justify-between">
                  <p className="dmn-property-row__label">
                    {t('propertiesPanel.graphShowAverageLine') ||
                      'Show Average Line'}
                  </p>
                  <Checkbox
                    checked={singleGraphPosition.showAvgLine ?? true}
                    onChange={() =>
                      handleGraphUpdate({
                        index: singleGraphIndex,
                        showAvgLine: !(singleGraphPosition.showAvgLine ?? true),
                      })
                    }
                  />
                </div>
              )}

              <PropertyRow
                label={t('propertiesPanel.graphSpeed') || 'Graph Speed'}
              >
                <NumberInput
                  value={Math.round(singleGraphPosition.graphSpeed || 1000)}
                  width="62px"
                  onChange={(value) => {
                    const clamped = Math.max(500, Math.min(5000, value));
                    const snapped = Math.round(clamped / 100) * 100;
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      graphSpeed: snapped,
                    });
                  }}
                  min={500}
                  max={5000}
                  suffix="ms"
                />
              </PropertyRow>

              <PropertyRow
                label={t('propertiesPanel.graphColor') || 'Graph Color'}
              >
                <ColorInput
                  value={singleGraphPosition.graphColor || '#86EFAC'}
                  onChange={() => {}}
                  onChangeComplete={(value) =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      graphColor: value,
                    })
                  }
                  colorId={`graph-color-${selectedKeyType}-${singleGraphIndex}`}
                  panelElement={panelElement}
                />
              </PropertyRow>

              <div className="dmn-property-row flex min-h-[32px] w-full items-center justify-between">
                <p className="dmn-property-row__label">
                  {t('propertiesPanel.graphAnimation') || 'Graph Animation'}
                </p>
                <Checkbox
                  checked={singleGraphPosition.graphAnimationEnabled ?? true}
                  onChange={() =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      graphAnimationEnabled: !(
                        singleGraphPosition.graphAnimationEnabled ?? true
                      ),
                    })
                  }
                />
              </div>
            </InspectorGroup>
            <InspectorGroup
              id="graph-appearance"
              title={t('propertiesPanel.tabStyle') || 'Appearance'}
            >
              <PropertyRow
                label={
                  t('propertiesPanel.backgroundColor') || 'Background Color'
                }
              >
                <ColorInput
                  value={
                    singleGraphPosition.backgroundColor ||
                    'rgba(17, 17, 20, 0.9)'
                  }
                  onChange={() => {}}
                  onChangeComplete={(value) =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      backgroundColor: value,
                    })
                  }
                  colorId={`graph-bg-color-${selectedKeyType}-${singleGraphIndex}`}
                  panelElement={panelElement}
                />
              </PropertyRow>

              <PropertyRow
                label={t('propertiesPanel.borderColor') || 'Border Color'}
              >
                <ColorInput
                  value={
                    singleGraphPosition.borderColor ||
                    'rgba(255, 255, 255, 0.1)'
                  }
                  onChange={() => {}}
                  onChangeComplete={(value) =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      borderColor: value,
                    })
                  }
                  colorId={`graph-border-color-${selectedKeyType}-${singleGraphIndex}`}
                  panelElement={panelElement}
                />
              </PropertyRow>

              <PropertyRow
                label={t('propertiesPanel.borderWidth') || 'Border Width'}
              >
                <NumberInput
                  visual="stroke"
                  value={Math.round(singleGraphPosition.borderWidth ?? 3)}
                  onChange={(value) =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      borderWidth: Math.max(0, Math.min(20, value)),
                    })
                  }
                  min={0}
                  max={20}
                  suffix="px"
                />
              </PropertyRow>

              <PropertyRow
                label={t('propertiesPanel.borderRadius') || 'Border Radius'}
              >
                <NumberInput
                  visual="corner"
                  value={Math.round(singleGraphPosition.borderRadius ?? 8)}
                  onChange={(value) =>
                    handleGraphUpdate({
                      index: singleGraphIndex,
                      borderRadius: Math.max(0, Math.min(100, value)),
                    })
                  }
                  min={0}
                  max={100}
                  suffix="px"
                />
              </PropertyRow>

              <PropertyRow
                label={t('propertiesPanel.customImage') || 'Custom Image'}
              >
                <button
                  ref={graphImageButtonRef}
                  type="button"
                  className="en-field-button"
                  data-open={showGraphImagePicker || undefined}
                  onClick={() => setShowGraphImagePicker(!showGraphImagePicker)}
                >
                  {t('propertiesPanel.configure') || 'Configure'}
                </button>
              </PropertyRow>
            </InspectorGroup>
            {useCustomCSS && (
              <InspectorGroup
                id="graph-css"
                title={t('propertiesPanel.sectionCss') || 'Custom CSS'}
              >
                <div className="dmn-property-row flex min-h-[32px] w-full items-center justify-between">
                  <p className="dmn-property-row__label">
                    {t('propertiesPanel.useInlineStyles') ||
                      '인라인 스타일 우선'}
                  </p>
                  <Checkbox
                    checked={singleGraphPosition.useInlineStyles ?? false}
                    onChange={() =>
                      handleGraphUpdate({
                        index: singleGraphIndex,
                        useInlineStyles: !(
                          singleGraphPosition.useInlineStyles ?? false
                        ),
                      })
                    }
                  />
                </div>

                <PropertyRow label={t('propertiesPanel.className') || '클래스'}>
                  <TextInput
                    value={graphClassNameDraft}
                    onChange={setGraphClassNameDraft}
                    onBlur={() =>
                      handleGraphUpdate({
                        index: singleGraphIndex,
                        className: graphClassNameDraft || '',
                      })
                    }
                    placeholder="className"
                    width="90px"
                  />
                </PropertyRow>
              </InspectorGroup>
            )}
          </div>
          {!isFloating && (
            <div className="properties-panel-overlay-bar">
              <div
                ref={singleThumbRefFor(TABS.STYLE)}
                className="properties-panel-overlay-thumb"
                style={{ display: 'none' }}
              />
            </div>
          )}
        </div>
      </div>

      {showGraphImagePicker && graphImageButtonRef.current && (
        <ImagePicker
          open={showGraphImagePicker}
          referenceRef={graphImageButtonRef}
          panelElement={panelElement}
          idleImage={singleGraphPosition.inactiveImage || ''}
          activeImage={singleGraphPosition.activeImage || ''}
          idleTransparent={false}
          activeTransparent={false}
          idleImageFit={
            singleGraphPosition.idleImageFit ||
            singleGraphPosition.imageFit ||
            'cover'
          }
          activeImageFit={
            singleGraphPosition.activeImageFit ||
            singleGraphPosition.imageFit ||
            'cover'
          }
          onIdleImageChange={(imageUrl: string) =>
            handleGraphUpdate({
              index: singleGraphIndex,
              inactiveImage: imageUrl,
            })
          }
          onActiveImageChange={(imageUrl: string) =>
            handleGraphUpdate({
              index: singleGraphIndex,
              activeImage: imageUrl,
            })
          }
          onIdleTransparentChange={(value: boolean) =>
            handleGraphUpdate({
              index: singleGraphIndex,
              idleTransparent: value,
            })
          }
          onActiveTransparentChange={(value: boolean) =>
            handleGraphUpdate({
              index: singleGraphIndex,
              activeTransparent: value,
            })
          }
          onIdleImageFitChange={(fit: string) =>
            handleGraphUpdate({
              index: singleGraphIndex,
              idleImageFit: fit as ImageFit,
            })
          }
          onActiveImageFitChange={(fit: string) =>
            handleGraphUpdate({
              index: singleGraphIndex,
              activeImageFit: fit as ImageFit,
            })
          }
          onIdleImageReset={() =>
            handleGraphUpdate({
              index: singleGraphIndex,
              inactiveImage: '',
            })
          }
          onActiveImageReset={() =>
            handleGraphUpdate({
              index: singleGraphIndex,
              activeImage: '',
            })
          }
          onClose={() => setShowGraphImagePicker(false)}
        />
      )}
    </div>
  );
};

// ============================================================================
// Single Knob Selection Panel
// ============================================================================

interface SingleKnobPanelProps {
  setPanelElement: (el: HTMLDivElement | null) => void;
  singleKnobPosition: KnobItemPosition;
  singleKnobIndex: number;
  selectedKeyType: string;
  isRenaming: boolean;
  renameInputRef: React.RefObject<HTMLInputElement | null>;
  renameValue: string;
  setRenameValue: (value: string) => void;
  renameCancelledRef: React.MutableRefObject<boolean>;
  handleRenameCommit: (value: string) => void;
  handleRenameCancel: () => void;
  handleRenameStart: () => void;
  handleKnobUpdate: (
    data: Partial<KnobItemPosition> & { index: number },
  ) => void;
  handleToggleMode: () => void;
  handleTogglePanel: () => void;
  singleScrollRefFor: (tab: TabType) => (node: HTMLDivElement | null) => void;
  singleThumbRefFor: (tab: TabType) => (node: HTMLDivElement | null) => void;
  panelElement: HTMLDivElement | null;
  useCustomCSS: boolean;
  t: (key: string) => string;
}

export const SingleKnobPanel: React.FC<SingleKnobPanelProps> = ({
  setPanelElement,
  singleKnobPosition,
  singleKnobIndex,
  selectedKeyType,
  isRenaming,
  renameInputRef,
  renameValue,
  setRenameValue,
  renameCancelledRef,
  handleRenameCommit,
  handleRenameCancel,
  handleRenameStart,
  handleKnobUpdate,
  handleToggleMode,
  handleTogglePanel,
  singleScrollRefFor,
  singleThumbRefFor,
  panelElement,
  useCustomCSS,
  t,
}) => {
  const inspectorPages = useInspectorPages();
  const isFloating = Boolean(inspectorPages);
  const panelRef = useRef<HTMLDivElement | null>(null);
  const imageButtonRef = useRef<HTMLButtonElement | null>(null);
  const [showImagePicker, setShowImagePicker] = useState(false);
  const [classNameDraft, setClassNameDraft] = useState(
    singleKnobPosition.className || '',
  );

  useEffect(() => {
    setClassNameDraft(singleKnobPosition.className || '');
  }, [singleKnobIndex, selectedKeyType, singleKnobPosition.className]);

  const setRef = (node: HTMLDivElement | null) => {
    panelRef.current = node;
    setPanelElement(node);
  };

  const knobTitle = singleKnobPosition.layerName || 'Knob';
  // 대기/입력 색상 (키 패널과 동일한 기본값/전환 로직)
  const DEFAULT_KNOB_BACKGROUND_COLOR = 'rgba(46, 46, 47, 0.9)';
  const DEFAULT_KNOB_BORDER_COLOR = 'rgba(113, 113, 113, 0.9)';
  const DEFAULT_KNOB_ACTIVE_BACKGROUND_COLOR = 'rgba(121, 121, 121, 0.9)';
  const DEFAULT_KNOB_ACTIVE_BORDER_COLOR = 'rgba(255, 255, 255, 0.9)';

  type KnobColorTarget = 'backgroundColor' | 'borderColor';
  type KnobColorProperty =
    KnobColorTarget | 'activeBackgroundColor' | 'activeBorderColor';

  const [pickerFor, setPickerFor] = useState<KnobColorTarget | null>(null);
  const [colorState, setColorState] = useState<'idle' | 'active'>('idle');
  const bgColorBtnRef = useRef<HTMLDivElement>(null);
  const borderColorBtnRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setShowImagePicker(false);
    setPickerFor(null);
  }, [inspectorPages?.activePage]);

  const [localColors, setLocalColors] = useState<
    Record<KnobColorProperty, string>
  >({
    backgroundColor:
      singleKnobPosition.backgroundColor || DEFAULT_KNOB_BACKGROUND_COLOR,
    activeBackgroundColor:
      singleKnobPosition.activeBackgroundColor ||
      singleKnobPosition.backgroundColor ||
      DEFAULT_KNOB_ACTIVE_BACKGROUND_COLOR,
    borderColor: singleKnobPosition.borderColor || DEFAULT_KNOB_BORDER_COLOR,
    activeBorderColor:
      singleKnobPosition.activeBorderColor ||
      singleKnobPosition.borderColor ||
      DEFAULT_KNOB_ACTIVE_BORDER_COLOR,
  });

  // 피커가 닫혀있을 때만 외부 prop과 동기화
  useEffect(() => {
    if (!pickerFor) {
      setLocalColors({
        backgroundColor:
          singleKnobPosition.backgroundColor || DEFAULT_KNOB_BACKGROUND_COLOR,
        activeBackgroundColor:
          singleKnobPosition.activeBackgroundColor ||
          singleKnobPosition.backgroundColor ||
          DEFAULT_KNOB_ACTIVE_BACKGROUND_COLOR,
        borderColor:
          singleKnobPosition.borderColor || DEFAULT_KNOB_BORDER_COLOR,
        activeBorderColor:
          singleKnobPosition.activeBorderColor ||
          singleKnobPosition.borderColor ||
          DEFAULT_KNOB_ACTIVE_BORDER_COLOR,
      });
    }
  }, [
    pickerFor,
    singleKnobPosition.backgroundColor,
    singleKnobPosition.activeBackgroundColor,
    singleKnobPosition.borderColor,
    singleKnobPosition.activeBorderColor,
    DEFAULT_KNOB_BACKGROUND_COLOR,
    DEFAULT_KNOB_ACTIVE_BACKGROUND_COLOR,
    DEFAULT_KNOB_BORDER_COLOR,
    DEFAULT_KNOB_ACTIVE_BORDER_COLOR,
  ]);

  const resolveColorProperty = (target: KnobColorTarget): KnobColorProperty =>
    colorState === 'active'
      ? target === 'backgroundColor'
        ? 'activeBackgroundColor'
        : 'activeBorderColor'
      : target;

  const activeColorPropertyFor = (
    target: KnobColorTarget,
  ): 'activeBackgroundColor' | 'activeBorderColor' =>
    target === 'backgroundColor'
      ? 'activeBackgroundColor'
      : 'activeBorderColor';

  const isNonEmptyString = (value: unknown): value is string =>
    typeof value === 'string' && value.trim().length > 0;

  const colorValueFor = (target: KnobColorTarget): string =>
    localColors[resolveColorProperty(target)];

  const handleColorChange = (target: KnobColorTarget, color: string) => {
    const prop = resolveColorProperty(target);
    setLocalColors((prev) => ({ ...prev, [prop]: color }));
  };

  const handleColorChangeComplete = (
    target: KnobColorTarget,
    color: string,
  ) => {
    const prop = resolveColorProperty(target);
    setLocalColors((prev) => ({ ...prev, [prop]: color }));

    const updates: Partial<KnobItemPosition> = {
      [prop]: color,
    } as Partial<KnobItemPosition>;

    // idle 변경 시 active 값이 비어 있으면 현재 표시되던 active 값을 함께 저장
    // (active가 idle로 덮이는 현상 방지 — 키 패널과 동일)
    if (colorState !== 'active') {
      const activeProp = activeColorPropertyFor(target);
      const currentActive = singleKnobPosition[activeProp];
      if (!isNonEmptyString(currentActive)) {
        updates[activeProp] = localColors[activeProp];
      }
    }

    handleKnobUpdate({ index: singleKnobIndex, ...updates });
  };

  // A color has two states, both in its row. Pressing one opens its chooser;
  // pressing the one that is open closes it.
  const knobSwatches = (
    target: KnobColorTarget,
    ref: React.RefObject<HTMLDivElement | null>,
    label: string,
  ) => (
    <StateSwatches
      ref={ref}
      label={label}
      activeName={t('colorPicker.turning')}
      colors={{
        idle: localColors[target],
        active: localColors[activeColorPropertyFor(target)],
      }}
      open={pickerFor === target ? colorState : null}
      onPress={(state) => {
        if (pickerFor === target && colorState === state) {
          setPickerFor(null);
          return;
        }
        setColorState(state);
        setPickerFor(target);
      }}
    />
  );

  // 라운딩 기본값: 미지정 시 원형(짧은 변의 절반)
  const effectiveBorderRadius =
    singleKnobPosition.borderRadius ??
    Math.round(
      Math.min(
        singleKnobPosition.width || 60,
        singleKnobPosition.height || 60,
      ) / 2,
    );

  return (
    <div
      ref={setRef}
      className={`${PANEL_ROOT_CLASS}${isFloating ? ' en-inspector-panel' : ''}`}
    >
      <div className="dmn-properties-panel__header flex items-center justify-between px-[12px]">
        {isRenaming ? (
          <input
            ref={renameInputRef}
            type="text"
            className="text-[#DBDEE8] text-style-2 bg-transparent border-none p-0 outline-none w-[130px] caret-ui-accent"
            value={renameValue}
            onChange={(e) => setRenameValue(e.target.value)}
            onBlur={() => {
              if (!renameCancelledRef.current) {
                handleRenameCommit(renameValue);
              }
              renameCancelledRef.current = false;
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                (e.target as HTMLInputElement).blur();
              } else if (e.key === 'Escape') {
                e.preventDefault();
                handleRenameCancel();
              }
            }}
          />
        ) : (
          <div className="flex items-center gap-[4px] min-w-0">
            <span
              className="text-[#DBDEE8] text-style-2 truncate max-w-[100px] cursor-default"
              onDoubleClick={handleRenameStart}
              title={knobTitle}
            >
              {knobTitle}
            </span>
            <button
              onClick={handleRenameStart}
              className="w-[18px] h-[18px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors flex-shrink-0"
              title={t('contextMenu.rename') || 'Rename'}
            >
              <RenameIcon />
            </button>
          </div>
        )}
        <div className="flex items-center gap-[4px]">
          <button
            onClick={handleToggleMode}
            className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
            title={t('propertiesPanel.switchToLayer') || 'Switch to Layer'}
          >
            <ModeToggleIcon mode="layer" />
          </button>
          <button
            onClick={handleTogglePanel}
            className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
            title={t('propertiesPanel.closePanel') || 'Close'}
          >
            <SidebarToggleIcon isOpen={true} />
          </button>
        </div>
      </div>

      <div
        className={
          isFloating
            ? 'en-inspector-body'
            : 'flex-1 properties-panel-overlay-scroll'
        }
      >
        <div
          ref={isFloating ? undefined : singleScrollRefFor(TABS.STYLE)}
          className={
            isFloating
              ? 'en-inspector-page'
              : 'properties-panel-overlay-viewport'
          }
        >
          <div className="p-[12px] flex flex-col gap-[12px]">
            <InspectorGroup
              id="knob-appearance"
              title={t('propertiesPanel.tabStyle') || 'Appearance'}
            >
              {/* 배경색: 평소와 돌릴 때, 둘 다 줄에 보인다 */}
              <PropertyRow
                label={t('propertiesPanel.backgroundColor') || '배경색'}
              >
                {knobSwatches(
                  'backgroundColor',
                  bgColorBtnRef,
                  t('propertiesPanel.backgroundColor') || '배경색',
                )}
              </PropertyRow>

              {/* 테두리 색상 */}
              <PropertyRow
                label={t('propertiesPanel.borderColor') || '테두리 색상'}
              >
                {knobSwatches(
                  'borderColor',
                  borderColorBtnRef,
                  t('propertiesPanel.borderColor') || '테두리 색상',
                )}
              </PropertyRow>

              {/* 테두리 두께 */}
              <PropertyRow
                label={t('propertiesPanel.borderWidth') || '테두리 두께'}
              >
                <NumberInput
                  visual="stroke"
                  value={singleKnobPosition.borderWidth ?? 3}
                  onChange={(value) =>
                    handleKnobUpdate({
                      index: singleKnobIndex,
                      borderWidth: value,
                    })
                  }
                  suffix="px"
                  min={0}
                  max={20}
                />
              </PropertyRow>

              {/* 모서리 반경 (미지정 시 원형) */}
              <PropertyRow
                label={t('propertiesPanel.borderRadius') || '모서리 반경'}
              >
                <NumberInput
                  visual="corner"
                  value={effectiveBorderRadius}
                  onChange={(value) =>
                    handleKnobUpdate({
                      index: singleKnobIndex,
                      borderRadius: value,
                    })
                  }
                  suffix="px"
                  min={0}
                  max={999}
                />
              </PropertyRow>

              <PropertyRow
                label={t('propertiesPanel.customImage') || 'Custom Image'}
              >
                <button
                  ref={imageButtonRef}
                  type="button"
                  className="en-field-button"
                  data-open={showImagePicker || undefined}
                  onClick={() => setShowImagePicker(!showImagePicker)}
                >
                  {t('propertiesPanel.configure') || 'Configure'}
                </button>
              </PropertyRow>
            </InspectorGroup>
            {useCustomCSS && (
              <InspectorGroup
                id="knob-css"
                title={t('propertiesPanel.sectionCss') || 'Custom CSS'}
              >
                <PropertyRow label={t('propertiesPanel.className') || '클래스'}>
                  <TextInput
                    value={classNameDraft}
                    onChange={setClassNameDraft}
                    onBlur={() =>
                      handleKnobUpdate({
                        index: singleKnobIndex,
                        className: classNameDraft || '',
                      })
                    }
                    placeholder="className"
                    width="90px"
                  />
                </PropertyRow>
              </InspectorGroup>
            )}
          </div>
          {!isFloating && (
            <div className="properties-panel-overlay-bar">
              <div
                ref={singleThumbRefFor(TABS.STYLE)}
                className="properties-panel-overlay-thumb"
                style={{ display: 'none' }}
              />
            </div>
          )}
        </div>
      </div>

      {showImagePicker && imageButtonRef.current && (
        <ImagePicker
          open={showImagePicker}
          referenceRef={imageButtonRef}
          panelElement={panelRef.current}
          idleImage={singleKnobPosition.inactiveImage || ''}
          activeImage={singleKnobPosition.activeImage || ''}
          idleTransparent={singleKnobPosition.idleTransparent ?? false}
          activeTransparent={singleKnobPosition.activeTransparent ?? false}
          idleImageFit={
            singleKnobPosition.idleImageFit ||
            singleKnobPosition.imageFit ||
            'cover'
          }
          activeImageFit={
            singleKnobPosition.activeImageFit ||
            singleKnobPosition.imageFit ||
            'cover'
          }
          onIdleImageChange={(imageUrl: string) =>
            handleKnobUpdate({
              index: singleKnobIndex,
              inactiveImage: imageUrl,
            })
          }
          onActiveImageChange={(imageUrl: string) =>
            handleKnobUpdate({ index: singleKnobIndex, activeImage: imageUrl })
          }
          onIdleTransparentChange={(value: boolean) =>
            handleKnobUpdate({ index: singleKnobIndex, idleTransparent: value })
          }
          onActiveTransparentChange={(value: boolean) =>
            handleKnobUpdate({
              index: singleKnobIndex,
              activeTransparent: value,
            })
          }
          onIdleImageFitChange={(fit: string) =>
            handleKnobUpdate({
              index: singleKnobIndex,
              idleImageFit: fit as ImageFit,
            })
          }
          onActiveImageFitChange={(fit: string) =>
            handleKnobUpdate({
              index: singleKnobIndex,
              activeImageFit: fit as ImageFit,
            })
          }
          onIdleImageReset={() =>
            handleKnobUpdate({ index: singleKnobIndex, inactiveImage: '' })
          }
          onActiveImageReset={() =>
            handleKnobUpdate({ index: singleKnobIndex, activeImage: '' })
          }
          onClose={() => setShowImagePicker(false)}
        />
      )}

      {/* 평소/돌릴 때 색상 ColorPicker: 어느 쪽인지는 줄에서 고른다 */}
      {pickerFor && (
        <ColorPicker
          open={!!pickerFor}
          referenceRef={
            (pickerFor === 'backgroundColor'
              ? bgColorBtnRef
              : borderColorBtnRef) as React.RefObject<HTMLElement>
          }
          panelElement={panelElement}
          color={colorValueFor(pickerFor)}
          onColorChange={(c: string) => handleColorChange(pickerFor, c)}
          onColorChangeComplete={(c: string) =>
            handleColorChangeComplete(pickerFor, c)
          }
          onClose={() => setPickerFor(null)}
          solidOnly={true}
          interactiveRefs={
            [bgColorBtnRef, borderColorBtnRef] as React.RefObject<HTMLElement>[]
          }
        />
      )}
    </div>
  );
};

// ============================================================================
// Single Key/Stat Selection Panel
// ============================================================================

const GhostKeyHint: React.FC<{ t: (key: string) => string | undefined }> = ({
  t,
}) => (
  <p className="text-[#9395A1] text-style-4 leading-[18px]">
    {t('propertiesPanel.ghostKeyHint') ||
      'Bind a second keyboard key to this key. It gets its own style, note and counter, and the key limiter ignores it.'}
  </p>
);

interface SingleKeyStatPanelProps {
  inspectorAction?: InspectorAction;
  /** Which face of the key is edited; the sidebar's page head switches it. */
  binding: 'default' | 'ghost';
  setPanelElement: (el: HTMLDivElement | null) => void;
  isSingleStat: boolean;
  isSingleKey: boolean;
  singleKeyIndex: number | null;
  singleStatIndex: number | null;
  singleKeyPosition: KeyPosition | null;
  singleStatPosition: StatItemPosition | null;
  singleKeyCode: string | null;
  singleKeyInfo: KeyInfo | null;
  selectedKeyType: string;
  isRenaming: boolean;
  renameInputRef: React.RefObject<HTMLInputElement | null>;
  renameValue: string;
  setRenameValue: (value: string) => void;
  renameCancelledRef: React.MutableRefObject<boolean>;
  handleRenameCommit: (value: string) => void;
  handleRenameCancel: () => void;
  handleRenameStart: () => void;
  handleToggleMode: () => void;
  handleTogglePanel: () => void;
  activeTab: TabType;
  setActiveTab: (tab: TabType) => void;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  handleStatUpdate: (
    data: Partial<StatItemPosition> & { index: number },
  ) => void;
  handleStatPreview: (
    index: number,
    updates: Partial<StatItemPosition>,
  ) => void;
  onGhostUpdate?: (data: Partial<KeyPosition> & { index: number }) => void;
  onGhostPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  showImagePicker: boolean;
  setShowImagePicker: (value: boolean) => void;
  imageButtonRef: React.RefObject<HTMLButtonElement | null>;
  panelElement: HTMLDivElement | null;
  useCustomCSS: boolean;
  singleScrollRefFor: (tab: TabType) => (node: HTMLDivElement | null) => void;
  singleThumbRefFor: (tab: TabType) => (node: HTMLDivElement | null) => void;
  t: (key: string) => string | undefined;
}

export const SingleKeyStatPanel: React.FC<SingleKeyStatPanelProps> = ({
  inspectorAction,
  binding,
  setPanelElement,
  isSingleStat,
  isSingleKey: _isSingleKey,
  singleKeyIndex,
  singleStatIndex,
  singleKeyPosition,
  singleStatPosition,
  singleKeyCode,
  singleKeyInfo,
  selectedKeyType,
  isRenaming,
  renameInputRef,
  renameValue,
  setRenameValue,
  renameCancelledRef,
  handleRenameCommit,
  handleRenameCancel,
  handleRenameStart,
  handleToggleMode,
  handleTogglePanel,
  activeTab,
  setActiveTab,
  onKeyUpdate,
  onKeyPreview,
  handleStatUpdate,
  handleStatPreview,
  onGhostUpdate,
  onGhostPreview,
  showImagePicker,
  setShowImagePicker,
  imageButtonRef,
  panelElement,
  useCustomCSS,
  singleScrollRefFor,
  singleThumbRefFor,
  t,
}) => {
  const availableTabs = isSingleStat
    ? [TABS.STYLE, TABS.COUNTER]
    : [TABS.INPUT, TABS.STYLE, TABS.NOTE, TABS.COUNTER];

  // Default edits the key itself; Ghost edits its second keyboard key.
  const supportsGhost =
    !isSingleStat &&
    !!singleKeyPosition &&
    !!onGhostUpdate &&
    inspectorAction !== 'layout';
  const editingGhost = supportsGhost && binding === 'ghost';
  const ghost = singleKeyPosition?.ghost;
  // Geometry always follows the owner, so the editor shows the owner's box.
  const ghostPosition: KeyPosition | null = singleKeyPosition
    ? {
        ...(ghost?.settings ?? createDefaultGhostSettings(singleKeyPosition)),
        dx: singleKeyPosition.dx,
        dy: singleKeyPosition.dy,
        width: singleKeyPosition.width,
        height: singleKeyPosition.height,
      }
    : null;
  const ghostKeyInfo: KeyInfo | null = ghost?.key
    ? getKeyInfoByGlobalKey(ghost.key)
    : null;
  // The sidebar greys the settings out while the ghost is off, so they keep
  // their place; the hint lives on the element's first page.
  const showGhostHint = false;
  const shouldRenderTab = (tab: TabType) =>
    !inspectorAction || activeTab === tab;

  const resolvedStatType =
    (singleStatPosition?.statType as StatItemType) || 'kps';
  const statTitle = getStatTypeLabel(resolvedStatType);

  const keyLikeIndex = isSingleStat ? singleStatIndex! : singleKeyIndex!;
  const keyLikePosition: KeyPosition = isSingleStat
    ? singleStatPosition!
    : singleKeyPosition!;

  const keyLikeDefaultTitle = isSingleStat
    ? statTitle
    : singleKeyInfo?.displayName || singleKeyCode || 'Key';
  const keyLikeTitle = keyLikePosition?.layerName || keyLikeDefaultTitle;

  const keyLikeCode = isSingleStat ? null : singleKeyCode;
  const keyLikeInfo: KeyInfo | null = isSingleStat
    ? {
        browserKey: statTitle,
        globalKey: statTitle,
        displayName: statTitle,
      }
    : singleKeyInfo;

  const handleKeyLikeUpdate = isSingleStat
    ? (data: Partial<KeyPosition> & { index: number }) =>
        handleStatUpdate(data as Partial<StatItemPosition> & { index: number })
    : onKeyUpdate;

  // A look pointed at in the sidebar is drawn on the canvas over the element
  // itself (choicePreview.ts). The ghost face has no element of its own on
  // the canvas, so its pages draw nothing.
  const showChoice = (changes: Partial<KeyPosition> | null) =>
    useChoicePreviewStore
      .getState()
      .show(
        { type: isSingleStat ? 'stat' : 'key', index: keyLikeIndex },
        changes,
      );

  const handleKeyLikePreview = isSingleStat
    ? (index: number, updates: Partial<KeyPosition>) =>
        handleStatPreview(index, updates as Partial<StatItemPosition>)
    : onKeyPreview;

  return (
    <div
      ref={setPanelElement}
      className={`${PANEL_ROOT_CLASS}${inspectorAction ? ' en-inspector-panel' : ''}`}
      data-inspector-action={inspectorAction}
    >
      {/* 헤더 + 탭 영역 */}
      <div className="dmn-properties-panel__head-stack flex-shrink-0">
        {/* 헤더 */}
        {!inspectorAction && (
          <div className="dmn-properties-panel__header flex items-center justify-between px-[12px]">
            {isRenaming ? (
              <input
                ref={renameInputRef}
                type="text"
                className="text-[#DBDEE8] text-style-2 bg-transparent border-none p-0 outline-none w-[130px] caret-ui-accent"
                value={renameValue}
                onChange={(e) => setRenameValue(e.target.value)}
                onBlur={() => {
                  if (!renameCancelledRef.current) {
                    handleRenameCommit(renameValue);
                  }
                  renameCancelledRef.current = false;
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    (e.target as HTMLInputElement).blur();
                  } else if (e.key === 'Escape') {
                    e.preventDefault();
                    handleRenameCancel();
                  }
                }}
              />
            ) : (
              <div className="flex items-center gap-[4px] min-w-0">
                <span
                  className="text-[#DBDEE8] text-style-2 cursor-default truncate max-w-[110px]"
                  onDoubleClick={handleRenameStart}
                  title={keyLikeTitle}
                >
                  {keyLikeTitle}
                </span>
                <button
                  onClick={handleRenameStart}
                  className="w-[18px] h-[18px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors flex-shrink-0"
                  title={t('contextMenu.rename') || 'Rename'}
                >
                  <RenameIcon />
                </button>
              </div>
            )}

            <div className="flex items-center gap-[4px]">
              {/* 레이어 모드로 전환 버튼 */}
              <button
                onClick={handleToggleMode}
                className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
                title={t('propertiesPanel.switchToLayer') || 'Switch to Layer'}
              >
                <ModeToggleIcon mode="layer" />
              </button>
              {/* 패널 닫기 버튼 */}
              <button
                onClick={handleTogglePanel}
                className="w-[24px] h-[24px] flex items-center justify-center text-ui-fg-muted hover:text-ui-fg-primary rounded-[4px] transition-colors"
                title={t('propertiesPanel.closePanel') || '속성 패널 닫기'}
              >
                <SidebarToggleIcon isOpen={true} />
              </button>
            </div>
          </div>
        )}

        {/* 탭 */}
        {!inspectorAction && !showGhostHint && (
          <div className="px-[12px] pb-[12px]">
            <Tabs
              activeTab={activeTab}
              onTabChange={setActiveTab}
              t={t}
              availableTabs={availableTabs}
            />
          </div>
        )}
      </div>

      {/* Floating tools size to their active page; docked tabs keep their scroll position. */}
      <div
        className={
          inspectorAction
            ? 'en-inspector-body'
            : 'flex-1 properties-panel-overlay-scroll'
        }
      >
        {!isSingleStat && shouldRenderTab(TABS.INPUT) && (
          <div
            ref={inspectorAction ? undefined : singleScrollRefFor(TABS.INPUT)}
            className={`${inspectorAction ? 'en-inspector-page' : 'properties-panel-overlay-viewport'} ${activeTab === TABS.INPUT ? '' : 'hidden'}`}
          >
            <div className="p-[12px] flex flex-col gap-[12px]">
              {showGhostHint ? (
                <GhostKeyHint t={t} />
              ) : (
                <InputTabContent
                  key={editingGhost ? 'ghost-input' : 'default-input'}
                  keyIndex={singleKeyIndex!}
                  keyPosition={
                    editingGhost ? ghostPosition! : singleKeyPosition!
                  }
                  onKeyUpdate={editingGhost ? onGhostUpdate! : onKeyUpdate}
                  onKeyPreview={editingGhost ? onGhostPreview : onKeyPreview}
                  panelElement={panelElement}
                  t={t}
                />
              )}
            </div>
            {!inspectorAction && (
              <div className="properties-panel-overlay-bar">
                <div
                  ref={singleThumbRefFor(TABS.INPUT)}
                  className="properties-panel-overlay-thumb"
                  style={{ display: 'none' }}
                />
              </div>
            )}
          </div>
        )}
        {/* STYLE 탭 viewport */}
        {shouldRenderTab(TABS.STYLE) && (
          <div
            ref={inspectorAction ? undefined : singleScrollRefFor(TABS.STYLE)}
            className={`${inspectorAction ? 'en-inspector-page' : 'properties-panel-overlay-viewport'} ${
              activeTab === TABS.STYLE ? '' : 'hidden'
            }`}
          >
            <div className="p-[12px] flex flex-col gap-[12px]">
              {showGhostHint ? (
                <GhostKeyHint t={t} />
              ) : editingGhost ? (
                <StyleTabContent
                  key="ghost-style"
                  keyIndex={singleKeyIndex!}
                  keyPosition={ghostPosition!}
                  keyCode={ghost?.key ?? null}
                  keyInfo={ghostKeyInfo}
                  onKeyUpdate={onGhostUpdate!}
                  onKeyPreview={onGhostPreview}
                  showImagePicker={showImagePicker}
                  onToggleImagePicker={() =>
                    setShowImagePicker(!showImagePicker)
                  }
                  imageButtonRef={imageButtonRef}
                  panelElement={panelElement}
                  useCustomCSS={useCustomCSS}
                  t={t}
                />
              ) : (
                <StyleTabContent
                  key="default-style"
                  keyIndex={keyLikeIndex}
                  keyPosition={keyLikePosition}
                  keyCode={keyLikeCode}
                  keyInfo={keyLikeInfo}
                  onKeyUpdate={handleKeyLikeUpdate}
                  onKeyPreview={handleKeyLikePreview}
                  showImagePicker={showImagePicker}
                  onToggleImagePicker={() =>
                    setShowImagePicker(!showImagePicker)
                  }
                  imageButtonRef={imageButtonRef}
                  panelElement={panelElement}
                  useCustomCSS={useCustomCSS}
                  t={t}
                />
              )}
            </div>
            {!inspectorAction && (
              <div className="properties-panel-overlay-bar">
                <div
                  ref={singleThumbRefFor(TABS.STYLE)}
                  className="properties-panel-overlay-thumb"
                  style={{ display: 'none' }}
                />
              </div>
            )}
          </div>
        )}

        {/* NOTE 탭 viewport */}
        {!isSingleStat && shouldRenderTab(TABS.NOTE) && (
          <div
            ref={inspectorAction ? undefined : singleScrollRefFor(TABS.NOTE)}
            className={`${inspectorAction ? 'en-inspector-page' : 'properties-panel-overlay-viewport'} ${
              activeTab === TABS.NOTE ? '' : 'hidden'
            }`}
          >
            <div className="p-[12px] flex flex-col gap-[12px]">
              {showGhostHint ? (
                <GhostKeyHint t={t} />
              ) : (
                <NoteTabContent
                  key={editingGhost ? 'ghost-note' : 'default-note'}
                  keyIndex={singleKeyIndex!}
                  keyPosition={
                    editingGhost ? ghostPosition! : singleKeyPosition!
                  }
                  onKeyUpdate={editingGhost ? onGhostUpdate! : onKeyUpdate}
                  onKeyPreview={editingGhost ? onGhostPreview : onKeyPreview}
                  onChoicePreview={editingGhost ? undefined : showChoice}
                  panelElement={panelElement}
                  t={t}
                />
              )}
            </div>
            {!inspectorAction && (
              <div className="properties-panel-overlay-bar">
                <div
                  ref={singleThumbRefFor(TABS.NOTE)}
                  className="properties-panel-overlay-thumb"
                  style={{ display: 'none' }}
                />
              </div>
            )}
          </div>
        )}

        {/* COUNTER 탭 viewport */}
        {shouldRenderTab(TABS.COUNTER) && (
          <div
            ref={inspectorAction ? undefined : singleScrollRefFor(TABS.COUNTER)}
            className={`${inspectorAction ? 'en-inspector-page' : 'properties-panel-overlay-viewport'} ${
              activeTab === TABS.COUNTER ? '' : 'hidden'
            }`}
          >
            <div className="p-[12px] flex flex-col gap-[12px]">
              {showGhostHint ? (
                <GhostKeyHint t={t} />
              ) : editingGhost ? (
                <CounterTabContent
                  key="ghost-counter"
                  keyIndex={singleKeyIndex!}
                  keyPosition={ghostPosition!}
                  keyDisplayName={ghostKeyInfo?.displayName}
                  isStat={false}
                  onKeyUpdate={onGhostUpdate!}
                  panelElement={panelElement}
                  t={t}
                />
              ) : (
                <CounterTabContent
                  key="default-counter"
                  keyIndex={keyLikeIndex}
                  keyPosition={keyLikePosition}
                  keyDisplayName={keyLikeInfo?.displayName}
                  isStat={isSingleStat}
                  onKeyUpdate={handleKeyLikeUpdate}
                  onChoicePreview={showChoice}
                  countKey={
                    !isSingleStat && keyLikeCode
                      ? { mode: selectedKeyType, key: keyLikeCode }
                      : undefined
                  }
                  panelElement={panelElement}
                  t={t}
                />
              )}
            </div>
            {!inspectorAction && (
              <div className="properties-panel-overlay-bar">
                <div
                  ref={singleThumbRefFor(TABS.COUNTER)}
                  className="properties-panel-overlay-thumb"
                  style={{ display: 'none' }}
                />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
