import { useEffect, useState } from 'react';
import { appApi, windowApi } from '@api/modules/appApi';
import { obsApi } from '@api/modules/obsApi';
import { watchObsModePrompt } from '@src/renderer/application/obsModePrompt';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import { setObsModeRunning } from '@utils/obsMode';

/** Main window only: the question to turn on OBS mode for in-game viewers. */
export function useObsModePrompt(onEnableFailed: () => void) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const stopWatching = watchObsModePrompt(
      {
        onGameReady: (listener) => {
          if (useKeyLimiterStatusStore.getState().connectionState === 'ready')
            listener();
          return useKeyLimiterStatusStore.subscribe((state, previous) => {
            if (
              state.connectionState === 'ready' &&
              previous.connectionState !== 'ready'
            )
              listener();
          });
        },
        obsRunning: async () => (await obsApi.status()).running,
        inGameViewersEnabled: () =>
          useSettingsStore.getState().inGameViewersEnabled,
      },
      () => {
        setOpen(true);
        void revealForGameLaunch();
      },
    );
    // Turning OBS mode on in Settings answers the question as well.
    const stopStatus = obsApi.onStatus((status) => {
      if (status.running) setOpen(false);
    });
    return () => {
      stopWatching();
      stopStatus();
    };
  }, []);

  return {
    open,
    enable: () => {
      setOpen(false);
      setObsModeRunning(true).catch((error) => {
        console.error('Failed to turn on OBS mode', error);
        onEnableFailed();
      });
    },
    dismiss: () => setOpen(false),
  };
}

// A game launch starts in the background; the question needs the window.
async function revealForGameLaunch(): Promise<void> {
  const launch = await appApi.launchContext().catch(() => null);
  if (launch?.launchedByGame) await windowApi.showMain().catch(() => undefined);
}
