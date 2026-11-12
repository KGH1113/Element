import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { usePartPreviewStore } from './partPreview';

describe('part preview', () => {
  const state = () => usePartPreviewStore.getState();

  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('presses the key now and then while a part is shown, and stops after', () => {
    const end = state().show('counter');
    expect(state().part).toBe('counter');
    // Nothing on the frame the pointer arrives.
    expect(state().pressed).toBe(false);
    vi.advanceTimersByTime(200);
    expect(state()).toMatchObject({ pressed: true, presses: 1 });
    vi.advanceTimersByTime(200);
    expect(state().pressed).toBe(false);
    vi.advanceTimersByTime(700);
    expect(state().presses).toBe(2);

    end();
    expect(state()).toMatchObject({ part: null, pressed: false, presses: 0 });
    vi.advanceTimersByTime(2000);
    expect(state().presses).toBe(0);
  });

  it('shows the part asked for last, and the one before it again when that ends', () => {
    const endPage = state().show('note');
    const endHover = state().show('counter');
    expect(state().part).toBe('counter');
    endHover();
    expect(state().part).toBe('note');
    // Ending twice changes nothing.
    endHover();
    expect(state().part).toBe('note');
    endPage();
    expect(state().part).toBeNull();
  });
});
