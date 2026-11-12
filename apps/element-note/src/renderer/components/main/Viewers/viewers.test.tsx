import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useKeyStore } from '@stores/data/useKeyStore';
import ViewTabs from './ViewTabs';
import ViewerPicker from './ViewerPicker';
import ViewerGallery from './ViewerGallery';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, params?: Record<string, string | number>) =>
      params ? `${key}:${Object.values(params).join(',')}` : key,
  }),
}));

describe('key viewers', () => {
  let root: Root;
  let container: HTMLDivElement;
  const select = vi.fn(async () => ({ success: true }));
  const originalApi = window.api;

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    select.mockClear();
    window.api = {
      keys: { tabs: { select } },
    } as unknown as typeof window.api;
    useKeyStore.setState({
      isBootstrapped: true,
      selectedKeyType: 'hand-16',
      selectedViewerTabs: { hand: 'hand-16', foot: 'foot-pedal' },
      tabs: [
        { id: 'hand-16', name: '16K', viewerKind: 'hand' },
        { id: 'hand-4', name: '4K', viewerKind: 'hand' },
        { id: 'foot-pedal', name: 'Pedal', viewerKind: 'foot' },
      ],
      positions: {},
    });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    window.api = originalApi;
  });

  it('switches the view with a tab and does nothing else', () => {
    act(() => root.render(<ViewTabs />));
    const tabs = Array.from(
      container.querySelectorAll<HTMLButtonElement>('[role="tab"]'),
    );
    // Both views are always there, each saying which key viewer it shows.
    expect(tabs.map((tab) => tab.textContent)).toEqual([
      'viewers.hand16K',
      'viewers.footPedal',
    ]);
    expect(tabs.map((tab) => tab.getAttribute('aria-selected'))).toEqual([
      'true',
      'false',
    ]);

    act(() => tabs[1].click());
    expect(useKeyStore.getState().selectedKeyType).toBe('foot-pedal');
    expect(tabs.map((tab) => tab.getAttribute('aria-selected'))).toEqual([
      'false',
      'true',
    ]);
    // The key viewers that are up did not change: a tab only swaps the view.
    expect(useKeyStore.getState().selectedViewerTabs).toEqual({
      hand: 'hand-16',
      foot: 'foot-pedal',
    });
  });

  it('chooses the key viewer of the open view from the button on the canvas', () => {
    const onChoose = vi.fn();
    act(() => root.render(<ViewerPicker onChoose={onChoose} />));
    const picker = container.querySelector<HTMLButtonElement>('.en-picker')!;
    // A value with an arrow: the key viewer the open view shows.
    expect(picker.querySelector('.en-picker-value')?.textContent).toBe('16K');
    expect(picker.querySelector('.en-picker-arrow')).not.toBeNull();
    act(() => picker.click());
    expect(onChoose).toHaveBeenCalledWith('hand');
    // Choosing does not switch the view by itself.
    expect(useKeyStore.getState().selectedKeyType).toBe('hand-16');

    // With the feet view open, the same value is about the feet.
    act(() => useKeyStore.getState().setSelectedKeyType('foot-pedal'));
    expect(picker.querySelector('.en-picker-value')?.textContent).toBe('Pedal');
    act(() => picker.click());
    expect(onChoose).toHaveBeenLastCalledWith('foot');
  });

  it('lists only the key viewers of the slot that is being changed', () => {
    const onClose = vi.fn();
    act(() =>
      root.render(
        <ViewerGallery
          slot="hand"
          onClose={onClose}
          showConfirm={vi.fn()}
          showAlert={vi.fn()}
        />,
      ),
    );
    const names = () =>
      Array.from(container.querySelectorAll('.en-card .en-card-name')).map(
        (name) => name.textContent,
      );
    expect(names()).toEqual(['16K', '4K']);
    expect(container.textContent).not.toContain('Pedal');
    // The one that is up is marked.
    const cards = Array.from(container.querySelectorAll('.en-card'));
    expect(cards.map((card) => card.hasAttribute('data-shown'))).toEqual([
      true,
      false,
    ]);

    // Pressing another puts it up, opens it for editing and returns.
    act(() =>
      cards[1].querySelector<HTMLButtonElement>('.en-card-open')!.click(),
    );
    expect(useKeyStore.getState().selectedKeyType).toBe('hand-4');
    expect(useKeyStore.getState().selectedViewerTabs.hand).toBe('hand-4');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('offers a new key viewer for that slot and a menu on each one', () => {
    act(() =>
      root.render(
        <ViewerGallery
          slot="foot"
          onClose={vi.fn()}
          showConfirm={vi.fn()}
          showAlert={vi.fn()}
        />,
      ),
    );
    expect(
      container.querySelector('.en-card-new .en-card-name')?.textContent,
    ).toBe('viewers.new');
    const more = container.querySelectorAll('.en-card-more');
    expect(more).toHaveLength(1);
    expect(more[0].getAttribute('aria-label')).toBe('viewers.menu:Pedal');
  });

  it('names a new key viewer on its card, without a dialog', async () => {
    const create = vi.fn(async () => ({ success: true }));
    window.api = {
      keys: { tabs: { select, create } },
    } as unknown as typeof window.api;
    const onClose = vi.fn();
    act(() =>
      root.render(
        <ViewerGallery
          slot="hand"
          onClose={onClose}
          showConfirm={vi.fn()}
          showAlert={vi.fn()}
        />,
      ),
    );
    act(() =>
      container.querySelector<HTMLButtonElement>('.en-card-new')!.click(),
    );
    const field = () =>
      container.querySelector<HTMLInputElement>('.en-card-name-input');
    expect(field()).not.toBeNull();
    expect(container.querySelector('[data-dmn-modal-backdrop]')).toBeNull();

    const type = (value: string) => {
      field()!.value = value;
    };
    const press = (key: string) =>
      act(async () => {
        field()!.dispatchEvent(
          new KeyboardEvent('keydown', { key, bubbles: true }),
        );
      });

    // A name another key viewer of the slot has is refused, and says so.
    type('4K');
    await press('Enter');
    expect(create).not.toHaveBeenCalled();
    expect(container.querySelector('[role="alert"]')?.textContent).toBe(
      'tabs.name.duplicate',
    );

    type('  7K ');
    await press('Enter');
    expect(create).toHaveBeenCalledWith('hand', '7K');
    expect(field()).toBeNull();

    // Escape in the field leaves the name, not the gallery.
    act(() =>
      container.querySelector<HTMLButtonElement>('.en-card-new')!.click(),
    );
    await press('Escape');
    expect(field()).toBeNull();
    expect(onClose).not.toHaveBeenCalled();
  });
});
