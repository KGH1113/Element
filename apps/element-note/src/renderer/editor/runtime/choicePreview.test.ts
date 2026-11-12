import { afterEach, describe, expect, it } from 'vitest';
import { useChoicePreviewStore } from './choicePreview';

describe('choice preview', () => {
  const state = () => useChoicePreviewStore.getState();
  afterEach(() => state().show({ type: 'key', index: 0 }, null));

  it('draws changes over one element and stops drawing them', () => {
    state().show({ type: 'key', index: 2 }, { noteAlignment: 'left' });
    expect(state()).toMatchObject({
      target: { type: 'key', index: 2 },
      changes: { noteAlignment: 'left' },
    });
    state().show({ type: 'key', index: 2 }, null);
    expect(state()).toMatchObject({ target: null, changes: null });
  });

  it('draws over a stat apart from the key with the same index', () => {
    state().show({ type: 'stat', index: 0 }, { noteAlignment: 'right' });
    expect(state().target).toEqual({ type: 'stat', index: 0 });
  });
});
