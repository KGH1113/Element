import { describe, expect, it } from 'vitest';
import { FALLBACK_POSITION } from '@constants/overlayDefaults';
import type { KeyMappings, KeyPositions } from '@src/types/key/keys';
import { createKeyLayoutInbox, type KeyLayout } from './keyLayoutInbox';

const places = (count: number): KeyPositions['tab'] =>
  Array.from({ length: count }, (_, index) => ({
    ...FALLBACK_POSITION,
    dx: index * 70,
  }));

function setup(initial: KeyLayout) {
  let layout = initial;
  const applied: Partial<KeyLayout>[] = [];
  const timers: (() => void)[] = [];
  const inbox = createKeyLayoutInbox({
    read: () => layout,
    apply: (change) => {
      applied.push(change);
      layout = { ...layout, ...change };
    },
    setTimer: (run) => timers.push(run),
    clearTimer: (timer) => {
      timers[(timer as number) - 1] = () => {};
    },
  });
  return {
    inbox,
    applied,
    layout: () => layout,
    /** Something other than the inbox rewrites the layout. */
    replace: (next: KeyLayout) => {
      layout = next;
    },
    runTimers: () => timers.splice(0).forEach((run) => run()),
  };
}

const twoKeys: KeyLayout = {
  keyMappings: { tab: ['A', 'B'] },
  positions: { tab: places(2) },
};
const pastedKeys: KeyMappings = { tab: ['A', 'B', 'A'] };
const pastedPlaces: KeyPositions = { tab: places(3) };

describe('keyLayoutInbox', () => {
  it('applies a paste as one change, never keys without their places', () => {
    const t = setup(twoKeys);

    t.inbox.receiveKeyMappings(pastedKeys);
    expect(t.applied).toEqual([]);

    t.inbox.receivePositions(pastedPlaces);
    expect(t.applied).toEqual([
      { keyMappings: pastedKeys, positions: pastedPlaces },
    ]);
  });

  it('pairs the halves in either order', () => {
    const t = setup(twoKeys);

    t.inbox.receivePositions(pastedPlaces);
    t.inbox.receiveKeyMappings(pastedKeys);

    expect(t.applied).toEqual([
      { keyMappings: pastedKeys, positions: pastedPlaces },
    ]);
  });

  it('applies a half at once when it keeps every count', () => {
    const t = setup(twoKeys);
    const moved = { tab: places(2).map((p) => ({ ...p, dy: 40 })) };
    const rebound = { tab: ['C', 'D'] };

    t.inbox.receivePositions(moved);
    t.inbox.receiveKeyMappings(rebound);

    expect(t.applied).toEqual([{ positions: moved }, { keyMappings: rebound }]);
  });

  it('applies a half alone when the other one never comes', () => {
    const t = setup(twoKeys);

    t.inbox.receiveKeyMappings(pastedKeys);
    t.runTimers();

    expect(t.applied).toEqual([{ keyMappings: pastedKeys }]);
  });

  it('keeps a tab whose counts already differed from waiting forever', () => {
    const t = setup({
      keyMappings: { tab: ['A', 'B', 'C'] },
      positions: { tab: places(2) },
    });
    const moved = { tab: places(2).map((p) => ({ ...p, dy: 40 })) };

    t.inbox.receivePositions(moved);

    expect(t.applied).toEqual([{ positions: moved }]);
  });

  it('drops a waiting half once the layout was replaced', () => {
    const t = setup(twoKeys);
    const snapshot: KeyLayout = {
      keyMappings: { tab: ['X'] },
      positions: { tab: places(1) },
    };

    t.inbox.receiveKeyMappings(pastedKeys);
    t.replace(snapshot);
    t.runTimers();

    expect(t.applied).toEqual([]);
    expect(t.layout()).toBe(snapshot);
  });

  it('lets a newer half of the same kind replace the waiting one', () => {
    const t = setup(twoKeys);
    const pastedTwice = { tab: ['A', 'B', 'A', 'B'] };

    t.inbox.receiveKeyMappings(pastedKeys);
    t.inbox.receiveKeyMappings(pastedTwice);
    t.inbox.receivePositions({ tab: places(4) });

    expect(t.applied).toEqual([
      { keyMappings: pastedTwice, positions: { tab: places(4) } },
    ]);
  });
});
