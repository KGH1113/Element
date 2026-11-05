import { afterEach, describe, expect, it, vi } from 'vitest';
import type { BridgeMessage } from '@src/types/plugin/api';

const listeners = vi.hoisted(() => ({
  receive: undefined as
    ((event: { payload: BridgeMessage }) => void) | undefined,
}));
vi.mock('@tauri-apps/api/event', () => ({
  listen: vi.fn((_event, callback) => {
    listeners.receive = callback;
    return Promise.resolve(() => {});
  }),
}));
vi.mock('../invoke', () => ({ invoke: vi.fn() }));

import { bridgeApi } from './bridgeApi';

afterEach(() => {
  bridgeApi.off('sync');
});

describe('bridge target routing', () => {
  it('does not consume overlay-targeted broadcasts in the editor', () => {
    window.__dmn_window_type = 'main';
    const received = vi.fn();
    bridgeApi.on('sync', received);
    listeners.receive?.({
      payload: { type: 'sync', target: 'overlay', data: 1 },
    });
    expect(received).not.toHaveBeenCalled();
    listeners.receive?.({ payload: { type: 'sync', data: 2 } });
    expect(received).toHaveBeenCalledWith(2);
  });

  it('keeps once listeners for the intended overlay message', () => {
    window.__dmn_window_type = 'overlay';
    const received = vi.fn();
    bridgeApi.once('sync', received);
    listeners.receive?.({ payload: { type: 'sync', target: 'main', data: 1 } });
    listeners.receive?.({
      payload: { type: 'sync', target: 'overlay', data: 2 },
    });
    listeners.receive?.({
      payload: { type: 'sync', target: 'overlay', data: 3 },
    });
    expect(received).toHaveBeenCalledExactlyOnceWith(2);
  });
});
