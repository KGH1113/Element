import { describe, expect, it } from 'vitest';
import {
  NOTE_SETTINGS_CONSTRAINTS,
  clampValue,
} from './noteSettingsConstraints';
import { NOTE_SETTINGS_DEFAULTS, noteSettingsSchema } from './noteSettings';

describe('key display delay constraints', () => {
  it('uses the 30000ms upper bound for UI clamping', () => {
    expect(NOTE_SETTINGS_CONSTRAINTS.keyDisplayDelayMs).toMatchObject({
      min: 0,
      max: 30000,
    });
    expect(clampValue(-1, 'keyDisplayDelayMs')).toBe(0);
    expect(clampValue(30000, 'keyDisplayDelayMs')).toBe(30000);
    expect(clampValue(30001, 'keyDisplayDelayMs')).toBe(30000);
  });

  it('uses the same upper bound in note settings validation', () => {
    expect(
      noteSettingsSchema.safeParse({
        ...NOTE_SETTINGS_DEFAULTS,
        keyDisplayDelayMs: 30000,
      }).success,
    ).toBe(true);
    expect(
      noteSettingsSchema.safeParse({
        ...NOTE_SETTINGS_DEFAULTS,
        keyDisplayDelayMs: 30001,
      }).success,
    ).toBe(false);
    expect(
      noteSettingsSchema.safeParse({
        ...NOTE_SETTINGS_DEFAULTS,
        keyDisplayDelayMs: -1,
      }).success,
    ).toBe(false);
  });
});
