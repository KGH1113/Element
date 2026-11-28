import type { KeyMappings, KeyPositions } from '@src/types/key/keys';
import { useKeyStore } from './useKeyStore';

/**
 * A tab's keys and their places are one layout, but the app sends a change
 * to both as two events: `keys:changed`, then `positions:changed`. Applied
 * one at a time, a viewer would draw a tab with a key that has no place yet.
 *
 * The inbox applies a half at once unless it changes a tab's key count that
 * the other half does not have yet. Such a half waits for the other one and
 * both are applied together. A half nobody completes is applied alone after
 * a short wait, and one the layout moved on from (a full snapshot, a local
 * edit) is dropped.
 */

export interface KeyLayout {
  keyMappings: KeyMappings;
  positions: KeyPositions;
}

type Half = keyof KeyLayout;

const OTHER_HALF: Record<Half, Half> = {
  keyMappings: 'positions',
  positions: 'keyMappings',
};

/** Long enough for the second event, short enough to never feel late. */
export const KEY_LAYOUT_HALF_WAIT_MS = 250;

type TabLists = Record<string, readonly unknown[] | undefined>;

/** True when some tab gets a count that neither it nor the other half had. */
function changesCountAlone(
  incoming: TabLists,
  previous: TabLists,
  other: TabLists,
): boolean {
  const tabs = new Set([...Object.keys(incoming), ...Object.keys(previous)]);
  for (const tab of tabs) {
    const count = incoming[tab]?.length ?? 0;
    if (
      count !== (previous[tab]?.length ?? 0) &&
      count !== (other[tab]?.length ?? 0)
    )
      return true;
  }
  return false;
}

export interface KeyLayoutInboxOptions {
  read: () => KeyLayout;
  apply: (change: Partial<KeyLayout>) => void;
  waitMs?: number;
  setTimer?: (run: () => void, ms: number) => unknown;
  clearTimer?: (timer: unknown) => void;
}

export interface KeyLayoutInbox {
  receiveKeyMappings: (keyMappings: KeyMappings) => void;
  receivePositions: (positions: KeyPositions) => void;
}

export function createKeyLayoutInbox({
  read,
  apply,
  waitMs = KEY_LAYOUT_HALF_WAIT_MS,
  setTimer = (run, ms) => setTimeout(run, ms),
  clearTimer = (timer) => clearTimeout(timer as ReturnType<typeof setTimeout>),
}: KeyLayoutInboxOptions): KeyLayoutInbox {
  let held: {
    half: Half;
    change: Partial<KeyLayout>;
    basis: KeyLayout;
  } | null = null;
  let timer: unknown = null;

  const release = () => {
    if (timer !== null) clearTimer(timer);
    timer = null;
    const last = held;
    held = null;
    return last;
  };

  const stillCurrent = (basis: KeyLayout) => {
    const now = read();
    return (
      now.keyMappings === basis.keyMappings && now.positions === basis.positions
    );
  };

  const receive = (half: Half, change: Partial<KeyLayout>) => {
    const waiting = release();
    if (waiting && waiting.half !== half && stillCurrent(waiting.basis)) {
      apply({ ...waiting.change, ...change });
      return;
    }

    const current = read();
    if (
      !changesCountAlone(
        change[half] as TabLists,
        current[half],
        current[OTHER_HALF[half]],
      )
    ) {
      apply(change);
      return;
    }

    held = { half, change, basis: current };
    timer = setTimer(() => {
      timer = null;
      const late = held;
      held = null;
      if (late && stillCurrent(late.basis)) apply(late.change);
    }, waitMs);
  };

  return {
    receiveKeyMappings: (keyMappings) =>
      receive('keyMappings', { keyMappings }),
    receivePositions: (positions) => receive('positions', { positions }),
  };
}

/** This window's inbox for the key store. */
export const keyLayoutInbox = createKeyLayoutInbox({
  read: () => useKeyStore.getState(),
  apply: (change) => useKeyStore.setState(change),
});
