import React, { useState, useRef, useEffect } from 'react';
import type { StyleTabContentProps } from '../types';
import type { ImageFit, KeyPosition } from '@src/types/key/keys';
import {
  PropertyRow,
  NumberInput,
  TextInput,
  FontStyleToggle,
} from '../PropertyInputs';
import ImagePicker from '../../../Modal/content/pickers/ImagePicker';
import ColorPicker from '../../../Modal/content/pickers/ColorPicker';
import FontPicker from '../../../Modal/content/pickers/FontPicker';
import FontManagerModal from '../../../Modal/content/managers/FontManagerModal';
import PropertySection from '../PropertySection';
import { useInspectorPages } from '../inspectorPageContext';
import Checkbox from '../../../common/Checkbox';
import StateSwatches from '../StateSwatches';

// 피커 타겟 타입
type PickerTarget =
  'backgroundColor' | 'borderColor' | 'fontColor' | 'image' | 'font' | null;

type ColorState = 'idle' | 'active';
type StyleColorTarget = 'backgroundColor' | 'borderColor' | 'fontColor';
type ActiveStyleColorProperty =
  'activeBackgroundColor' | 'activeBorderColor' | 'activeFontColor';
type StyleColorProperty =
  | StyleColorTarget
  | 'activeBackgroundColor'
  | 'activeBorderColor'
  | 'activeFontColor';

