/**
 * Key/StatItem 공통 스타일 계산 훅
 * 오버레이에서 키/통계 요소의 스타일을 일관되게 계산
 */

import { resolveImageSource } from '@utils/core/imageSource';

export interface KeyElementPosition {
  hidden?: boolean;
  dx: number;
  dy: number;
  width: number;
  height?: number;
  activeImage?: string;
  inactiveImage?: string;
  activeTransparent?: boolean;
  idleTransparent?: boolean;
  className?: string;
  backgroundColor?: string;
  activeBackgroundColor?: string;
  borderColor?: string;
  activeBorderColor?: string;
  borderWidth?: number;
  borderRadius?: number;
  fontSize?: number;
  fontColor?: string;
  activeFontColor?: string;
  fontFamily?: string;
  idleImageFit?: string;
  activeImageFit?: string;
  imageFit?: string;
  useInlineStyles?: boolean;
  displayText?: string;
  fontWeight?: number;
  fontItalic?: boolean;
  fontUnderline?: boolean;
  fontStrikethrough?: boolean;
  counter?: unknown;
  zIndex?: number;
}

interface KeyElementStylesInput {
  position: KeyElementPosition;
  active: boolean;
  label: string;
}

export interface KeyElementStyles {
  keyStyle: React.CSSProperties;
  imageStyle: React.CSSProperties;
  textStyle: React.CSSProperties;
  inactiveImageSrc: string | null;
  activeImageSrc: string | null;
  currentImageSrc: string | null;
  hasCurrentImage: boolean;
  isTransparent: boolean;
  labelText: string;
  useInline: boolean;
}

export function computeKeyElementStyles({
  position,
  active,
  label,
}: KeyElementStylesInput): KeyElementStyles {
  const {
    dx,
    dy,
    width,
    height = 60,
    activeImage,
    inactiveImage,
    activeTransparent = false,
    idleTransparent = false,
    backgroundColor,
    activeBackgroundColor,
    borderColor,
    activeBorderColor,
    borderWidth,
    borderRadius,
    fontSize,
    fontColor,
    activeFontColor,
    fontFamily,
    idleImageFit,
    activeImageFit,
    imageFit,
    useInlineStyles,
    displayText,
    fontWeight,
    fontItalic,
    fontUnderline,
    fontStrikethrough,
  } = position;

  const labelText = displayText || label;
  const useInline = useInlineStyles === true;

  // 상태별 색상
  // 활성 스타일이 저장되지 않은 v1 키는 활성 기본값으로 표시한다.
  // 대기 색상을 다시 상속하면 상태 신호는 바뀌어도 키가 전혀 눌리지 않은 것처럼
  // 보인다. 사용자가 활성 스타일을 명시한 경우에는 그 값을 그대로 우선한다.
  const stateBackgroundColor = active ? activeBackgroundColor : backgroundColor;
  const stateBorderColor = active ? activeBorderColor : borderColor;
  const stateFontColor = active ? activeFontColor : fontColor;

  // 이미지 소스
  const inactiveImageSrc = resolveImageSource(inactiveImage);
  const activeImageSrc = resolveImageSource(activeImage);

  const isTransparent = active ? activeTransparent : idleTransparent;

  const currentImageSrc =
    (active && activeImageSrc ? activeImageSrc : inactiveImageSrc) || null;
  const hasCurrentImage = !!currentImageSrc;
  const isUsingActiveImage = active && !!activeImageSrc;
  const effectiveImageFit = isUsingActiveImage
    ? activeImageFit || imageFit || 'cover'
    : idleImageFit || imageFit || 'cover';

  // 기본 색상
  const defaultBgColor = hasCurrentImage
    ? 'transparent'
    : active
      ? 'rgba(121, 121, 121, 0.9)'
      : 'rgba(46, 46, 47, 0.9)';
  const defaultBorderColor = active
    ? 'rgba(255, 255, 255, 0.9)'
    : 'rgba(113, 113, 113, 0.9)';
  const defaultTextColor =
    active && !activeImageSrc ? '#FFFFFF' : 'rgba(121, 121, 121, 0.9)';

  const keyStyle: React.CSSProperties = {
    width: `${width}px`,
    height: `${height}px`,
    transform: `translate3d(calc(${dx}px + var(--key-offset-x, 0px)), calc(${dy}px + var(--key-offset-y, 0px)), 0)`,
    backgroundColor:
      useInline && stateBackgroundColor
        ? stateBackgroundColor
        : `var(--key-bg, ${stateBackgroundColor || defaultBgColor})`,
    borderRadius:
      useInline && borderRadius != null
        ? `${borderRadius}px`
        : `var(--key-radius, ${
            borderRadius != null ? `${borderRadius}px` : '10px'
          })`,
    border:
      useInline && (stateBorderColor || borderWidth != null)
        ? `${borderWidth ?? 3}px solid ${
            stateBorderColor || defaultBorderColor
          }`
        : `var(--key-border, ${borderWidth ?? 3}px solid ${
            stateBorderColor || defaultBorderColor
          })`,
    color:
      useInline && stateFontColor
        ? stateFontColor
        : `var(--key-text-color, ${stateFontColor || defaultTextColor})`,
    fontSize: fontSize ? `${fontSize}px` : undefined,
    overflow: 'hidden' as const,
    willChange: active ? 'transform, background-color' : 'transform',
    backfaceVisibility: 'hidden' as const,
    transformStyle: 'preserve-3d' as const,
    contain: 'layout style paint',
    imageRendering: 'auto' as const,
    isolation: 'isolate' as const,
    boxSizing: 'border-box' as const,
    zIndex: position.zIndex,
    cursor: 'default',
  };

  const fallbackImageDimmed = active && !activeImageSrc && !!inactiveImageSrc;
  const imageStyle: React.CSSProperties = {
    width: '100%',
    height: '100%',
    objectFit: effectiveImageFit as React.CSSProperties['objectFit'],
    display: 'block',
    pointerEvents: 'none' as const,
    userSelect: 'none' as const,
    position: 'relative' as const,
    zIndex: 0,
    filter: fallbackImageDimmed ? 'brightness(0.62)' : 'none',
  };

  // 텍스트 데코레이션
  const textDecorations: string[] = [];
  if (fontUnderline) textDecorations.push('underline');
  if (fontStrikethrough) textDecorations.push('line-through');

  const textStyle: React.CSSProperties = {
    willChange: 'auto',
    fontSize: fontSize ? `${fontSize}px` : undefined,
    fontFamily: fontFamily
      ? `"${fontFamily}", "SUIT-Regular", sans-serif`
      : undefined,
    fontWeight: fontWeight ?? 700,
    fontStyle: fontItalic ? ('italic' as const) : ('normal' as const),
    textDecoration:
      textDecorations.length > 0 ? textDecorations.join(' ') : 'none',
  };

  return {
    keyStyle,
    imageStyle,
    textStyle,
    inactiveImageSrc,
    activeImageSrc,
    currentImageSrc,
    hasCurrentImage,
    isTransparent,
    labelText,
    useInline,
  };
}
