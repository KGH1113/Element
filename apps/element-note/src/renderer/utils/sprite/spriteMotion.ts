import {
  SPRITE_CONSTRAINTS,
  type SpriteActivation,
} from '@src/types/key/sprites';
import { cssEasingToBezier, isSameBezier } from '@utils/cubicBezier';

/**
 * How an image moves from one look to another, in the words the sidebar
 * uses. What is stored is a CSS easing and two times in milliseconds; here
 * they become a feel that is chosen by name and a time that is read in
 * seconds. Nothing in the sidebar is typed as code.
 */

/** What an image does when a key of one of its looks is pressed. */
export const SPRITE_REACTIONS = [
  { id: 'whileHeld', labelKey: 'sprite.whileHeld' },
  { id: 'onPress', labelKey: 'sprite.onPress' },
] as const satisfies readonly { id: SpriteActivation; labelKey: string }[];

export interface SpriteFeel {
  id: string;
  /** Names the feel in the sidebar. */
  labelKey: string;
  /** What is stored for it. */
  easing: string;
}

/** The feels that can be chosen. The first one is what a new image has. */
export const SPRITE_FEELS = [
  {
    id: 'smooth',
    labelKey: 'sprite.feelSmooth',
    easing: 'cubic-bezier(0.4, 0, 0.2, 1)',
  },
  {
    id: 'quickStart',
    labelKey: 'sprite.feelQuickStart',
    easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
  },
  {
    id: 'slowStart',
    labelKey: 'sprite.feelSlowStart',
    easing: 'cubic-bezier(0.32, 0, 0.67, 0)',
  },
  { id: 'steady', labelKey: 'sprite.feelSteady', easing: 'linear' },
  {
    id: 'bouncy',
    labelKey: 'sprite.feelBouncy',
    easing: 'cubic-bezier(0.34, 1.56, 0.64, 1)',
  },
] as const satisfies readonly SpriteFeel[];

export type SpriteFeelId = (typeof SPRITE_FEELS)[number]['id'];

/** Stands for a stored easing that is none of the feels. */
export const CUSTOM_SPRITE_FEEL = 'custom';

/**
 * The feel a stored easing is, or null when it is none of them: such an
 * easing is kept as it is until another feel is chosen. The same curve
 * written another way (`linear` as `cubic-bezier(0, 0, 1, 1)`) is that feel.
 */
export function feelOfEasing(
  easing: string,
): (typeof SPRITE_FEELS)[number] | null {
  const curve = cssEasingToBezier(easing);
  if (!curve) return null;
  return (
    SPRITE_FEELS.find((feel) => {
      const own = cssEasingToBezier(feel.easing);
      return own !== null && isSameBezier(own, curve);
    }) ?? null
  );
}

/** What is stored when a feel is chosen. */
export const easingOfFeel = (id: SpriteFeelId): string =>
  SPRITE_FEELS.find((feel) => feel.id === id)!.easing;

const secondsToMs = (seconds: readonly number[]) =>
  seconds.map((value) => Math.round(value * 1000));

/** The times a look can take to appear while its keys are held. */
export const SPRITE_CHANGE_STOPS_MS: readonly number[] = secondsToMs([
  0, 0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.8, 1,
]).filter(
  (ms) =>
    ms >= SPRITE_CONSTRAINTS.transitionMs.min &&
    ms <= SPRITE_CONSTRAINTS.transitionMs.max,
);

/** The times a look can take to go back after one press. */
export const SPRITE_RETURN_STOPS_MS: readonly number[] = secondsToMs([
  0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.8, 1, 1.5, 2, 3, 5,
]).filter(
  (ms) =>
    ms >= SPRITE_CONSTRAINTS.pressDurationMs.min &&
    ms <= SPRITE_CONSTRAINTS.pressDurationMs.max,
);

/** Shorter than this is no time at all to the eye. */
const INSTANT_BELOW_MS = 10;

type Translate = (key: string, options?: Record<string, unknown>) => string;

const secondFormats = new Map<string, Intl.NumberFormat | null>();

/** Writes seconds the way a language does; null when it cannot be asked. */
const secondFormat = (language = ''): Intl.NumberFormat | null => {
  let format = secondFormats.get(language);
  if (format === undefined) {
    try {
      format = new Intl.NumberFormat(language || undefined, {
        maximumFractionDigits: 2,
      });
    } catch {
      format = null;
    }
    secondFormats.set(language, format);
  }
  return format;
};

/** A stored time as it is read: "0.3 sec", or that it takes no time. */
export function spriteTimeText(
  ms: number,
  t: Translate,
  language?: string,
): string {
  if (ms < INSTANT_BELOW_MS) return t('sprite.instant');
  const seconds = ms / 1000;
  const value =
    secondFormat(language)?.format(seconds) ??
    String(Number(seconds.toFixed(2)));
  return t('sprite.seconds', { value });
}
