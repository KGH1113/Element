import type { KeyViewerKind } from '@src/types/key/keys';

/** The views of the editor. Each shows one key viewer of its kind. */
export const VIEWS: KeyViewerKind[] = ['hand', 'foot'];

interface ViewerLike {
  id: string;
  name: string;
  viewerKind: KeyViewerKind;
}

/** The key viewer a view has up: the chosen one, or the first of its kind. */
export function viewerShownIn<T extends ViewerLike>(
  view: KeyViewerKind,
  viewers: readonly T[],
  shownIds: Partial<Record<KeyViewerKind, string>>,
): T | undefined {
  return (
    viewers.find((viewer) => viewer.id === shownIds[view]) ??
    viewers.find((viewer) => viewer.viewerKind === view)
  );
}
