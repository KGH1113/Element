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
import TopBar from './TopBar';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock('@assets/svgs/setting.svg', () => ({ default: () => null }));
// The left part of the bar has its own tests.
vi.mock('@components/main/Viewers/ViewerSlots', () => ({
  default: () => null,
}));
// OBS is on: it used to replace the switch with "Shown by OBS".
vi.mock('@api/modules/obsApi', () => ({
  obsApi: {
    status: vi.fn(async () => ({ running: true, port: 34891, clientCount: 1 })),
    onStatus: () => vi.fn(),
  },
}));

describe('top bar', () => {
  let container: HTMLDivElement;
  let root: Root;
  const setVisible = vi.fn(async (_visible: boolean) => undefined);
  const onOpenSettings = vi.fn();
  const onOpenOutputs = vi.fn();
  const originalApi = window.api;

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  });
  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
    window.api = originalApi;
  });
  beforeEach(async () => {
    setVisible.mockClear();
    onOpenSettings.mockClear();
    onOpenOutputs.mockClear();
    window.api = {
      overlay: {
        get: vi.fn(async () => ({ visible: false })),
        onVisibility: () => vi.fn(),
        setVisible,
      },
    } as unknown as typeof window.api;
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    await act(async () =>
      root.render(
        <TopBar
          onOpenSettings={onOpenSettings}
          onOpenOutputs={onOpenOutputs}
        />,
      ),
    );
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('has no switch of its own: where to show is switched in settings', () => {
    expect(container.querySelector('[role="switch"]')).toBeNull();
    expect(setVisible).not.toHaveBeenCalled();
  });

  it('opens settings on where to show from the game connection', async () => {
    const status = container.querySelector<HTMLButtonElement>(
      '.key-limiter-status',
    )!;
    expect(status.hasAttribute('aria-haspopup')).toBe(false);
    await act(async () => status.click());
    expect(onOpenOutputs).toHaveBeenCalledTimes(1);
    expect(onOpenSettings).not.toHaveBeenCalled();
    // No dialog on the way: the switches live in settings.
    expect(document.querySelector('[role="dialog"]')).toBeNull();
  });
});
