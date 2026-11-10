import { describe, expect, it } from 'vitest';
import { createRenderKeyRouter } from './key-router.mjs';

describe('frozen renderer canonical key routing', () => {
  it('activates an all-match slot only while every member is held', () => {
    const router = createRenderKeyRouter([{ keys: ['A', 'B'], match: 'all' }]);
    expect(router.process('a', ['A'], true, 0)).toEqual([
      { key: 'A+B', transition: null, press: false, holdDurationMs: undefined },
    ]);
    expect(router.process('b', ['B'], true, 20)[0]).toMatchObject({
      transition: true,
      press: true,
    });
    expect(router.process('b', ['B'], false, 50)[0]).toMatchObject({
      transition: false,
      press: false,
      holdDurationMs: 30,
    });
    expect(router.active.size).toBe(0);
  });

  it('counts each fresh any-match press but retains one continuous note', () => {
    const router = createRenderKeyRouter([{ keys: ['A', 'B'], match: 'any' }]);
    expect(router.process('a', ['A'], true, 0)[0]).toMatchObject({
      transition: true,
      press: true,
    });
    expect(router.process('b', ['B'], true, 10)[0]).toMatchObject({
      transition: null,
      press: true,
    });
    expect(router.process('a', ['A'], false, 20)[0]).toMatchObject({
      transition: null,
      press: false,
    });
    // The physical B hold started after canonical DOWN: use canonical timing.
    expect(router.process('b', ['B'], false, 30)[0]).toMatchObject({
      transition: false,
      holdDurationMs: undefined,
    });
  });

  it('deduplicates canonical slots and repeat DOWNs', () => {
    const router = createRenderKeyRouter(['A', 'A']);
    expect(router.process('a', ['A'], true, 3)).toHaveLength(1);
    expect(router.process('a', ['A'], true, 5)).toEqual([]);
    expect(router.process('a', ['A'], false, 10)[0]).toMatchObject({
      holdDurationMs: 7,
    });
    expect(router.process('a', ['A'], false, 12)).toEqual([]);
  });

  it('resolves the first matching physical alias before reevaluating slots', () => {
    const router = createRenderKeyRouter(['+', 'NUMPAD PLUS']);
    expect(
      router
        .process('pad', ['NUMPAD PLUS', '+'], true, 0)
        .map((event) => event.key),
    ).toEqual(['NUMPAD PLUS']);
    expect([...router.active]).toEqual(['NUMPAD PLUS']);
  });

  it('holds overlapping physical keys until the final source releases', () => {
    const router = createRenderKeyRouter(['A']);
    router.process('keyboard-a', ['A'], true, 0);
    expect(router.process('gamepad-a', ['A'], true, 10)[0]).toMatchObject({
      press: true,
      transition: null,
    });
    expect(
      router.process('keyboard-a', ['A'], false, 20)[0].transition,
    ).toBeNull();
    expect(router.process('gamepad-a', ['A'], false, 30)[0]).toMatchObject({
      transition: false,
      holdDurationMs: undefined,
    });
  });

  it('keeps literal plus labels distinct from compounds and empty bindings inert', () => {
    const router = createRenderKeyRouter([
      '',
      '+',
      { keys: ['A', 'B'], match: 'all' },
    ]);
    expect(router.process('plus', ['+'], true, 0)[0]).toMatchObject({
      key: '+',
      transition: true,
    });
    expect(router.process('empty', [''], true, 0)).toEqual([]);
  });
});
