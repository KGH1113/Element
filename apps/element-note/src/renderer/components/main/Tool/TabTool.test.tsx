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
import TabTool from './TabTool';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('../Modal/FloatingTooltip', () => ({
  default: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock('../Modal/TooltipGroup', () => ({
  TooltipGroup: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock('../Modal/FloatingPopup', () => ({
  default: ({
    open,
    children,
    className,
  }: {
    open: boolean;
    children: React.ReactNode;
    className?: string;
  }) =>
    open ? (
      <div role="dialog" className={className}>
        {children}
      </div>
    ) : null,
}));

vi.mock('../Modal/content/settings/TabList', () => ({
  default: ({ viewerKind }: { viewerKind: string }) => (
    <div data-viewer-kind={viewerKind} />
  ),
}));

describe('TabTool', () => {
  let container: HTMLDivElement;
  let root: Root;

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
    useKeyStore.setState({
      isBootstrapped: true,
      selectedKeyType: 'hand-main',
      selectedViewerTabs: { hand: 'hand-main', foot: 'foot-main' },
      tabs: [
        { id: 'hand-main', name: 'Hand 1', viewerKind: 'hand' },
        { id: 'foot-main', name: 'Foot 1', viewerKind: 'foot' },
      ],
    });
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('renders only the hand and foot viewer triggers', () => {
    act(() => root.render(<TabTool />));

    const buttons = container.querySelectorAll('button');
    expect(buttons).toHaveLength(2);
    expect(buttons[0]?.getAttribute('aria-label')).toBe('tabs.handViewer');
    expect(buttons[1]?.getAttribute('aria-label')).toBe('tabs.footViewer');
    expect(buttons[0]?.getAttribute('aria-pressed')).toBe('true');
    expect(buttons[1]?.getAttribute('aria-pressed')).toBe('false');
    expect(buttons[0]?.getAttribute('data-selected')).toBe('true');
    expect(buttons[1]?.getAttribute('data-selected')).toBe('false');
    expect(
      buttons[0]?.querySelector('.viewer-tab-selected-dot'),
    ).not.toBeNull();
    expect(buttons[1]?.querySelector('.viewer-tab-selected-dot')).toBeNull();
    expect(buttons[0]?.textContent).toContain('1');
    expect(buttons[1]?.textContent).toContain('2');
  });

  it('opens the requested group popup and keeps the other group closed', () => {
    act(() => root.render(<TabTool />));
    const buttons = container.querySelectorAll('button');

    act(() =>
      buttons[1]?.dispatchEvent(new MouseEvent('click', { bubbles: true })),
    );

    expect(container.querySelectorAll('[role="dialog"]')).toHaveLength(1);
    expect(container.querySelector('[data-viewer-kind="foot"]')).not.toBeNull();
    expect(buttons[1]?.getAttribute('aria-expanded')).toBe('true');
    expect(buttons[0]?.getAttribute('aria-expanded')).toBe('false');
    expect(buttons[1]?.getAttribute('data-open')).toBe('true');
    expect(buttons[0]?.getAttribute('data-open')).toBe('false');
    expect(
      buttons[0]?.querySelector('.viewer-tab-selected-dot'),
    ).not.toBeNull();
    expect(buttons[1]?.querySelector('.viewer-tab-selected-dot')).toBeNull();
    expect(
      buttons[1]?.querySelector('.viewer-tab-trigger-surface')?.className,
    ).toContain('bg-button-active');
    expect(container.querySelector('.viewer-tab-popup')).not.toBeNull();
    expect(container.querySelector('.viewer-tab-popup')?.className).toContain(
      'z-[100]',
    );
  });
});
