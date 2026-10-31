import { NOTE_SETTINGS_CONSTRAINTS } from '../../types/settings/noteSettingsConstraints';
import {
  createDefaultCounterSettings,
  type KeyPosition,
} from '../../types/key/keys';

export const FALLBACK_POSITION: KeyPosition = {
  dx: 0,
  dy: 0,
  width: 60,
  height: 60,
  hidden: false,
  activeImage: '',
  inactiveImage: '',
  activeTransparent: false,
  idleTransparent: false,
  count: 0,
  noteColor: '#FFFFFF',
  noteOpacity: 80,
  noteAlignment: 'center',
  noteEffectEnabled: true,
  noteGlowEnabled: false,
  noteGlowSize: 20,
  noteGlowOpacity: 70,
  noteGlowColor: '#FFFFFF',
  noteAutoYCorrection: true,
  className: '',
  counter: createDefaultCounterSettings(),
};

export interface NoteSettings {
  frameLimit: number;
  speed: number;
  trackHeight: number;
  reverse: boolean;
  fadeTopPx: number;
  fadeBottomPx: number;
  reverseFadeTopPx: number;
  reverseFadeBottomPx: number;
  delayedNoteEnabled: boolean;
  shortNoteThresholdMs: number;
  shortNoteMinLengthPx: number;
  keyDisplayDelayMs: number;
}

export const DEFAULT_NOTE_BORDER_RADIUS: number =
  NOTE_SETTINGS_CONSTRAINTS.borderRadius.default;

// 노트 효과 기본 설정
export const DEFAULT_NOTE_SETTINGS: NoteSettings = {
  frameLimit: NOTE_SETTINGS_CONSTRAINTS.frameLimit.default,
  speed: NOTE_SETTINGS_CONSTRAINTS.speed.default,
  trackHeight: NOTE_SETTINGS_CONSTRAINTS.trackHeight.default,
  reverse: false,
  fadeTopPx: NOTE_SETTINGS_CONSTRAINTS.fadeTopPx.default,
  fadeBottomPx: NOTE_SETTINGS_CONSTRAINTS.fadeBottomPx.default,
  reverseFadeTopPx: NOTE_SETTINGS_CONSTRAINTS.reverseFadeTopPx.default,
  reverseFadeBottomPx: NOTE_SETTINGS_CONSTRAINTS.reverseFadeBottomPx.default,
  delayedNoteEnabled: false,
  shortNoteThresholdMs: NOTE_SETTINGS_CONSTRAINTS.shortNoteThresholdMs.default,
  shortNoteMinLengthPx: NOTE_SETTINGS_CONSTRAINTS.shortNoteMinLengthPx.default,
  keyDisplayDelayMs: NOTE_SETTINGS_CONSTRAINTS.keyDisplayDelayMs.default,
};

// 기존 상수(하위 호환성 유지)
export const TRACK_HEIGHT: number = DEFAULT_NOTE_SETTINGS.trackHeight;

// 제약 값 export
export { NOTE_SETTINGS_CONSTRAINTS } from '../../types/settings/noteSettingsConstraints';
