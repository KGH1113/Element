import { FeatureSettings, SettingsGroup } from '../FeatureSettings';
import { Dimmed } from '@components/main/Sidebar/SidebarParts';
import React, { useEffect, useRef, useState } from 'react';
import { useInspectorPages } from '../inspectorPageContext';
import type { CounterTabContentProps } from '../types';
import type {
  KeyCounterAnimationSettings,
  KeyCounterSettings,
} from '@src/types/key/keys';
import { normalizeCounterSettings } from '@src/types/key/keys';
import { PropertyRow, FontStyleToggle, NumberInput } from '../PropertyInputs';
import StateSwatches from '../StateSwatches';
import Checkbox from '@components/main/common/Checkbox';
import PictureSegments from '../PictureSegments';
import KeyCountSettings from '../KeyCountSettings';
import ColorPicker from '@components/main/Modal/content/pickers/ColorPicker';
import FontPicker from '@components/main/Modal/content/pickers/FontPicker';
import FontManagerModal from '@components/main/Modal/content/managers/FontManagerModal';
import CounterAnimationPicker from '@components/main/Modal/content/pickers/CounterAnimationPicker';

type PickerTarget = 'fill' | 'stroke' | 'font' | null;
type Placement = KeyCounterSettings['placement'];
type Side = KeyCounterSettings['align'];
type Arrangement = 'center' | 'between';

const COUNTER_PLACEMENTS: readonly Placement[] = ['inside', 'outside'];
const PLACEMENT_LABELS: Record<Placement, string> = {
  inside: 'counterSetting.placementInside',
  outside: 'counterSetting.placementOutside',
};
const COUNTER_SIDES: readonly Side[] = ['top', 'bottom', 'left', 'right'];
const SIDE_LABELS: Record<Side, string> = {
  top: 'counterSetting.alignTop',
  bottom: 'counterSetting.alignBottom',
  left: 'counterSetting.alignLeft',
  right: 'counterSetting.alignRight',
};
const COUNTER_ARRANGEMENTS: readonly Arrangement[] = ['center', 'between'];
const ARRANGEMENT_LABELS: Record<Arrangement, string> = {
  center: 'counterSetting.alignModeCenter',
  between: 'counterSetting.alignModeBetween',
};

/*
 * Pictures of where the count goes: the key is an outline, the count (12) and
 * the key's name (a short bar) are what move from one choice to the next.
 */
const Count = ({ x, y }: { x: number; y: number }) => (
  <text
    className="en-pic-mark"
    x={x}
    y={y}
    textAnchor="middle"
    fontSize="7"
    fontWeight="800"
  >
    12
  </text>
);
const Name = ({ x, y }: { x: number; y: number }) => (
  <rect className="en-pic-faint" x={x} y={y} width="9" height="3" rx="1.5" />
);

function CounterPlacePicture({ placement }: { placement: Placement }) {
  return placement === 'inside' ? (
    <svg width="40" height="28" viewBox="0 0 40 28">
      <rect className="en-pic-key" x="7" y="4" width="26" height="20" rx="4" />
      <Count x={20} y={13} />
      <Name x={15.5} y={16} />
    </svg>
  ) : (
    <svg width="40" height="28" viewBox="0 0 40 28">
      <rect className="en-pic-key" x="7" y="11" width="26" height="15" rx="4" />
      <Count x={20} y={8} />
      <Name x={15.5} y={17} />
    </svg>
  );
}

