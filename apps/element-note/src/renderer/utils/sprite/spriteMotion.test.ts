import { describe, expect, it } from 'vitest';
import {
  DEFAULT_SPRITE_TRANSITION_EASING,
  SPRITE_CONSTRAINTS,
} from '@src/types/key/sprites';
import { validateSpriteDraft } from '@components/main/Modal/content/editors/spriteEditorDraft';
import { makeSpritePosition } from './spriteFixtures';
import {
  SPRITE_CHANGE_STOPS_MS,
  SPRITE_FEELS,
  SPRITE_RETURN_STOPS_MS,
  easingOfFeel,
  feelOfEasing,
  spriteTimeText,
} from './spriteMotion';
import { resolveSpriteRenderEasing } from './spriteReach';

const t = (key: string, options?: Record<string, unknown>) =>
  options?.value !== undefined ? `${key}:${options.value}` : key;

describe('the feel of an image', () => {
  it('is stored as its easing and read back as the same feel', () => {
    for (const feel of SPRITE_FEELS) {
      expect(easingOfFeel(feel.id)).toBe(feel.easing);
      expect(feelOfEasing(feel.easing)?.id).toBe(feel.id);
    }
    // No two feels are the same curve.
    expect(new Set(SPRITE_FEELS.map((feel) => feel.easing)).size).toBe(
      SPRITE_FEELS.length,
    );
  });

  it('is what a new image has', () => {
    expect(feelOfEasing(DEFAULT_SPRITE_TRANSITION_EASING)?.id).toBe(
      SPRITE_FEELS[0].id,
    );
  });

  it('is found for the same curve written another way', () => {
    expect(feelOfEasing('cubic-bezier(.4,0,.2,1)')?.id).toBe('smooth');
    expect(feelOfEasing(' CUBIC-BEZIER(0.40, 0.0, 0.20, 1.0) ')?.id).toBe(
      'smooth',
    );
    expect(feelOfEasing('cubic-bezier(0, 0, 1, 1)')?.id).toBe('steady');
    expect(feelOfEasing('LINEAR')?.id).toBe('steady');
  });

  it('is none of them for any other stored easing', () => {
    for (const easing of [
      'ease',
      'ease-in-out',
      'cubic-bezier(0.1, 0.9, 0.2, 1)',
      'steps(4, end)',
      'linear(0, 0.5, 1)',
      '',
      'not an easing',
    ]) {
      expect(feelOfEasing(easing)).toBeNull();
    }
  });

  it('is an easing the image can be saved and drawn with', () => {
    for (const feel of SPRITE_FEELS) {
      expect(resolveSpriteRenderEasing(feel.easing)).toBe(feel.easing);
      expect(
        validateSpriteDraft(
          makeSpritePosition({ id: 'cat', transitionEasing: feel.easing }),
        ),
      ).toBeNull();
    }
  });
});

describe('the times of an image', () => {
  it('can only be set to times the image may have', () => {
    const { transitionMs, pressDurationMs } = SPRITE_CONSTRAINTS;
    for (const ms of SPRITE_CHANGE_STOPS_MS) {
      expect(Number.isInteger(ms)).toBe(true);
      expect(ms).toBeGreaterThanOrEqual(transitionMs.min);
      expect(ms).toBeLessThanOrEqual(transitionMs.max);
    }
    for (const ms of SPRITE_RETURN_STOPS_MS) {
      expect(Number.isInteger(ms)).toBe(true);
      expect(ms).toBeGreaterThanOrEqual(pressDurationMs.min);
      expect(ms).toBeLessThanOrEqual(pressDurationMs.max);
    }
    expect([...SPRITE_CHANGE_STOPS_MS]).toEqual(
      [...SPRITE_CHANGE_STOPS_MS].sort((a, b) => a - b),
    );
    expect([...SPRITE_RETURN_STOPS_MS]).toEqual(
      [...SPRITE_RETURN_STOPS_MS].sort((a, b) => a - b),
    );
  });

  it('are read in seconds, never in milliseconds', () => {
    expect(spriteTimeText(300, t, 'en')).toBe('sprite.seconds:0.3');
    expect(spriteTimeText(90, t, 'en')).toBe('sprite.seconds:0.09');
    expect(spriteTimeText(1500, t, 'en')).toBe('sprite.seconds:1.5');
    expect(spriteTimeText(5000, t, 'en')).toBe('sprite.seconds:5');
    // The decimal mark is the language's.
    expect(spriteTimeText(250, t, 'ru')).toBe('sprite.seconds:0,25');
    // A language that cannot be asked still gets a number.
    expect(spriteTimeText(250, t, 'no such language!')).toBe(
      'sprite.seconds:0.25',
    );
  });

  it('say that no time at all is instant', () => {
    expect(spriteTimeText(0, t, 'en')).toBe('sprite.instant');
    expect(spriteTimeText(1, t, 'en')).toBe('sprite.instant');
  });
});
