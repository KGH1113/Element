import { describe, expect, it } from 'vitest';
import { noteInputTiming } from './noteInputTiming';

describe('note input timing', () => {
  it('clamps display recovery without changing physical input or hold duration', () => {
    expect(noteInputTiming(400, 80, 1000)).toEqual({
      displayTime: 750,
      physTime: 600,
      holdDurationMs: 80,
    });
  });

  it('keeps malformed clock values out of note buffers and preserves zero holds', () => {
    for (const age of [NaN, Infinity, -100, undefined]) {
      expect(noteInputTiming(age, 0, 1000)).toEqual({
        displayTime: 1000,
        physTime: 1000,
        holdDurationMs: 0,
      });
    }
    for (const hold of [NaN, Infinity, -1, undefined]) {
      expect(noteInputTiming(10, hold, 1000)).toEqual({
        displayTime: 990,
        physTime: 990,
        holdDurationMs: undefined,
      });
    }
  });
});