function CounterSidePicture({
  placement,
  side,
}: {
  placement: Placement;
  side: Side;
}) {
  if (placement === 'outside') {
    const at = {
      top: [20, 6],
      bottom: [20, 27],
      left: [5, 16.5],
      right: [35, 16.5],
    }[side];
    return (
      <svg width="40" height="28" viewBox="0 0 40 28">
        <rect
          className="en-pic-key"
          x="10"
          y="8"
          width="20"
          height="12"
          rx="3"
        />
        <Count x={at[0]} y={at[1]} />
      </svg>
    );
  }
  const [count, name] = {
    top: [
      [20, 11],
      [15.5, 15],
    ],
    bottom: [
      [20, 21],
      [15.5, 9],
    ],
    left: [
      [11, 16.5],
      [20.5, 12.5],
    ],
    right: [
      [29, 16.5],
      [10.5, 12.5],
    ],
  }[side];
  return (
    <svg width="40" height="28" viewBox="0 0 40 28">
      <rect className="en-pic-key" x="4" y="3" width="32" height="22" rx="4" />
      <Count x={count[0]} y={count[1]} />
      <Name x={name[0]} y={name[1]} />
    </svg>
  );
}

function CounterArrangePicture({
  mode,
  side,
}: {
  mode: Arrangement;
  side: Side;
}) {
  const across = side === 'left' || side === 'right';
  const [count, name] = across
    ? mode === 'center'
      ? [
          [15, 16.5],
          [20.5, 12.5],
        ]
      : [
          [8.5, 16.5],
          [26, 12.5],
        ]
    : mode === 'center'
      ? [
          [20, 12.5],
          [15.5, 15.5],
        ]
      : [
          [20, 9.5],
          [15.5, 20],
        ];
  return (
    <svg width="40" height="28" viewBox="0 0 40 28">
      {across ? (
        <rect
          className="en-pic-key"
          x="2"
          y="6"
          width="36"
          height="16"
          rx="4"
        />
      ) : (
        <rect
          className="en-pic-key"
          x="8"
          y="1"
          width="24"
          height="26"
          rx="4"
        />
      )}
      <Count x={count[0]} y={count[1]} />
      <Name x={name[0]} y={name[1]} />
    </svg>
  );
}
type ColorState = 'idle' | 'active';

