import { beforeEach, describe, expect, it } from 'vitest';
import { usePressedPreviewStore } from './pressedPreview';

describe('showing keys as pressed while a pressed look is chosen', () => {
  beforeEach(() => usePressedPreviewStore.setState({ open: 0 }));

  it('lasts as long as a chooser of a pressed look is open', () => {
    const { begin } = usePressedPreviewStore.getState();
    const endFirst = begin();
    const endSecond = begin();
    expect(usePressedPreviewStore.getState().open).toBe(2);
    endFirst();
    expect(usePressedPreviewStore.getState().open).toBe(1);
    // Ending twice counts once.
    endFirst();
    expect(usePressedPreviewStore.getState().open).toBe(1);
    endSecond();
    expect(usePressedPreviewStore.getState().open).toBe(0);
  });
});
