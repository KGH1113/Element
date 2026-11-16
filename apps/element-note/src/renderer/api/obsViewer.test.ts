import { describe, expect, it } from 'vitest';
import { obsViewerKind, obsWindowLabel, viewerPageClient } from './obsViewer';

describe('OBS dedicated viewer routing', () => {
  it.each(['hand', 'foot'] as const)(
    'selects the dedicated %s window context',
    (viewer) => {
      const kind = obsViewerKind(new URLSearchParams({ viewer }));
      expect(kind).toBe(viewer);
      expect(obsWindowLabel(kind)).toBe(`${viewer}-overlay`);
    },
  );

  it.each(['', 'viewer=unknown', 'viewer=HAND', 'viewer=hand-overlay'])(
    'keeps the existing OBS window context for %s',
    (query) => {
      const kind = obsViewerKind(new URLSearchParams(query));
      expect(kind).toBeUndefined();
      expect(obsWindowLabel(kind)).toBe('obs-overlay');
    },
  );

  it("tells the game's own pages apart from OBS browser sources", () => {
    expect(
      viewerPageClient(new URLSearchParams('viewer=hand&client=game')),
    ).toBe('game');
    expect(viewerPageClient(new URLSearchParams('viewer=hand'))).toBe(
      'obs-browser',
    );
    expect(viewerPageClient(new URLSearchParams('client=other'))).toBe(
      'obs-browser',
    );
  });
});
