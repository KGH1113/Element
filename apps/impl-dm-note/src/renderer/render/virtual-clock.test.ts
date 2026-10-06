import { describe, expect, it } from 'vitest';
import { VirtualClock } from './virtual-clock.mjs';

describe('render timer progress', () => {
  it('advances nested zero-delay retries instead of starving the frame', () => {
    const clock = new VirtualClock();
    let calls = 0;
    const retry = () => {
      calls++;
      if (clock.nowMs < 1) clock.schedule(retry, 0);
    };
    clock.schedule(retry, 0);
    clock.advanceTo(16);
    expect(calls).toBe(6);
    expect(clock.timers.size).toBe(0);
    expect(clock.nowMs).toBe(16);
  });

  it('bounds zero-delay intervals while preserving cancellation', () => {
    const clock = new VirtualClock();
    let calls = 0;
    const id = clock.schedule(() => calls++, 0, true);
    clock.advanceTo(1000);
    expect(calls).toBeGreaterThan(0);
    expect(calls).toBeLessThan(1000);
    clock.timers.delete(id);
    const before = calls;
    clock.advanceTo(2000);
    expect(calls).toBe(before);
  });

  it('preserves deadlines and insertion order for ordinary timers', () => {
    const clock = new VirtualClock();
    const calls: number[] = [];
    clock.schedule(() => calls.push(clock.nowMs), 0.25);
    clock.schedule(() => calls.push(clock.nowMs + 1), 0.25);
    clock.advanceTo(1);
    expect(calls).toEqual([0.25, 1.25]);
  });
});
