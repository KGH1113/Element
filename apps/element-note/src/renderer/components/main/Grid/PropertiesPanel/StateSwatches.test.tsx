import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { usePressedPreviewStore } from '@src/renderer/editor/runtime/pressedPreview';
import StateSwatches, { type LookState } from './StateSwatches';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe('a color with two states', () => {
  let root: Root;
  let container: HTMLDivElement;
  const onPress = vi.fn();

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    onPress.mockReset();
    usePressedPreviewStore.setState({ open: 0 });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  const render = (open: LookState | null, activeName?: string) =>
    act(() =>
      root.render(
        <StateSwatches
          label="Background"
          colors={{ idle: '#111111', active: 'rgba(255, 0, 0, 0.5)' }}
          open={open}
          onPress={onPress}
          activeName={activeName}
        />,
      ),
    );
  const states = () =>
    Array.from(container.querySelectorAll<HTMLButtonElement>('.en-state'));

  it('shows both, each with its name and its color, and none is hidden in a chooser', () => {
    render(null);
    expect(states().map((state) => state.textContent)).toEqual([
      'colorPicker.idle',
      'colorPicker.active',
    ]);
    expect(states().map((state) => state.getAttribute('aria-label'))).toEqual([
      'Background: colorPicker.idle',
      'Background: colorPicker.active',
    ]);
    expect(
      states().map(
        (state) =>
          state.querySelector<HTMLElement>('.en-state-swatch')!.style
            .background,
      ),
    ).toEqual(['rgb(17, 17, 17)', 'rgba(255, 0, 0, 0.5)']);
  });

  it('says which state a press is for and which one is open', () => {
    render('idle');
    expect(
      states().map((state) => state.getAttribute('aria-expanded')),
    ).toEqual(['true', 'false']);
    act(() => states()[1].click());
    expect(onPress).toHaveBeenLastCalledWith('active');
  });

  it('names the second state after what the element does', () => {
    render(null, 'Turning');
    expect(states()[1].textContent).toBe('Turning');
  });

  it('has the canvas draw the selected keys as pressed while the pressed color is chosen', () => {
    render('idle');
    expect(usePressedPreviewStore.getState().open).toBe(0);
    render('active');
    expect(usePressedPreviewStore.getState().open).toBe(1);
    render(null);
    expect(usePressedPreviewStore.getState().open).toBe(0);
    render('active');
    act(() => root.unmount());
    expect(usePressedPreviewStore.getState().open).toBe(0);
    root = createRoot(container);
  });
});
