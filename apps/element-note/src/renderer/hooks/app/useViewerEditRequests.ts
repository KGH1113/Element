import { useEffect, useRef } from 'react';
import {
  onViewerEditRequest,
  selectViewerTab,
} from '@src/renderer/nativeViewers/viewerEditRequests';

/**
 * Main window only: the game's Edit in ElementNote opens the key grid on the
 * tab that viewer shows. The window itself comes forward from the IPC side.
 */
export function useViewerEditRequests(
  isSettingsOpen: boolean,
  closeSettings: () => void,
) {
  const latest = useRef({ isSettingsOpen, closeSettings });
  latest.current = { isSettingsOpen, closeSettings };

  useEffect(
    () =>
      onViewerEditRequest((viewerKind) => {
        if (!selectViewerTab(viewerKind)) return;
        if (latest.current.isSettingsOpen) latest.current.closeSettings();
      }),
    [],
  );
}
