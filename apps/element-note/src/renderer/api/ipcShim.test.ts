import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { disposeIpcShim, initIpcShim } from './ipcShim';

class MockSocket {
  static readonly OPEN = 1;
  static latest: MockSocket;
  readonly readyState = MockSocket.OPEN;
  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  sent: Array<{ type: string; payload: Record<string, unknown> }> = [];

  constructor() {
    MockSocket.latest = this;
  }

  send(message: string) {
    this.sent.push(JSON.parse(message));
  }

  close() {
    this.onclose?.();
  }

  receive(type: string, payload: unknown) {
    this.onmessage?.({
      data: JSON.stringify({ v: 2, seq: 1, ts: 0, type, payload }),
    });
  }
}

function invoke(command: string) {
  return (
    window as unknown as {
      __TAURI_INTERNALS__: { invoke(command: string): Promise<unknown> };
    }
  ).__TAURI_INTERNALS__.invoke(command);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('WebSocket', MockSocket);
});

afterEach(() => {
  disposeIpcShim();
  vi.clearAllTimers();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('DmNote v2 OBS protocol', () => {
  it('uses protocol 2 and exact server command allowlisting', async () => {
    const initialized = initIpcShim('ws://127.0.0.1:34891', 'token', 'foot');
    const socket = MockSocket.latest;
    socket.onopen?.();
    expect(socket.sent[0]).toMatchObject({
      type: 'hello',
      payload: { protocol: 2, token: 'token' },
    });
    socket.receive('hello_ack', { allowedList: ['app_bootstrap'] });
    socket.receive('snapshot', {});
    await initialized;

    await expect(invoke('app_bootstrap_extra')).rejects.toThrow(
      'Command is not available in OBS',
    );
    expect(socket.sent).toHaveLength(1);

    const response = invoke('app_bootstrap');
    const request = socket.sent[1];
    socket.receive('invoke_response', {
      requestId: request.payload.requestId,
      result: { selectedViewerTabs: { hand: 'hand', foot: 'foot' } },
    });
    await expect(response).resolves.toMatchObject({
      selectedViewerTabs: { foot: 'foot' },
    });
  });

  it('rejects RPC when a hello acknowledgement omits the allowlist', async () => {
    const initialized = initIpcShim('ws://127.0.0.1:34891', 'token');
    MockSocket.latest.receive('hello_ack', {});
    MockSocket.latest.receive('snapshot', {});
    await initialized;
    await expect(invoke('app_bootstrap')).rejects.toThrow(
      'Command is not available in OBS',
    );
  });

  it('fails initialization clearly for a protocol mismatch', async () => {
    const initialized = initIpcShim('ws://127.0.0.1:34891', 'token');
    MockSocket.latest.receive('error', { code: 'PROTOCOL_MISMATCH' });
    await expect(initialized).rejects.toThrow('refresh the browser source');
    expect(vi.getTimerCount()).toBe(0);
  });
});
