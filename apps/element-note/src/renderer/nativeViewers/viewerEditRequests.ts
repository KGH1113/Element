import type { KeyViewerKind } from '@src/types/key/keys';
import type { Unsubscribe } from '../application/ports/messagePort';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { useKeyStore } from '@stores/data/useKeyStore';

type Listener = (viewerKind: KeyViewerKind) => void;

const listeners = new Set<Listener>();

/** The game asked to edit this viewer; the main window answers (useViewerEditRequests). */
export function requestViewerEdit(viewerKind: KeyViewerKind): void {
  listeners.forEach((listener) => listener(viewerKind));
}

export function onViewerEditRequest(listener: Listener): Unsubscribe {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/**
 * Selects the tab the viewer shows, the way picking it in its tab list does.
 * False when the request must leave the window as it is: before the tabs load,
 * while a dialog is open or a key is being recorded.
 */
export function selectViewerTab(viewerKind: KeyViewerKind): boolean {
  if (
    document.querySelector("[data-dmn-modal-backdrop='true']") ||
    window.__dmn_isKeyListening
  )
    return false;
  const {
    isBootstrapped,
    selectedKeyType,
    selectedViewerTabs,
    setSelectedKeyType,
  } = useKeyStore.getState();
  if (!isBootstrapped) return false;
  const tab = selectedViewerTabs[viewerKind];
  if (tab && tab !== selectedKeyType) {
    // Selection belongs to the tab; the grid clears it only while it is shown.
    useGridSelectionStore.getState().clearSelection();
    setSelectedKeyType(tab);
  }
  return true;
}