const StyleTabContent: React.FC<StyleTabContentProps> = ({
  keyIndex,
  keyPosition,
  keyInfo,
  onKeyUpdate,
  onKeyPreview,
  hideDisplayText = false,
  displayTextPlaceholder,
  showImagePicker = false,
  onToggleImagePicker,
  imageButtonRef,
  panelElement,
  useCustomCSS = false,
  t,
}) => {
  const DEFAULT_KEY_BACKGROUND_COLOR = 'rgba(46, 46, 47, 0.9)';
  const DEFAULT_KEY_BORDER_COLOR = 'rgba(113, 113, 113, 0.9)';
  const DEFAULT_KEY_FONT_COLOR = 'rgba(121, 121, 121, 0.9)';
  const DEFAULT_KEY_ACTIVE_BACKGROUND_COLOR = 'rgba(121, 121, 121, 0.9)';
  const DEFAULT_KEY_ACTIVE_BORDER_COLOR = 'rgba(255, 255, 255, 0.9)';
  const DEFAULT_KEY_ACTIVE_FONT_COLOR = '#FFFFFF';

  // 통합 피커 상태
  const [pickerFor, setPickerFor] = useState<PickerTarget>(null);
  const [colorState, setColorState] = useState<ColorState>('idle');

  // 컬러 버튼 refs
  const bgColorBtnRef = useRef<HTMLDivElement>(null);
  // 폰트 버튼 ref
  const fontButtonRef = useRef<HTMLButtonElement>(null);
  // 폰트 관리 모달 상태
  const [showFontManager, setShowFontManager] = useState(false);
  const activeInspectorPage = useInspectorPages()?.activePage;
  useEffect(() => {
    setPickerFor(null);
    setShowFontManager(false);
  }, [activeInspectorPage]);
  const borderColorBtnRef = useRef<HTMLDivElement>(null);
  const fontColorBtnRef = useRef<HTMLDivElement>(null);
  const internalImageButtonRef = useRef<HTMLButtonElement>(null);

  // 로컬 색상 상태 (드래그 중 UI 업데이트용)
  const [localColors, setLocalColors] = useState<
    Record<StyleColorProperty, string>
  >({
    backgroundColor:
      keyPosition.backgroundColor || DEFAULT_KEY_BACKGROUND_COLOR,
    activeBackgroundColor:
      keyPosition.activeBackgroundColor ||
      keyPosition.backgroundColor ||
      DEFAULT_KEY_ACTIVE_BACKGROUND_COLOR,
    borderColor: keyPosition.borderColor || DEFAULT_KEY_BORDER_COLOR,
    activeBorderColor:
      keyPosition.activeBorderColor ||
      keyPosition.borderColor ||
      DEFAULT_KEY_ACTIVE_BORDER_COLOR,
    fontColor: keyPosition.fontColor || DEFAULT_KEY_FONT_COLOR,
    activeFontColor:
      keyPosition.activeFontColor ||
      keyPosition.fontColor ||
      DEFAULT_KEY_ACTIVE_FONT_COLOR,
  });

  // 피커가 닫혀있을 때만 외부 prop과 동기화
  useEffect(() => {
    if (
      !pickerFor ||
      (pickerFor !== 'backgroundColor' &&
        pickerFor !== 'borderColor' &&
        pickerFor !== 'fontColor')
    ) {
      setLocalColors({
        backgroundColor:
          keyPosition.backgroundColor || DEFAULT_KEY_BACKGROUND_COLOR,
        activeBackgroundColor:
          keyPosition.activeBackgroundColor ||
          keyPosition.backgroundColor ||
          DEFAULT_KEY_ACTIVE_BACKGROUND_COLOR,
        borderColor: keyPosition.borderColor || DEFAULT_KEY_BORDER_COLOR,
        activeBorderColor:
          keyPosition.activeBorderColor ||
          keyPosition.borderColor ||
          DEFAULT_KEY_ACTIVE_BORDER_COLOR,
        fontColor: keyPosition.fontColor || DEFAULT_KEY_FONT_COLOR,
        activeFontColor:
          keyPosition.activeFontColor ||
          keyPosition.fontColor ||
          DEFAULT_KEY_ACTIVE_FONT_COLOR,
      });
    }
  }, [
    pickerFor,
    keyPosition.backgroundColor,
    keyPosition.activeBackgroundColor,
    keyPosition.borderColor,
    keyPosition.activeBorderColor,
    keyPosition.fontColor,
    keyPosition.activeFontColor,
    DEFAULT_KEY_BACKGROUND_COLOR,
    DEFAULT_KEY_ACTIVE_BACKGROUND_COLOR,
    DEFAULT_KEY_BORDER_COLOR,
    DEFAULT_KEY_ACTIVE_BORDER_COLOR,
    DEFAULT_KEY_FONT_COLOR,
    DEFAULT_KEY_ACTIVE_FONT_COLOR,
  ]);

  // interactiveRefs
  const colorPickerInteractiveRefs = [
    bgColorBtnRef,
    borderColorBtnRef,
    fontColorBtnRef,
  ] as React.RefObject<HTMLElement>[];

  // 실제 사용할 이미지 버튼 ref (외부에서 제공되면 외부 것 사용)
  const _actualImageButtonRef = imageButtonRef || internalImageButtonRef;

  // 피커 토글 (같은 타겟이면 닫고, 다른 타겟이면 바로 전환)
  const handlePickerToggle = (target: PickerTarget) => {
    setPickerFor((prev) => (prev === target ? null : target));
  };

  // A color has two states, both in its row. Pressing one opens its chooser;
  // pressing the one that is open closes it.
  const handleColorPress = (target: StyleColorTarget, state: ColorState) => {
    if (pickerFor === target && colorState === state) {
      setPickerFor(null);
      return;
    }
    setColorState(state);
    setPickerFor(target);
  };

  const stateSwatches = (
    target: StyleColorTarget,
    ref: React.RefObject<HTMLDivElement | null>,
    label: string,
  ) => (
    <StateSwatches
      ref={ref}
      label={label}
      colors={{
        idle: localColors[target],
        active: localColors[activeColorPropertyFor(target)],
      }}
      open={pickerFor === target ? colorState : null}
      onPress={(state) => handleColorPress(target, state)}
    />
  );

  // 이미지 피커 토글 (외부 핸들러가 있으면 사용, 없으면 내부 상태 사용)
  const _handleImagePickerToggle = () => {
    if (onToggleImagePicker) {
      onToggleImagePicker();
      setPickerFor(null); // 다른 피커 닫기
    } else {
      handlePickerToggle('image');
    }
  };

  const resolveColorProperty = (
    target: StyleColorTarget,
  ): StyleColorProperty => {
    if (colorState !== 'active') return target;
    switch (target) {
      case 'backgroundColor':
        return 'activeBackgroundColor';
      case 'borderColor':
        return 'activeBorderColor';
      case 'fontColor':
        return 'activeFontColor';
      default:
        return target;
    }
  };

  const activeColorPropertyFor = (
    target: StyleColorTarget,
  ): ActiveStyleColorProperty => {
    switch (target) {
      case 'backgroundColor':
        return 'activeBackgroundColor';
      case 'borderColor':
        return 'activeBorderColor';
      case 'fontColor':
        return 'activeFontColor';
    }
  };

  const isNonEmptyString = (value: unknown): value is string =>
    typeof value === 'string' && value.trim().length > 0;

  // 현재 피커 색상값 가져오기
  const colorValueFor = (target: StyleColorTarget): string => {
    return localColors[resolveColorProperty(target)];
  };

  // 드래그 중 로컬 상태만 업데이트
  const handleColorChange = (target: StyleColorTarget, color: string) => {
    const prop = resolveColorProperty(target);
    setLocalColors((prev) => ({ ...prev, [prop]: color }));
  };

  // 드래그 완료 시 부모에게 전달
  const handleColorChangeComplete = (
    target: StyleColorTarget,
    color: string,
  ) => {
    const prop = resolveColorProperty(target);
    setLocalColors((prev) => ({ ...prev, [prop]: color }));

    const updates: Partial<KeyPosition> = {
      [prop]: color,
    } as Partial<KeyPosition>;

    // "idle" 상태에서만 변경했을 때 active 값이 비어 있으면,
    // 현재 표시되던 active 값을 함께 저장해(active가 idle로 덮이는 현상 방지)
    if (colorState !== 'active') {
      const activeProp = activeColorPropertyFor(target);
      const currentActive = keyPosition[activeProp];
      if (!isNonEmptyString(currentActive)) {
        updates[activeProp] = localColors[activeProp];
      }
    }

    onKeyUpdate({ index: keyIndex, ...updates });
  };

  // 스타일 변경 핸들러
  const _handleStyleChange = (
    property: keyof KeyPosition,
    value: KeyPosition[keyof KeyPosition],
  ) => {
    onKeyPreview?.(keyIndex, { [property]: value });
  };

  const handleStyleChangeComplete = (
    property: keyof KeyPosition,
    value: KeyPosition[keyof KeyPosition],
  ) => {
    onKeyUpdate({ index: keyIndex, [property]: value });
  };

  // 이미지 변경 핸들러
  const handleIdleImageChange = (imageUrl: string) => {
    onKeyPreview?.(keyIndex, { inactiveImage: imageUrl });
    onKeyUpdate({ index: keyIndex, inactiveImage: imageUrl });
  };

  const handleActiveImageChange = (imageUrl: string) => {
    onKeyPreview?.(keyIndex, { activeImage: imageUrl });
    onKeyUpdate({ index: keyIndex, activeImage: imageUrl });
  };

  const handleIdleTransparentChange = (checked: boolean) => {
    onKeyPreview?.(keyIndex, { idleTransparent: checked });
    onKeyUpdate({ index: keyIndex, idleTransparent: checked });
  };

  const handleActiveTransparentChange = (checked: boolean) => {
    onKeyPreview?.(keyIndex, { activeTransparent: checked });
    onKeyUpdate({ index: keyIndex, activeTransparent: checked });
  };

  const handleIdleImageReset = () => {
    onKeyPreview?.(keyIndex, { inactiveImage: '' });
    onKeyUpdate({ index: keyIndex, inactiveImage: '' });
  };

  const handleActiveImageReset = () => {
    onKeyPreview?.(keyIndex, { activeImage: '' });
    onKeyUpdate({ index: keyIndex, activeImage: '' });
  };

  const handleIdleImageFitChange = (fit: ImageFit) => {
    onKeyPreview?.(keyIndex, { idleImageFit: fit });
    onKeyUpdate({ index: keyIndex, idleImageFit: fit });
  };

  const handleActiveImageFitChange = (fit: ImageFit) => {
    onKeyPreview?.(keyIndex, { activeImageFit: fit });
    onKeyUpdate({ index: keyIndex, activeImageFit: fit });
  };

  // 표시 텍스트 핸들러
  const handleDisplayTextChange = (value: string) => {
    onKeyPreview?.(keyIndex, { displayText: value });
  };

  const handleDisplayTextBlur = () => {
    onKeyUpdate({
      index: keyIndex,
      displayText: keyPosition.displayText || '',
    });
  };

  // 클래스명 핸들러
  const handleClassNameChange = (value: string) => {
    onKeyPreview?.(keyIndex, { className: value });
  };

  const handleClassNameBlur = () => {
    onKeyUpdate({ index: keyIndex, className: keyPosition.className || '' });
  };

  // 이미지 피커 열림 상태 (외부 또는 내부)
  const _isImagePickerOpen = onToggleImagePicker
    ? showImagePicker
    : pickerFor === 'image';

  const imagePicker = showImagePicker &&
    onToggleImagePicker &&
    imageButtonRef && (
      <ImagePicker
        open={showImagePicker}
        referenceRef={imageButtonRef}
        panelElement={panelElement}
        idleImage={keyPosition.inactiveImage || ''}
        activeImage={keyPosition.activeImage || ''}
        idleTransparent={keyPosition.idleTransparent ?? false}
        activeTransparent={keyPosition.activeTransparent ?? false}
        idleImageFit={
          keyPosition.idleImageFit ?? keyPosition.imageFit ?? 'cover'
        }
        activeImageFit={
          keyPosition.activeImageFit ?? keyPosition.imageFit ?? 'cover'
        }
        onIdleImageChange={handleIdleImageChange}
        onActiveImageChange={handleActiveImageChange}
        onIdleTransparentChange={handleIdleTransparentChange}
        onActiveTransparentChange={handleActiveTransparentChange}
        onIdleImageFitChange={handleIdleImageFitChange}
        onActiveImageFitChange={handleActiveImageFitChange}
        onIdleImageReset={handleIdleImageReset}
        onActiveImageReset={handleActiveImageReset}
        onClose={onToggleImagePicker}
      />
    );

  const colorPicker = pickerFor &&
    pickerFor !== 'image' &&
    pickerFor !== 'font' && (
      <ColorPicker
        open
        referenceRef={
          (pickerFor === 'backgroundColor'
            ? bgColorBtnRef
            : pickerFor === 'borderColor'
              ? borderColorBtnRef
              : fontColorBtnRef) as React.RefObject<HTMLElement>
        }
        panelElement={panelElement}
        color={colorValueFor(pickerFor)}
        onColorChange={(color: string) => handleColorChange(pickerFor, color)}
        onColorChangeComplete={(color: string) =>
          handleColorChangeComplete(pickerFor, color)
        }
        onClose={() => setPickerFor(null)}
        solidOnly
        interactiveRefs={colorPickerInteractiveRefs}
      />
    );

  return (
    <>
      {/* The look of the key, in three cards: its colors, its form, a picture. */}
      <PropertySection id="style-shape" title={t('partPage.color')} defaultOpen>
        {/* 배경색 */}
        <PropertyRow label={t('propertiesPanel.backgroundColor') || '배경색'}>
          {stateSwatches(
            'backgroundColor',
            bgColorBtnRef,
            t('propertiesPanel.backgroundColor') || '배경색',
          )}
        </PropertyRow>

        {/* 테두리 색상 */}
        <PropertyRow label={t('propertiesPanel.borderColor') || '테두리 색상'}>
          {stateSwatches(
            'borderColor',
            borderColorBtnRef,
            t('propertiesPanel.borderColor') || '테두리 색상',
          )}
        </PropertyRow>
        {pickerFor !== 'fontColor' && colorPicker}
      </PropertySection>

      <PropertySection id="style-form" title={t('partPage.shape')}>
        {/* 테두리 두께 */}
        <PropertyRow label={t('propertiesPanel.borderWidth') || '테두리 두께'}>
          <NumberInput
            visual="stroke"
            value={keyPosition.borderWidth ?? 3}
            onChange={(value) =>
              handleStyleChangeComplete('borderWidth', value)
            }
            suffix="px"
            min={0}
            max={20}
            allowDecimal
            decimalScale={1}
          />
        </PropertyRow>

        {/* 모서리 반경 */}
        <PropertyRow label={t('propertiesPanel.borderRadius') || '모서리 반경'}>
          <NumberInput
            visual="corner"
            value={keyPosition.borderRadius ?? 10}
            onChange={(value) =>
              handleStyleChangeComplete('borderRadius', value)
            }
            suffix="px"
            min={0}
            max={100}
            allowDecimal
            decimalScale={1}
          />
        </PropertyRow>
      </PropertySection>

      {/* 커스텀 이미지 - 단일 선택 모드에서만 표시 */}
      {onToggleImagePicker && imageButtonRef && (
        <PropertySection id="style-image" title={t('partPage.image')}>
          <PropertyRow
            label={t('propertiesPanel.customImage') || '커스텀 이미지'}
          >
            <button
              ref={imageButtonRef}
              type="button"
              className="en-field-button"
              data-open={showImagePicker || undefined}
              onClick={onToggleImagePicker}
            >
              {t('propertiesPanel.configure') || '설정하기'}
            </button>
          </PropertyRow>
          {imagePicker}
        </PropertySection>
      )}

      <PropertySection
        id="style-text"
        title={t('propertiesPanel.sectionText') || 'Text on the key'}
      >
        {/* 표시 텍스트 */}
        {!hideDisplayText && (
          <PropertyRow
            label={t('propertiesPanel.displayText') || '표시 텍스트'}
          >
            <TextInput
              value={keyPosition.displayText || ''}
              onChange={handleDisplayTextChange}
              onBlur={handleDisplayTextBlur}
              placeholder={displayTextPlaceholder ?? keyInfo?.displayName ?? ''}
              width="54px"
            />
          </PropertyRow>
        )}

        {/* 폰트 */}
        <PropertyRow label={t('propertiesPanel.font') || '폰트'}>
          <button
            ref={fontButtonRef}
            type="button"
            className="en-field-button"
            data-open={pickerFor === 'font' || undefined}
            onClick={() => handlePickerToggle('font')}
          >
            {t('propertiesPanel.configure') || '설정하기'}
          </button>
        </PropertyRow>

        {/* 글꼴 크기 */}
        <PropertyRow label={t('propertiesPanel.fontSize') || '글꼴 크기'}>
          <NumberInput
            visual="text"
            value={keyPosition.fontSize ?? 14}
            onChange={(value) => handleStyleChangeComplete('fontSize', value)}
            suffix="px"
            min={8}
            max={72}
            allowDecimal
            decimalScale={1}
          />
        </PropertyRow>

        {/* 글꼴 색상 */}
        <PropertyRow label={t('propertiesPanel.fontColor') || '글꼴 색상'}>
          {stateSwatches(
            'fontColor',
            fontColorBtnRef,
            t('propertiesPanel.fontColor') || '글꼴 색상',
          )}
        </PropertyRow>

        {/* 글꼴 스타일 */}
        <PropertyRow label={t('propertiesPanel.fontStyle') || '글꼴 스타일'}>
          <FontStyleToggle
            isBold={(keyPosition.fontWeight ?? 700) >= 700}
            isItalic={keyPosition.fontItalic ?? false}
            isUnderline={keyPosition.fontUnderline ?? false}
            isStrikethrough={keyPosition.fontStrikethrough ?? false}
            onBoldChange={(value) =>
              handleStyleChangeComplete('fontWeight', value ? 700 : 400)
            }
            onItalicChange={(value) =>
              handleStyleChangeComplete('fontItalic', value)
            }
            onUnderlineChange={(value) =>
              handleStyleChangeComplete('fontUnderline', value)
            }
            onStrikethroughChange={(value) =>
              handleStyleChangeComplete('fontStrikethrough', value)
            }
          />
        </PropertyRow>
        {pickerFor === 'fontColor' && colorPicker}
        {pickerFor === 'font' && (
          <FontPicker
            open
            referenceRef={fontButtonRef}
            panelElement={panelElement}
            selectedFont={keyPosition.fontFamily || null}
            onFontSelect={(fontName) =>
              handleStyleChangeComplete('fontFamily', fontName)
            }
            onClose={() => setPickerFor(null)}
            onOpenManager={() => setShowFontManager(true)}
            interactiveRefs={[fontButtonRef]}
          />
        )}
        {showFontManager && (
          <FontManagerModal
            isOpen={showFontManager}
            onClose={() => setShowFontManager(false)}
            t={t}
          />
        )}
      </PropertySection>

      {/* 커스텀 CSS 활성화 시에만 클래스명 및 CSS 우선순위 표시 */}
      {useCustomCSS && (
        <PropertySection
          id="style-css"
          title={t('propertiesPanel.sectionCss') || 'Custom CSS'}
        >
          {/* CSS 우선순위 토글 */}
          <div className="dmn-property-row flex min-h-[32px] w-full items-center justify-between">
            <p className="dmn-property-row__label">
              {t('propertiesPanel.useInlineStyles') || '인라인 스타일 우선'}
            </p>
            <Checkbox
              checked={keyPosition.useInlineStyles ?? false}
              onChange={() =>
                handleStyleChangeComplete(
                  'useInlineStyles',
                  !(keyPosition.useInlineStyles ?? false),
                )
              }
            />
          </div>

          {/* 클래스명 */}
          <PropertyRow label={t('propertiesPanel.className') || '클래스'}>
            <TextInput
              value={keyPosition.className || ''}
              onChange={handleClassNameChange}
              onBlur={handleClassNameBlur}
              placeholder="className"
              width="90px"
            />
          </PropertyRow>
        </PropertySection>
      )}
    </>
  );
};

export default StyleTabContent;
