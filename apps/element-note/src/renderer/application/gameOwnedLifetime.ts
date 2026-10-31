import type {
  MessagePort,
  SchedulerPort,
  Unsubscribe,
} from './ports/messagePort';

/** A game restart or mod reload reconnects well within this window. */
export const GAME_EXIT_GRACE_MS = 5_000;

/**
 * An app Element opened together with ADOFAI quits once the game
 * has been gone for the grace period. Before the first connection nothing
 * happens, so a slow game start never closes the app.
 */
export function quitWhenGameLeaves(
  messages: Pick<MessagePort, 'onState'>,
  scheduler: SchedulerPort,
  quit: () => void,
  graceMs = GAME_EXIT_GRACE_MS,
): Unsubscribe {
  let connected = false;
  let pending: Unsubscribe | undefined;
  const cancel = () => {
    pending?.();
    pending = undefined;
  };
  const unsubscribe = messages.onState((state) => {
    if (state === 'ready') {
      connected = true;
      cancel();
      return;
    }
    // 'disposed' is this app shutting its own connection down.
    if (!connected || pending || state === 'disposed') return;
    pending = scheduler.delay(() => {
      pending = undefined;
      quit();
    }, graceMs);
  });
  return () => {
    cancel();
    unsubscribe();
  };
}
