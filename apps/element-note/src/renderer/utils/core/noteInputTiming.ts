import { MAX_EVENT_AGE_MS } from '@constants/inputTiming';
import type { NoteKeyTiming } from '@hooks/overlay/useNoteSystem';

/** Keep display correction bounded while retaining physical input duration. */
export function noteInputTiming(
  eventAgeMs: number | undefined,
  holdDurationMs: number | undefined,
  now: number,
): NoteKeyTiming {
  const age =
    typeof eventAgeMs === 'number' && Number.isFinite(eventAgeMs)
      ? Math.max(0, eventAgeMs)
      : 0;
  return {
    displayTime: now - Math.min(age, MAX_EVENT_AGE_MS),
    physTime: now - age,
    holdDurationMs:
      typeof holdDurationMs === 'number' &&
      Number.isFinite(holdDurationMs) &&
      holdDurationMs >= 0
        ? holdDurationMs
        : undefined,
  };
}
