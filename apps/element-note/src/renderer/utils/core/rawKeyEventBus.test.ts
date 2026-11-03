import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { RawInputPayload } from '@src/types/plugin/api';

// A backend that answers a moment later, like the real IPC round trip.
const backend = vi.hoisted(() => ({
  subscribers: 0,
  emit: null as ((payload: unknown) => void) | null,
}));

vi.mock('@tauri-apps/api/core', () => ({
  invoke: (command: string) =>
    new Promise<void>((resolve) =>
      setTimeout(() => {
        if (command === 'raw_input_subscribe') backend.subscribers += 1;
        if (command === 'raw_input_unsubscribe')
          backend.subscribers = Math.max(0, backend.subscribers - 1);
        resolve();
      }, 1),
    ),
}));

vi.mock('@tauri-apps/api/event', () => ({
  listen: async (
    event: string,
    handler: (message: { payload: unknown }) => void,
  ) => {
    if (event === 'input:raw') backend.emit = (payload) => handler({ payload });
    return () => undefined;
  },
}));

const press = (label: string): RawInputPayload => ({
  device: 'keyboard',
  label,
  labels: [label],
  state: 'DOWN',
});

// The backend only streams raw input while someone subscribed to it.
const pressKey = (label: string) => {
  if (backend.subscribers > 0) backend.emit?.(press(label));
};

async function loadKeysApi() {
  const { keysApi } = await import('@api/modules/keysApi');
  return keysApi;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.resetModules();
  backend.subscribers = 0;
  backend.emit = null;
});

afterEach(() => {
  vi.useRealTimers();
});

describe('raw input listeners', () => {
  it('stop hearing keys once removed, even before the backend answers', async () => {
    const keysApi = await loadKeysApi();
    const stale = vi.fn();
    const current = vi.fn();

    // A key binding session restarts (the panel re-rendered) while the
    // first subscription is still on its way to the backend.
    const stopStale = keysApi.onRawInput(stale);
    stopStale();
    const stopCurrent = keysApi.onRawInput(current);
    await vi.runAllTimersAsync();

    pressKey('R');

    expect(current).toHaveBeenCalledTimes(1);
    expect(stale).not.toHaveBeenCalled();
    stopCurrent();
  });

  it('let the backend stop streaming when the last listener leaves', async () => {
    const keysApi = await loadKeysApi();
    const early = vi.fn();
    const listener = vi.fn();

    keysApi.onRawInput(early)();
    await vi.runAllTimersAsync();
    const stop = keysApi.onRawInput(listener);
    await vi.runAllTimersAsync();
    expect(backend.subscribers).toBe(1);

    stop();
    await vi.runAllTimersAsync();
    pressKey('MOUSE1');

    expect(backend.subscribers).toBe(0);
    expect(early).not.toHaveBeenCalled();
    expect(listener).not.toHaveBeenCalled();
  });

  it('hear each press once while subscribed', async () => {
    const keysApi = await loadKeysApi();
    const listener = vi.fn();

    const stop = keysApi.onRawInput(listener);
    await vi.runAllTimersAsync();
    pressKey('E');
    stop();
    await vi.runAllTimersAsync();
    pressKey('E');

    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledWith(press('E'));
  });
});
