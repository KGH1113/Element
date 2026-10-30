import type { KeyViewerKind } from '@src/types/key/keys';

/** Omitted or invalid queries retain the existing OBS editor-tab behavior. */
export function obsViewerKind(
  params: URLSearchParams,
): KeyViewerKind | undefined {
  const viewer = params.get('viewer');
  return viewer === 'hand' || viewer === 'foot' ? viewer : undefined;
}

export function obsWindowLabel(viewer?: KeyViewerKind): string {
  return viewer ? `${viewer}-overlay` : 'obs-overlay';
}
