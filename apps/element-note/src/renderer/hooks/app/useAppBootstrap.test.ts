import { describe, expect, it } from 'vitest';
import {
  mergeSelectedViewerTabs,
  resolveViewerSelectedKeyType,
} from './useAppBootstrap';

const selectedViewerTabs = {
  hand: 'hand-selected',
  foot: 'foot-selected',
};

describe('resolveViewerSelectedKeyType', () => {
  it('pins each native overlay to its viewer selection', () => {
    expect(
      resolveViewerSelectedKeyType(
        'hand',
        selectedViewerTabs,
        'editor-selected',
      ),
    ).toBe('hand-selected');
    expect(
      resolveViewerSelectedKeyType(
        'foot',
        selectedViewerTabs,
        'editor-selected',
      ),
    ).toBe('foot-selected');
  });

  it('keeps the global editor selection for main and OBS', () => {
    expect(
      resolveViewerSelectedKeyType(
        undefined,
        selectedViewerTabs,
        'editor-selected',
      ),
    ).toBe('editor-selected');
  });
});

describe('mergeSelectedViewerTabs', () => {
  it('does not let a foot selection event overwrite the current hand tab', () => {
    expect(
      mergeSelectedViewerTabs(
        { hand: 'hand-new', foot: 'foot-old' },
        { hand: 'hand-stale', foot: 'foot-new' },
        'foot',
      ),
    ).toEqual({ hand: 'hand-new', foot: 'foot-new' });
  });

  it('replaces both selections for full-state events', () => {
    expect(
      mergeSelectedViewerTabs(
        { hand: 'hand-old', foot: 'foot-old' },
        { hand: 'hand-restored', foot: 'foot-restored' },
      ),
    ).toEqual({ hand: 'hand-restored', foot: 'foot-restored' });
  });
});
