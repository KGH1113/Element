import { FeatureSettings, SettingsGroup } from '../FeatureSettings';
import { Dimmed } from '@components/main/Sidebar/SidebarParts';
import React, { useEffect, useRef, useState } from 'react';
import { useInspectorPages } from '../inspectorPageContext';
import type { KeyCounterSettings } from '@src/types/key/keys';
import { PropertyRow, NumberInput, FontStyleToggle } from '../index';
import StateSwatches, { type LookState } from '../StateSwatches';
import Checkbox from '@components/main/common/Checkbox';
import Dropdown from '@components/main/common/Dropdown';
import FontPicker from '@components/main/Modal/content/pickers/FontPicker';
import FontManagerModal from '@components/main/Modal/content/managers/FontManagerModal';
import CounterAnimationPicker from '@components/main/Modal/content/pickers/CounterAnimationPicker';

interface BatchKeyVisual {
  width?: number;
  height?: number;
  backgroundColor?: string;
  borderColor?: string;
  borderWidth?: number;
  borderRadius?: number;
  fontColor?: string;
  fontSize?: number;
  fontWeight?: number;
  fontFamily?: string;
  fontItalic?: boolean;
  fontUnderline?: boolean;
  fontStrikethrough?: boolean;
  displayText?: string;
  displayName?: string;
  className?: string;
  activeBackgroundColor?: string;
  activeBorderColor?: string;
  activeFontColor?: string;
  useInlineStyles?: boolean;
  isStat?: boolean;
}

interface BatchCounterTabContentProps {
  visibilityMixed?: boolean;
  // 카운터 설정 (첫 번째 선택 키 기준)
  batchCounterSettings: KeyCounterSettings;
  // 첫 번째 선택 키의 시각 정보 (프리뷰용)
  keyVisual?: BatchKeyVisual;
  // 핸들러
  handleBatchCounterUpdate: (updates: Partial<KeyCounterSettings>) => void;
  /** The color of each state of a counter color, as it is shown right now. */
  counterColor: (target: 'fill' | 'stroke', state: LookState) => string;
  /** The state whose chooser is open for that color, if it is open. */
  openCounterColor: (target: 'fill' | 'stroke') => LookState | null;
  /** Opens the chooser of one state of a color, or closes it if it is open. */
  onCounterColorPress: (target: 'fill' | 'stroke', state: LookState) => void;
  // The pair of each color: the chooser is placed against it.
  batchCounterFillButtonRef: React.RefObject<HTMLDivElement | null>;
  batchCounterStrokeButtonRef: React.RefObject<HTMLDivElement | null>;
  // 패널 요소 (FloatingPopup 위치용)
  panelElement: HTMLElement | null;
  // 번역
  t: (key: string) => string;
}

