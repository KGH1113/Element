import {
  useCallback,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type RefObject,
} from 'react';

export type PopupMotionState = 'entering' | 'open' | 'closing';

interface PopupPresenceOptions {
  enabled?: boolean;
  skipEnter?: boolean;
  ready?: boolean;
  motionRef?: RefObject<HTMLElement | null>;
  exitDurationVar?: string;
  fallbackExitMs?: number;
}

export interface PopupPresence {
  mounted: boolean;
  state: PopupMotionState;
  cycle: number;
}

const CLOSED: PopupPresence = { mounted: false, state: 'entering', cycle: 0 };

const parseDuration = (raw: string): number | null => {
  const value = raw.trim();
  const parsed = Number.parseFloat(value);
  if (!Number.isFinite(parsed)) return null;
  if (value.endsWith('ms')) return parsed;
  if (value.endsWith('s')) return parsed * 1000;
  return null;
};

const readMotionDuration = (variable: string, fallbackMs: number): number => {
  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return 0;
  const raw = window
    .getComputedStyle(document.documentElement)
    .getPropertyValue(variable);
  return parseDuration(raw) ?? fallbackMs;
};

/** Keeps popup DOM mounted long enough for reversible enter and exit motion. */
export const usePopupPresence = (
  open: boolean,
  {
    enabled = true,
    skipEnter = false,
    ready = true,
    motionRef,
    exitDurationVar = '--ui-popup-exit-duration',
    fallbackExitMs = 120,
  }: PopupPresenceOptions = {},
): PopupPresence => {
  const initialState: PopupMotionState = skipEnter ? 'open' : 'entering';
  const [presence, setPresence] = useState<PopupPresence>(() =>
    open ? { mounted: true, state: initialState, cycle: 1 } : CLOSED,
  );
  const exitTimerRef = useRef<number | null>(null);

  const clearExitTimer = useCallback(() => {
    if (exitTimerRef.current === null) return;
    window.clearTimeout(exitTimerRef.current);
    exitTimerRef.current = null;
  }, []);

  const { mounted, state } = presence;

  useLayoutEffect(() => {
    if (!enabled) return;
    if (open) {
      clearExitTimer();
      setPresence((previous) =>
        previous.mounted && previous.state !== 'closing'
          ? previous
          : {
              mounted: true,
              state: previous.mounted ? 'open' : initialState,
              cycle: previous.cycle + 1,
            },
      );
      return;
    }

    if (!mounted) return;
    setPresence((previous) => ({ ...previous, state: 'closing' }));
    exitTimerRef.current = window.setTimeout(
      () => {
        exitTimerRef.current = null;
        setPresence((previous) => ({ ...CLOSED, cycle: previous.cycle }));
      },
      readMotionDuration(exitDurationVar, fallbackExitMs),
    );

    return clearExitTimer;
  }, [
    clearExitTimer,
    enabled,
    exitDurationVar,
    fallbackExitMs,
    initialState,
    mounted,
    open,
  ]);

  useLayoutEffect(() => {
    if (!enabled || !open || !mounted || state !== 'entering' || !ready) return;
    const node = motionRef?.current;
    if (node) void window.getComputedStyle(node).opacity;
    else void document.body.offsetHeight;
    setPresence((previous) => ({ ...previous, state: 'open' }));
  }, [enabled, motionRef, mounted, open, ready, state]);

  const bypass = useMemo<PopupPresence>(
    () => ({ mounted: open, state: 'open', cycle: presence.cycle }),
    [open, presence.cycle],
  );

  return enabled ? presence : bypass;
};

export const useModalPresence = (
  open: boolean,
  options: Omit<
    PopupPresenceOptions,
    'exitDurationVar' | 'fallbackExitMs'
  > = {},
): PopupPresence =>
  usePopupPresence(open, {
    ...options,
    exitDurationVar: '--ui-modal-exit-duration',
    fallbackExitMs: 150,
  });
