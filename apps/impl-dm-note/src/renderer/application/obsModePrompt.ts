import type { Unsubscribe } from './ports/messagePort';

export interface ObsModePromptSources {
  /** Calls back whenever the game connection becomes ready, at once if it already is. */
  onGameReady(listener: () => void): Unsubscribe;
  obsRunning(): Promise<boolean>;
  inGameViewersEnabled(): boolean;
}

/**
 * In-game viewers mirror the OBS pages, so ImplResourcePack shows nothing
 * while OBS mode is off. When the game connects in that state, ask once per
 * app run whether to turn it on.
 */
export function watchObsModePrompt(
  sources: ObsModePromptSources,
  ask: () => void,
): Unsubscribe {
  let asked = false;
  let checking = false;
  let active = true;
  const unsubscribe = sources.onGameReady(() => {
    if (asked || checking || !sources.inGameViewersEnabled()) return;
    checking = true;
    sources.obsRunning().then(
      (running) => {
        checking = false;
        if (!active || asked || running) return;
        asked = true;
        ask();
      },
      () => {
        checking = false;
      },
    );
  });
  return () => {
    active = false;
    unsubscribe();
  };
}
