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
import { obsApi } from '@api/modules/obsApi';
import { useOverlayVisibility } from './useOverlayVisibility';

// OBS is on for every test here: the desktop switch must not care.
vi.mock('@api/modules/obsApi', () => ({
  obsApi: {
    status: vi.fn(async () => ({ running: true, port: 34891, clientCount: 1 })),
    onStatus: vi.fn(() => vi.fn()),
  },
}));

describe('the desktop key viewer switch', () => {
  let container: HTMLDivElement;
  let root: Root;
  let overlay: ReturnType<typeof useOverlayVisibility>;
  let announce: (payload: { visible: boolean }) => void;
  const get = vi.fn();
  const setVisible = vi.fn();
  const originalApi = window.api;

  function Probe() {
    overlay = useOverlayVisibility();
    return null;
  }
  const mount = async () => {
    root = createRoot(container);
    await act(async () => root.render(<Probe />));
  };

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  });
  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
    window.api = originalApi;
  });
  beforeEach(() => {
    get.mockReset().mockResolvedValue({ visible: false });
    setVisible.mockReset().mockResolvedValue(undefined);
    window.api = {
      overlay: {
        get,
        setVisible,
        onVisibility: (listener: typeof announce) => {
          announce = listener;
          return vi.fn();
        },
      },
    } as unknown as typeof window.api;
    container = document.createElement('div');
    document.body.appendChild(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('shows what is on the desktop and follows the app', async () => {
    await mount();
    expect(overlay.visible).toBe(false);
    act(() => announce({ visible: true }));
    expect(overlay.visible).toBe(true);
  });

  it('shows and hides whatever OBS is doing', async () => {
    await mount();
    await act(async () => overlay.toggle());
    expect(setVisible).toHaveBeenLastCalledWith(true);
    expect(overlay.visible).toBe(true);
    await act(async () => overlay.toggle());
    expect(setVisible).toHaveBeenLastCalledWith(false);
    expect(overlay.visible).toBe(false);
    // There is no locked state and OBS is never asked.
    expect(Object.keys(overlay).sort()).toEqual(['set', 'toggle', 'visible']);
    expect(obsApi.status).not.toHaveBeenCalled();
    expect(obsApi.onStatus).not.toHaveBeenCalled();
  });

  it('is set to shown or hidden, and asks nothing when it already is', async () => {
    await mount();
    await act(async () => overlay.set(false));
    expect(setVisible).not.toHaveBeenCalled();
    await act(async () => overlay.set(true));
    expect(setVisible).toHaveBeenCalledTimes(1);
    expect(setVisible).toHaveBeenLastCalledWith(true);
    expect(overlay.visible).toBe(true);
  });

  it('goes back to what is on the desktop when the app refuses', async () => {
    setVisible.mockRejectedValue(new Error('window failed'));
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    await mount();
    await act(async () => overlay.toggle());
    expect(setVisible).toHaveBeenCalledWith(true);
    expect(overlay.visible).toBe(false);
    vi.restoreAllMocks();
  });

  it('starts from the last known state when the screen comes back', async () => {
    get.mockResolvedValue({ visible: true });
    await mount();
    expect(overlay.visible).toBe(true);
    act(() => root.unmount());
    // The app has not answered yet: no flick to a default in between.
    get.mockReturnValue(new Promise(() => undefined));
    await mount();
    expect(overlay.visible).toBe(true);
  });
});
