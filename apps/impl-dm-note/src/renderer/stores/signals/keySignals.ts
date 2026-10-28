import { signal, type Signal } from '@preact/signals-react';

// globalKey -> Signal<boolean> 매핑
const keySignals = new Map<string, Signal<boolean>>();

// globalKey -> when it was last pressed, as an increasing sequence number.
// A key and its ghost share one element; the newer press is the one shown.
const pressOrder = new Map<string, number>();
let pressSequence = 0;

export function getKeySignal(key: string): Signal<boolean> {
  let s = keySignals.get(key);
  if (!s) {
    s = signal(false);
    keySignals.set(key, s);
  }
  return s;
}

export function setKeyActive(key: string, isActive: boolean) {
  const keySignal = getKeySignal(key);
  // Recorded before the signal changes, so subscribers re-render with it.
  if (isActive && !keySignal.value) pressOrder.set(key, ++pressSequence);
  keySignal.value = isActive;
}

/** 0 when the key was never pressed. */
export function getKeyPressOrder(key: string): number {
  return pressOrder.get(key) ?? 0;
}

export function resetAllKeySignals() {
  for (const s of keySignals.values()) s.value = false;
  pressOrder.clear();
}
