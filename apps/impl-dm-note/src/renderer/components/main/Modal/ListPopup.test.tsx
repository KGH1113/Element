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
  type Mock,
} from 'vitest';
import ListPopup, { type ListItem } from './ListPopup';

vi.mock('@hooks/useLenis', () => ({
  useLenis: () => ({ scrollContainerRef: vi.fn() }),
}));

const SUBMENU_HOVER_DELAY_MS = 150;

const items: ListItem[] = [
  {
    id: 'import',
    label: 'Import',
    children: [
      { id: 'import-all', label: 'Everything' },
      { id: 'import-tab', label: 'Current tab' },
    ],
  },
  {
    id: 'locked',
    label: 'Locked',
    disabled: true,
    children: [{ id: 'locked-child', label: 'Hidden', disabled: true }],
  },
];

const hover = async (element: Element) => {
  await act(async () => {
    element.dispatchEvent(
      new MouseEvent('mouseover', {
        bubbles: true,
        relatedTarget: document.body,
      }),
    );
    await new Promise((resolve) =>
      setTimeout(resolve, SUBMENU_HOVER_DELAY_MS + 20),
    );
  });
};

const row = (label: string) =>
  Array.from(document.querySelectorAll('[role="menuitem"]')).find(
    (item) => item.textContent === label,
  ) as HTMLButtonElement;

describe('ListPopup submenu', () => {
  let container: HTMLDivElement;
  let root: Root;
  let onSelect: Mock<(id: string) => void>;
  let onClose: Mock<() => void>;

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  });

  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
  });

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    onSelect = vi.fn<(id: string) => void>();
    onClose = vi.fn<() => void>();
    act(() =>
      root.render(
        <ListPopup
          open
          position={{ x: 20, y: 20 }}
          items={items}
          textAlign="left"
          onSelect={onSelect}
          onClose={onClose}
        />,
      ),
    );
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  // The popup surface has a transform and backdrop-filter, which would make a
  // fixed submenu inside it position against the popup and land off-screen.
  it('renders the submenu outside the popup surface', async () => {
    await hover(row('Import'));

    const submenu = document.querySelector('[data-dmn-popup-submenu="true"]');
    const surface = row('Import').closest('.dmn-popup-surface');
    expect(submenu).not.toBeNull();
    expect(submenu?.parentElement).toBe(document.body);
    expect(surface?.contains(submenu as Node)).toBe(false);
    expect(submenu?.textContent).toContain('Everything');
  });

  it('selects a submenu item instead of closing on its mousedown', async () => {
    await hover(row('Import'));

    const choice = row('Current tab');
    act(() => {
      choice.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
      choice.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(onSelect).toHaveBeenCalledWith('import-tab');
    // The only close comes from the selection itself, not the outside-click guard.
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('keeps a disabled group closed', async () => {
    await hover(row('Locked'));

    expect(document.body.textContent).not.toContain('Hidden');
  });
});