const BatchCounterTabContent: React.FC<BatchCounterTabContentProps> = ({
  visibilityMixed = false,
  batchCounterSettings,
  keyVisual,
  handleBatchCounterUpdate,
  counterColor,
  openCounterColor,
  onCounterColorPress,
  batchCounterFillButtonRef,
  batchCounterStrokeButtonRef,
  panelElement,
  t,
}) => {
  const fontButtonRef = useRef<HTMLButtonElement>(null);
  const animationButtonRef = useRef<HTMLButtonElement>(null);
  const [showFontPicker, setShowFontPicker] = useState(false);
  const [showFontManager, setShowFontManager] = useState(false);
  const [showAnimationPicker, setShowAnimationPicker] = useState(false);
  const activePage = useInspectorPages()?.activePage;
  useEffect(() => {
    setShowFontPicker(false);
    setShowFontManager(false);
    setShowAnimationPicker(false);
  }, [activePage]);

  const handleAnimationUpdate = (
    nextAnimation: KeyCounterSettings['animation'],
  ) => {
    handleBatchCounterUpdate({ animation: nextAnimation });
  };

  return (
    <FeatureSettings
      enabled={batchCounterSettings.enabled}
      mixed={visibilityMixed}
    >
      <SettingsGroup
        id="counter-layout"
        title={t('editorHelp.position')}
        defaultOpen
      >
        {/* 배치 영역 */}
        <PropertyRow label={t('counterSetting.placementArea') || '배치 영역'}>
          <Dropdown
            options={[
              {
                label: t('counterSetting.placementInside') || '내부',
                value: 'inside',
              },
              {
                label: t('counterSetting.placementOutside') || '외부',
                value: 'outside',
              },
            ]}
            value={batchCounterSettings.placement}
            onChange={(value) =>
              handleBatchCounterUpdate({
                placement: value as 'inside' | 'outside',
              })
            }
          />
        </PropertyRow>

        {/* 정렬 방향 */}
        <PropertyRow label={t('counterSetting.alignDirection') || '정렬 방향'}>
          <Dropdown
            options={[
              { label: t('counterSetting.alignTop') || '상단', value: 'top' },
              {
                label: t('counterSetting.alignBottom') || '하단',
                value: 'bottom',
              },
              { label: t('counterSetting.alignLeft') || '좌측', value: 'left' },
              {
                label: t('counterSetting.alignRight') || '우측',
                value: 'right',
              },
            ]}
            value={batchCounterSettings.align}
            onChange={(value) =>
              handleBatchCounterUpdate({
                align: value as 'top' | 'bottom' | 'left' | 'right',
              })
            }
          />
        </PropertyRow>

        {/* 정렬 방식 (내부 배치 전용) */}
        <Dimmed off={batchCounterSettings.placement !== 'inside'}>
          <PropertyRow label={t('counterSetting.alignMode') || '정렬 방식'}>
            <Dropdown
              options={[
                {
                  label: t('counterSetting.alignModeCenter') || '가운데',
                  value: 'center',
                },
                {
                  label: t('counterSetting.alignModeBetween') || '양끝',
                  value: 'between',
                },
              ]}
              value={batchCounterSettings.alignMode ?? 'center'}
              onChange={(value) =>
                handleBatchCounterUpdate({
                  alignMode: value as 'center' | 'between',
                })
              }
            />
          </PropertyRow>
        </Dimmed>

        {/* 간격 */}
        <PropertyRow label={t('counterSetting.gap') || '간격'}>
          <NumberInput
            value={batchCounterSettings.gap}
            onChange={(value) => handleBatchCounterUpdate({ gap: value })}
            suffix="px"
            min={0}
            max={9999}
            width="54px"
          />
        </PropertyRow>
      </SettingsGroup>
      <SettingsGroup id="counter-colors" title={t('counterSetting.colors')}>
        {/* 채우기 색상 */}
        <PropertyRow label={t('counterSetting.fill') || '채우기'}>
          <StateSwatches
            ref={batchCounterFillButtonRef}
            label={t('counterSetting.fill') || '채우기'}
            colors={{
              idle: counterColor('fill', 'idle'),
              active: counterColor('fill', 'active'),
            }}
            open={openCounterColor('fill')}
            onPress={(state) => onCounterColorPress('fill', state)}
          />
        </PropertyRow>

        {/* 외곽선 색상 */}
        <PropertyRow label={t('counterSetting.stroke') || '외곽선'}>
          <StateSwatches
            ref={batchCounterStrokeButtonRef}
            label={t('counterSetting.stroke') || '외곽선'}
            colors={{
              idle: counterColor('stroke', 'idle'),
              active: counterColor('stroke', 'active'),
            }}
            open={openCounterColor('stroke')}
            onPress={(state) => onCounterColorPress('stroke', state)}
          />
        </PropertyRow>
      </SettingsGroup>
      <SettingsGroup id="counter-font" title={t('counterSetting.font')}>
        {/* 폰트 */}
        <PropertyRow label={t('counterSetting.font') || '폰트'}>
          <button
            ref={fontButtonRef}
            type="button"
            className="en-field-button"
            data-open={showFontPicker || undefined}
            onClick={() => setShowFontPicker((prev) => !prev)}
          >
            {t('propertiesPanel.configure') || '설정하기'}
          </button>
        </PropertyRow>

        {/* 폰트 크기 */}
        <PropertyRow label={t('counterSetting.fontSize') || '폰트 크기'}>
          <NumberInput
            visual="text"
            value={batchCounterSettings.fontSize ?? 16}
            onChange={(value) => handleBatchCounterUpdate({ fontSize: value })}
            suffix="px"
            min={8}
            max={72}
            width="54px"
          />
        </PropertyRow>

        {/* 폰트 스타일 */}
        <PropertyRow label={t('counterSetting.fontStyle') || '폰트 스타일'}>
          <FontStyleToggle
            isBold={(batchCounterSettings.fontWeight ?? 400) >= 700}
            isItalic={batchCounterSettings.fontItalic ?? false}
            isUnderline={batchCounterSettings.fontUnderline ?? false}
            isStrikethrough={batchCounterSettings.fontStrikethrough ?? false}
            onBoldChange={(value) =>
              handleBatchCounterUpdate({ fontWeight: value ? 700 : 400 })
            }
            onItalicChange={(value) =>
              handleBatchCounterUpdate({ fontItalic: value })
            }
            onUnderlineChange={(value) =>
              handleBatchCounterUpdate({ fontUnderline: value })
            }
            onStrikethroughChange={(value) =>
              handleBatchCounterUpdate({ fontStrikethrough: value })
            }
          />
        </PropertyRow>
      </SettingsGroup>
      <SettingsGroup id="counter-animation" title={t('editorHelp.animation')}>
        {/* 카운터 애니메이션 */}
        <div className="dmn-property-row flex min-h-[32px] w-full items-center justify-between">
          <p className="dmn-property-row__label">
            {t('counterSetting.animationEnabled') || '카운터 애니메이션'}
          </p>
          <Checkbox
            checked={batchCounterSettings.animation.enabled}
            onChange={() =>
              handleBatchCounterUpdate({
                animation: {
                  ...batchCounterSettings.animation,
                  enabled: !batchCounterSettings.animation.enabled,
                },
              })
            }
          />
        </div>

        <Dimmed off={!batchCounterSettings.animation.enabled}>
          <PropertyRow
            label={t('counterSetting.animation') || '애니메이션 설정'}
          >
            <button
              ref={animationButtonRef}
              type="button"
              className="en-field-button"
              data-open={showAnimationPicker || undefined}
              onClick={() => setShowAnimationPicker((prev) => !prev)}
            >
              {t('propertiesPanel.configure') || '설정하기'}
            </button>
          </PropertyRow>
        </Dimmed>
      </SettingsGroup>

      {/* FontPicker */}
      {showFontPicker && (
        <FontPicker
          open={true}
          referenceRef={fontButtonRef}
          panelElement={panelElement}
          selectedFont={batchCounterSettings.fontFamily || null}
          onFontSelect={(fontFamily) => {
            handleBatchCounterUpdate({ fontFamily });
          }}
          onClose={() => setShowFontPicker(false)}
          onOpenManager={() => {
            setShowFontManager(true);
          }}
          interactiveRefs={[fontButtonRef]}
        />
      )}

      {/* FontManagerModal */}
      {showFontManager && (
        <FontManagerModal
          isOpen={showFontManager}
          onClose={() => setShowFontManager(false)}
          t={t}
        />
      )}

      {showAnimationPicker && batchCounterSettings.animation.enabled && (
        <CounterAnimationPicker
          open={showAnimationPicker}
          referenceRef={animationButtonRef}
          panelElement={panelElement}
          animation={batchCounterSettings.animation}
          counterSettings={batchCounterSettings}
          keyVisual={keyVisual}
          onAnimationChange={handleAnimationUpdate}
          onClose={() => setShowAnimationPicker(false)}
          t={t}
          interactiveRefs={[animationButtonRef]}
        />
      )}
    </FeatureSettings>
  );
};

export default BatchCounterTabContent;
