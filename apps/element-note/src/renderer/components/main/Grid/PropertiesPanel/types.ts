import type { KeyPosition } from '@src/types/key/keys';

// ============================================================================
// 탭 상수
// ============================================================================

export const TABS = {
  INPUT: 'input',
  STYLE: 'style',
  NOTE: 'note',
  COUNTER: 'counter',
} as const;

export type TabType = (typeof TABS)[keyof typeof TABS];

export type InspectorAction = 'input' | 'layout' | 'style' | 'note' | 'counter';

// ============================================================================
// 컴포넌트 Props 타입
// ============================================================================

export interface PropertiesPanelProps {
  onPositionChange: (index: number, dx: number, dy: number) => void;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  onKeyMappingChange?: (index: number, newKey: string) => void;
}

export interface PropertyRowProps {
  label: string;
  children: React.ReactNode;
}

export interface NumberInputProps {
  value: number | string;
  onChange: (value: number) => void;
  onBlur?: () => void;
  min?: number;
  max?: number;
  prefix?: string;
  suffix?: string;
  width?: string;
  allowDecimal?: boolean;
  decimalScale?: number;
  step?: number;
  ariaLabel?: string;
  disabled?: boolean;
  isMixed?: boolean;
  mixedPlaceholder?: string;
  /**
   * What the number stands for. On a sidebar page a number with a small range
   * is also a slider, and this is drawn beside it, following the value.
   */
  visual?: import('./RangeTrack').RangeVisual;
}

export interface OptionalNumberInputProps {
  value?: number;
  onChange: (value?: number) => void;
  onBlur?: () => void;
  min?: number;
  max?: number;
  prefix?: string;
  suffix?: string;
  width?: string;
  placeholder?: string;
  allowNegative?: boolean;
  allowDecimal?: boolean;
  decimalScale?: number;
  step?: number;
  ariaLabel?: string;
  disabled?: boolean;
  isMixed?: boolean;
  mixedPlaceholder?: string;
}

export interface ColorInputProps {
  value: string;
  onChange: (value: string) => void;
  onChangeComplete?: (value: string) => void;
  activeValue?: string;
  onActiveChange?: (value: string) => void;
  onActiveChangeComplete?: (value: string) => void;
  showStateTabs?: boolean;
  stateMode?: 'idle' | 'active';
  onStateModeChange?: (mode: 'idle' | 'active') => void;
  /** What is colored; names the two states of a color that has both. */
  label?: string;
  /** With both states: the selected elements do not agree on that color. */
  mixed?: { idle?: boolean; active?: boolean };
  colorId?: string;
  solidOnly?: boolean;
  panelElement?: HTMLElement | null;
  // 외부에서 열림 상태를 제어할 때 사용
  isOpen?: boolean;
  onToggle?: () => void;
}

export interface SelectInputProps {
  value: string;
  options: { value: string; label: string }[];
  onChange: (value: string) => void;
}

export interface ToggleSwitchProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
}

export interface TextInputProps {
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  width?: string;
  isMixed?: boolean;
  ariaLabel?: string;
  disabled?: boolean;
}

export interface TabButtonProps {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}

export interface TabsProps {
  activeTab: TabType;
  onTabChange: (tab: TabType) => void;
  t: (key: string) => string;
  availableTabs?: TabType[];
}

// 글꼴 스타일 토글 Props
export interface FontStyleToggleProps {
  isBold: boolean;
  isItalic: boolean;
  isUnderline: boolean;
  isStrikethrough: boolean;
  onBoldChange: (value: boolean) => void;
  onItalicChange: (value: boolean) => void;
  onUnderlineChange: (value: boolean) => void;
  onStrikethroughChange: (value: boolean) => void;
}

// ============================================================================
// 단일 키 콘텐츠 Props (단일 선택 및 개별 편집 모드에서 재사용)
// ============================================================================

export interface SingleKeyContentProps {
  // 키 데이터
  keyIndex: number;
  keyPosition: KeyPosition;
  keyCode: string | null;
  keyInfo: { globalKey: string; displayName: string } | null;

  // 탭 상태
  activeTab: TabType;

  // 핸들러
  onPositionChange: (index: number, dx: number, dy: number) => void;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  onKeyMappingChange?: (index: number, newKey: string) => void;

  // 키 리스닝 상태 (옵션 - 개별 편집 모드에서는 사용하지 않음)
  isListening?: boolean;
  onKeyListen?: () => void;

  // 이미지 픽커 상태 (옵션 - 개별 편집 모드에서는 사용하지 않음)
  showImagePicker?: boolean;
  onToggleImagePicker?: () => void;
  imageButtonRef?: React.RefObject<HTMLButtonElement>;

  // 커스텀 CSS 설정
  useCustomCSS?: boolean;

  // 번역 함수
  t: (key: string) => string;
}

// ============================================================================
// 탭 콘텐츠 공통 Props
// ============================================================================

export interface StyleTabContentProps {
  keyIndex: number;
  keyPosition: KeyPosition;
  keyCode: string | null;
  keyInfo: { globalKey: string; displayName: string } | null;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  // 표시 텍스트 입력 숨김 (통계 요소는 statType이 displayText 역할)
  hideDisplayText?: boolean;
  // 표시 텍스트가 비었을 때 보여 줄 값 (고스트 키는 원래 키 이름을 따른다)
  displayTextPlaceholder?: string;
  showImagePicker?: boolean;
  onToggleImagePicker?: () => void;
  imageButtonRef?: React.RefObject<HTMLButtonElement>;
  panelElement?: HTMLElement | null;
  useCustomCSS?: boolean;
  t: (key: string) => string;
}

export interface NoteTabContentProps {
  keyIndex: number;
  keyPosition: KeyPosition;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  onKeyPreview?: (index: number, updates: Partial<KeyPosition>) => void;
  /** Draws a look on the canvas while it is pointed at, or stops (null). */
  onChoicePreview?: (changes: Partial<KeyPosition> | null) => void;
  panelElement?: HTMLElement | null;
  t: (key: string) => string;
}

export interface CounterTabContentProps {
  keyIndex: number;
  keyPosition: KeyPosition;
  keyDisplayName?: string;
  isStat?: boolean;
  onKeyUpdate: (data: Partial<KeyPosition> & { index: number }) => void;
  /** Draws a look on the canvas while it is pointed at, or stops (null). */
  onChoicePreview?: (changes: Partial<KeyPosition> | null) => void;
  /**
   * The keyboard key the count is of, in its key viewer: the page ends with
   * that count, which can be changed by hand. Absent for a stat or a face
   * with no key.
   */
  countKey?: { mode: string; key: string };
  panelElement?: HTMLElement | null;
  t: (key: string) => string;
}

// ============================================================================
// 키 데이터 타입
// ============================================================================

export interface KeyData {
  index: number;
  position: KeyPosition | undefined;
  keyCode: string | null;
  keyInfo: { globalKey: string; displayName: string } | null;
}

// ============================================================================
// 레이어 패널 타입
// ============================================================================

export interface LayerItem {
  type: 'key' | 'stat' | 'graph' | 'knob' | 'sprite' | 'plugin';
  id: string;
  index?: number;
  name: string;
  zIndex: number;
  hidden: boolean;
  groupId?: string;
}

export interface GroupHeaderItem {
  displayType: 'group-header';
  groupId: string;
  groupName: string;
  isCollapsed: boolean;
  childCount: number;
  allHidden: boolean;
}

export interface LayerDisplayItem {
  displayType: 'layer';
  item: LayerItem;
  groupDepth: number;
  flatIndex: number;
}

export type DisplayItem = GroupHeaderItem | LayerDisplayItem;
