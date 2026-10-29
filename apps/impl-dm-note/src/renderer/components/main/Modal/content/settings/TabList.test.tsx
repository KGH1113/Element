import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import { useKeyStore } from '@stores/data/useKeyStore';
import TabList from './TabList';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('@hooks/useLenis', () => ({
  useLenis: () => ({
    scrollContainerRef: vi.fn(),
    wrapperElement: null,
    lenisInstance: { current: { resize: vi.fn() } },
    scrollbarWidth: 0,
  }),
}));

vi.mock('@stores/data/editorHistorySnapshot', () => ({
  captureEditorHistorySnapshot: () => null,
  pushEditorHistorySnapshot: vi.fn(),
}));

vi.mock('../dialogs/Alert.jsx', () => ({ default: () => null }));
vi.mock('../editors/TabNameModal', () => ({ default: () => null }));

const tabsApi = {
  select: vi.fn(async () => ({ success: true })),
  reorder: vi.fn(async () => ({})),
};

const pointer = (type: string, clientY: number) => {
  const Event = window.PointerEvent ?? MouseEvent;
  return new Event(type, {
    bubbles: true,
    cancelable: true,
    button: 0,
    clientY,
  });
};

describe('TabList', () => {
  let container: HTMLDivElement;
  let root: Root;
  let originalRequestAnimationFrame: typeof requestAnimationFrame;
  let originalCancelAnimationFrame: typeof cancelAnimationFrame;

  const row = (id: string) =>
    container.querySelector<HTMLElement>(`[data-tab-row="${id}"]`)!;
  const rowOrder = () =>
    Array.from(container.querySelectorAll('[data-tab-row]')).map((el) =>
      el.getAttribute('data-tab-row'),
    );

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    globalThis.requestAnimationFrame = (callback) => {
      callback(0);
      return 1;
    };
    globalThis.cancelAnimationFrame = vi.fn();
    (window as unknown as { api: unknown }).api = { keys: { tabs: tabsApi } };
  });

  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
    globalThis.requestAnimationFrame = originalRequestAnimationFrame;
    globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
  });

  beforeEach(() => {
    tabsApi.select.mockClear();
    tabsApi.reorder.mockClear();
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    useKeyStore.setState({
      selectedKeyType: 'hand-two',
      selectedViewerTabs: { hand: 'hand-two', foot: 'foot-one' },
      tabs: [
        { id: 'hand-one', name: 'Hand 1', viewerKind: 'hand' },
        { id: 'hand-two', name: 'Hand 2', viewerKind: 'hand' },
        { id: 'foot-one', name: 'Foot 1', viewerKind: 'foot' },
      ],
    });
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('marks and focuses the selected tab', () => {
    act(() => root.render(<TabList viewerKind="hand" />));

    const selected = row('hand-two');
    const select = selected.querySelector('button[aria-current="true"]');
    expect(selected.getAttribute('data-selected')).toBe('true');
    expect(row('hand-one').getAttribute('data-selected')).toBe('false');
    expect(select?.getAttribute('aria-label')).toBe('Hand 2');
    expect(document.activeElement).toBe(select);
  });

  it('gives each tab rename and delete actions and a create row', () => {
    act(() => root.render(<TabList viewerKind="hand" />));

    for (const id of ['hand-one', 'hand-two']) {
      expect(
        row(id).querySelector('[aria-label="tabs.renameTitle"]'),
      ).not.toBeNull();
      expect(
        row(id).querySelector('[aria-label="tabs.delete"]'),
      ).not.toBeNull();
    }
    expect(container.textContent).toContain('tabs.createTitle');
  });

  it('hides delete on the last tab of a viewer', () => {
    act(() => root.render(<TabList viewerKind="foot" />));

    expect(
      row('foot-one').querySelector('[aria-label="tabs.delete"]'),
    ).toBeNull();
    expect(
      row('foot-one').querySelector('[aria-label="tabs.renameTitle"]'),
    ).not.toBeNull();
  });

  it('reorders by dragging the row itself', async () => {
    act(() => root.render(<TabList viewerKind="hand" />));
    expect(rowOrder()).toEqual(['hand-two', 'hand-one']);

    // jsdom lays every row out at y=0, so a 30px drag up lands in slot 0.
    act(() => {
      row('hand-one').dispatchEvent(pointer('pointerdown', 40));
    });
    act(() => {
      window.dispatchEvent(pointer('pointermove', 10));
    });
    expect(rowOrder()).toEqual(['hand-one', 'hand-two']);
    expect(row('hand-one').getAttribute('data-dragging')).toBe('true');

    await act(async () => {
      window.dispatchEvent(pointer('pointerup', 10));
    });
    expect(tabsApi.reorder).toHaveBeenCalledWith('hand', [
      'hand-one',
      'hand-two',
    ]);
    expect(row('hand-one').getAttribute('data-dragging')).toBeNull();
  });

  it('treats a press without movement as a click', () => {
    act(() => root.render(<TabList viewerKind="hand" />));
    const select = row('hand-one').querySelector<HTMLButtonElement>(
      'button[aria-label="Hand 1"]',
    )!;

    act(() => {
      select.dispatchEvent(pointer('pointerdown', 40));
      window.dispatchEvent(pointer('pointermove', 41));
      window.dispatchEvent(pointer('pointerup', 41));
      select.click();
    });

    expect(tabsApi.select).toHaveBeenCalledWith('hand', 'hand-one');
    expect(tabsApi.reorder).not.toHaveBeenCalled();
  });
});