const CounterTabContent: React.FC<CounterTabContentProps> = ({
  keyIndex,
  keyPosition,
  keyDisplayName,
  isStat,
  onKeyUpdate,
  onChoicePreview,
  countKey,
  panelElement,
  t,
}) => {
  const fillBtnRef = useRef<HTMLDivElement>(null);
  const strokeBtnRef = useRef<HTMLDivElement>(null);
  const fontBtnRef = useRef<HTMLButtonElement>(null);
  const animationBtnRef = useRef<HTMLButtonElement>(null);

  const [pickerFor, setPickerFor] = useState<PickerTarget>(null);
  const pickerOpen = pickerFor !== null && pickerFor !== 'font';
  const [colorState, setColorState] = useState<ColorState>('idle');
  const [showFontManager, setShowFontManager] = useState(false);
  const [showAnimationPicker, setShowAnimationPicker] = useState(false);
  const activePage = useInspectorPages()?.activePage;
  useEffect(() => {
    setPickerFor(null);
    setShowFontManager(false);
    setShowAnimationPicker(false);
  }, [activePage]);

  const counterSettings = normalizeCounterSettings(keyPosition.counter);

  // 로컬 색상 상태 (드래그 중 UI 업데이트용)
  const [localColors, setLocalColors] = useState({
    fillIdle: counterSettings.fill.idle,
    fillActive: counterSettings.fill.active,
    strokeIdle: counterSettings.stroke.idle,
    strokeActive: counterSettings.stroke.active,
  });

  // 피커가 닫혀있을 때만 외부 prop과 동기화
  useEffect(() => {
    if (!pickerOpen) {
      setLocalColors({
        fillIdle: counterSettings.fill.idle,
        fillActive: counterSettings.fill.active,
        strokeIdle: counterSettings.stroke.idle,
        strokeActive: counterSettings.stroke.active,
      });
    }
  }, [
    pickerOpen,
    counterSettings.fill.idle,
    counterSettings.fill.active,
    counterSettings.stroke.idle,
    counterSettings.stroke.active,
  ]);

  const colorPickerInteractiveRefs = [
    fillBtnRef,
    strokeBtnRef,
  ] as React.RefObject<HTMLElement>[];

  const handleCounterUpdate = (updates: Partial<KeyCounterSettings>) => {
    const currentSettings = normalizeCounterSettings(keyPosition.counter);
    const newSettings = { ...currentSettings, ...updates };
    onKeyUpdate({ index: keyIndex, counter: newSettings });
  };

  // A look pointed at is drawn on the canvas with the rest of the count's
  // settings as they are.
  const previewCounter = (changes: Partial<KeyCounterSettings> | null) =>
    onChoicePreview?.(
      changes
        ? {
            counter: {
              ...normalizeCounterSettings(keyPosition.counter),
              ...changes,
            },
          }
        : null,
    );

  const handleAnimationUpdate = (
    nextAnimation: KeyCounterAnimationSettings,
  ) => {
    handleCounterUpdate({ animation: nextAnimation });
  };

  const handlePickerToggle = (target: Exclude<PickerTarget, null>) => {
    setPickerFor((prev) => (prev === target ? null : target));
  };

  // A color has two states, both in its row. Pressing one opens its chooser;
  // pressing the one that is open closes it.
  const handleColorPress = (target: 'fill' | 'stroke', state: ColorState) => {
    if (pickerFor === target && colorState === state) {
      setPickerFor(null);
      return;
    }
    setColorState(state);
    setPickerFor(target);
  };

  const activeColorFor = (
    target: Exclude<PickerTarget, null>,
    state: ColorState,
  ): string => {
    if (target === 'fill') {
      return state === 'active' ? localColors.fillActive : localColors.fillIdle;
    }
    return state === 'active'
      ? localColors.strokeActive
      : localColors.strokeIdle;
  };

  // 드래그 중 로컬 상태만 업데이트 (부모에게 전달 안함)
  const handleColorChange = (color: string) => {
    if (!pickerFor) return;
    const key =
      pickerFor === 'fill'
        ? colorState === 'active'
          ? 'fillActive'
          : 'fillIdle'
        : colorState === 'active'
          ? 'strokeActive'
          : 'strokeIdle';

    setLocalColors((prev) => ({ ...prev, [key]: color }));
  };

  // 드래그 완료 시 부모에게 전달
  const handleColorChangeComplete = (color: string) => {
    if (!pickerFor) return;

    const key =
      pickerFor === 'fill'
        ? colorState === 'active'
          ? 'fillActive'
          : 'fillIdle'
        : colorState === 'active'
          ? 'strokeActive'
          : 'strokeIdle';

    setLocalColors((prev) => ({ ...prev, [key]: color }));

    if (pickerFor === 'fill') {
      if (colorState === 'active') {
        handleCounterUpdate({
          fill: { ...counterSettings.fill, active: color },
        });
      } else {
        handleCounterUpdate({
          fill: { ...counterSettings.fill, idle: color },
        });
      }
      return;
    }

    if (colorState === 'active') {
      handleCounterUpdate({
        stroke: { ...counterSettings.stroke, active: color },
      });
    } else {
      handleCounterUpdate({
        stroke: { ...counterSettings.stroke, idle: color },
      });
    }
  };

  return (
    <>
      <FeatureSettings enabled={counterSettings.enabled}>
        <SettingsGroup
          id="counter-layout"
          title={t('partPage.place')}
          defaultOpen
        >
          {/* 배치 영역: 키 안쪽 / 바깥쪽 */}
          <PictureSegments
            label={t('counterSetting.placementArea') || '배치 영역'}
            choices={COUNTER_PLACEMENTS.map((placement) => ({
              id: placement,
              label: t(PLACEMENT_LABELS[placement]),
              picture: <CounterPlacePicture placement={placement} />,
            }))}
            value={counterSettings.placement}
            onChoose={(placement) => handleCounterUpdate({ placement })}
            onPreview={(placement) =>
              previewCounter(placement ? { placement } : null)
            }
          />

          {/* 정렬 방향: 어느 쪽에 */}
          <PictureSegments
            label={t('counterSetting.alignDirection') || '정렬 방향'}
            choices={COUNTER_SIDES.map((side) => ({
              id: side,
              label: t(SIDE_LABELS[side]),
              picture: (
                <CounterSidePicture
                  placement={counterSettings.placement}
                  side={side}
                />
              ),
            }))}
            value={counterSettings.align}
            onChoose={(align) => handleCounterUpdate({ align })}
            onPreview={(align) => previewCounter(align ? { align } : null)}
          />

          {/* 정렬 방식 (내부 배치 전용) */}
          <Dimmed off={counterSettings.placement !== 'inside'}>
            <PictureSegments
              label={t('counterSetting.alignMode') || '정렬 방식'}
              choices={COUNTER_ARRANGEMENTS.map((mode) => ({
                id: mode,
                label: t(ARRANGEMENT_LABELS[mode]),
                picture: (
                  <CounterArrangePicture
                    mode={mode}
                    side={counterSettings.align}
                  />
                ),
              }))}
              value={counterSettings.alignMode ?? 'center'}
              onChoose={(alignMode) => handleCounterUpdate({ alignMode })}
              onPreview={(alignMode) =>
                previewCounter(alignMode ? { alignMode } : null)
              }
            />
          </Dimmed>

          {/* 간격 */}
          <PropertyRow label={t('counterSetting.gap') || '간격'}>
            <NumberInput
              value={counterSettings.gap}
              onChange={(value) => handleCounterUpdate({ gap: value })}
              suffix="px"
              min={0}
              max={9999}
              width="54px"
            />
          </PropertyRow>
        </SettingsGroup>
        <SettingsGroup id="counter-text" title={t('partPage.text')}>
          {/* 채우기 색상 */}
          <PropertyRow label={t('counterSetting.fill') || '채우기'}>
            <StateSwatches
              ref={fillBtnRef}
              label={t('counterSetting.fill') || '채우기'}
              colors={{
                idle: localColors.fillIdle,
                active: localColors.fillActive,
              }}
              open={pickerFor === 'fill' ? colorState : null}
              onPress={(state) => handleColorPress('fill', state)}
            />
          </PropertyRow>

          {/* 외곽선 색상 */}
          <PropertyRow label={t('counterSetting.stroke') || '외곽선'}>
            <StateSwatches
              ref={strokeBtnRef}
              label={t('counterSetting.stroke') || '외곽선'}
              colors={{
                idle: localColors.strokeIdle,
                active: localColors.strokeActive,
              }}
              open={pickerFor === 'stroke' ? colorState : null}
              onPress={(state) => handleColorPress('stroke', state)}
            />
          </PropertyRow>
          {/* 폰트 */}
          <PropertyRow label={t('counterSetting.font') || '폰트'}>
            <button
              ref={fontBtnRef}
              type="button"
              className="en-field-button"
              data-open={pickerFor === 'font' || undefined}
              onClick={() => handlePickerToggle('font')}
            >
              {t('propertiesPanel.configure') || '설정하기'}
            </button>
          </PropertyRow>

          {/* 폰트 크기 */}
          <PropertyRow label={t('counterSetting.fontSize') || '폰트 크기'}>
            <NumberInput
              visual="text"
              value={counterSettings.fontSize ?? 16}
              onChange={(value) => handleCounterUpdate({ fontSize: value })}
              suffix="px"
              min={8}
              max={72}
              width="54px"
            />
          </PropertyRow>

          {/* 폰트 스타일 */}
          <PropertyRow label={t('counterSetting.fontStyle') || '폰트 스타일'}>
            <FontStyleToggle
              isBold={(counterSettings.fontWeight ?? 400) >= 700}
              isItalic={counterSettings.fontItalic ?? false}
              isUnderline={counterSettings.fontUnderline ?? false}
              isStrikethrough={counterSettings.fontStrikethrough ?? false}
              onBoldChange={(value) =>
                handleCounterUpdate({ fontWeight: value ? 700 : 400 })
              }
              onItalicChange={(value) =>
                handleCounterUpdate({ fontItalic: value })
              }
              onUnderlineChange={(value) =>
                handleCounterUpdate({ fontUnderline: value })
              }
              onStrikethroughChange={(value) =>
                handleCounterUpdate({ fontStrikethrough: value })
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
              checked={counterSettings.animation.enabled}
              onChange={() =>
                handleCounterUpdate({
                  animation: {
                    ...counterSettings.animation,
                    enabled: !counterSettings.animation.enabled,
                  },
                })
              }
            />
          </div>

          <Dimmed off={!counterSettings.animation.enabled}>
            <PropertyRow
              label={t('counterSetting.animation') || '애니메이션 설정'}
            >
              <button
                ref={animationBtnRef}
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

        {pickerFor && pickerFor !== 'font' && (
          <ColorPicker
            open={pickerOpen}
            referenceRef={
              (pickerFor === 'fill'
                ? fillBtnRef
                : strokeBtnRef) as React.RefObject<HTMLElement>
            }
            panelElement={panelElement}
            color={activeColorFor(pickerFor as 'fill' | 'stroke', colorState)}
            onColorChange={(c: string) => handleColorChange(c)}
            onColorChangeComplete={(c: string) => handleColorChangeComplete(c)}
            onClose={() => setPickerFor(null)}
            solidOnly={true}
            interactiveRefs={colorPickerInteractiveRefs}
          />
        )}

        {/* FontPicker */}
        {pickerFor === 'font' && (
          <FontPicker
            open={true}
            referenceRef={fontBtnRef}
            panelElement={panelElement}
            selectedFont={counterSettings.fontFamily || null}
            onFontSelect={(fontName) => {
              handleCounterUpdate({ fontFamily: fontName });
            }}
            onClose={() => setPickerFor(null)}
            onOpenManager={() => {
              setShowFontManager(true);
            }}
            interactiveRefs={[fontBtnRef]}
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

        {showAnimationPicker && counterSettings.animation.enabled && (
          <CounterAnimationPicker
            open={showAnimationPicker}
            referenceRef={animationBtnRef}
            panelElement={panelElement}
            animation={counterSettings.animation}
            counterSettings={counterSettings}
            keyVisual={{
              width: keyPosition.width,
              height: keyPosition.height,
              backgroundColor: keyPosition.backgroundColor,
              borderColor: keyPosition.borderColor,
              borderWidth: keyPosition.borderWidth,
              borderRadius: keyPosition.borderRadius,
              fontColor: keyPosition.fontColor,
              fontSize: keyPosition.fontSize,
              fontWeight: keyPosition.fontWeight,
              fontFamily: keyPosition.fontFamily,
              fontItalic: keyPosition.fontItalic,
              fontUnderline: keyPosition.fontUnderline,
              fontStrikethrough: keyPosition.fontStrikethrough,
              displayText: keyPosition.displayText,
              displayName: keyDisplayName,
              className: keyPosition.className,
              activeBackgroundColor: keyPosition.activeBackgroundColor,
              activeBorderColor: keyPosition.activeBorderColor,
              activeFontColor: keyPosition.activeFontColor,
              useInlineStyles: keyPosition.useInlineStyles,
              isStat,
            }}
            onAnimationChange={handleAnimationUpdate}
            onClose={() => setShowAnimationPicker(false)}
            t={t}
            interactiveRefs={[animationBtnRef]}
          />
        )}
      </FeatureSettings>
      {/* The count itself is kept whether or not it is shown on the key. */}
      {countKey && (
        <KeyCountSettings mode={countKey.mode} keyCode={countKey.key} t={t} />
      )}
    </>
  );
};

export default CounterTabContent;
