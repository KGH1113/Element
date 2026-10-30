import { describe, expect, it, vi } from 'vitest';
import type {
  IpcConnection,
  IpcConnectionState,
  IpcNamespaceStatus,
} from 'adofai-ipc/transport';
import { SdkMessagePort } from './sdkMessagePort';

function harness() {
  const states = new Set<(state: IpcConnectionState) => void>();
  const statuses = new Set<(status: IpcNamespaceStatus) => void>();
  const channel = {
    status: 'initializing' as IpcNamespaceStatus,
    onStatus(listener: (status: IpcNamespaceStatus) => void) {
      statuses.add(listener);
      listener(this.status);
      return () => statuses.delete(listener);
    },
    on: vi.fn(() => vi.fn()),
    onCommand: vi.fn(() => vi.fn()),
    send: vi.fn(() => 'command'),
  };
  const client = {
    state: 'reconnecting' as IpcConnectionState,
    namespace: () => channel,
    checkConnection: vi.fn(),
    onState(listener: (state: IpcConnectionState) => void) {
      states.add(listener);
      listener(this.state);
      return () => states.delete(listener);
    },
  };
  const state = (value: IpcConnectionState) => {
    client.state = value;
    states.forEach((receive) => receive(value));
  };
  const status = (value: IpcNamespaceStatus) => {
    channel.status = value;
    statuses.forEach((receive) => receive(value));
  };
  return {
    client,
    channel,
    states,
    statuses,
    state,
    status,
    port: new SdkMessagePort(
      Promise.resolve(client as unknown as IpcConnection),
      'test',
    ),
  };
}

describe('SDK message port lifecycle', () => {
  it('retains retry state until transport and namespace are ready across game restarts', async () => {
    const h = harness();
    await Promise.resolve();
    expect(h.port.state).toBe('retrying');
    h.status('unavailable');
    expect(h.port.state).toBe('retrying');
    h.state('connected');
    expect(h.port.state).toBe('connecting');
    h.status('ready');
    expect(h.port.state).toBe('ready');
    h.state('reconnecting');
    h.status('unavailable');
    expect(h.port.state).toBe('retrying');
    expect(() => h.port.send('mutate', {})).toThrow('not ready');
    h.state('connected');
    h.status('ready');
    expect(h.port.state).toBe('ready');
    expect(h.channel.send).not.toHaveBeenCalled();
    h.state('closed');
    expect(h.port.state).toBe('disposed');
  });
  it('checks resumed connections and releases all lifecycle listeners on close', async () => {
    const h = harness();
    await Promise.resolve();
    const visibility = vi
      .spyOn(document, 'visibilityState', 'get')
      .mockReturnValue('visible');
    window.dispatchEvent(new Event('focus'));
    window.dispatchEvent(new Event('online'));
    document.dispatchEvent(new Event('visibilitychange'));
    expect(h.client.checkConnection).toHaveBeenCalledTimes(3);
    visibility.mockReturnValue('hidden');
    window.dispatchEvent(new Event('focus'));
    expect(h.client.checkConnection).toHaveBeenCalledTimes(3);
    h.state('closed');
    visibility.mockReturnValue('visible');
    window.dispatchEvent(new Event('focus'));
    window.dispatchEvent(new Event('online'));
    expect(h.client.checkConnection).toHaveBeenCalledTimes(3);
    expect(h.states.size).toBe(0);
    expect(h.statuses.size).toBe(0);
    visibility.mockRestore();
  });
  it('keeps protocol mismatch terminal when namespace status changes', async () => {
    const h = harness();
    await Promise.resolve();
    h.state('incompatible');
    h.status('unavailable');
    expect(h.port.state).toBe('version-mismatch');
    expect(h.states.size).toBe(0);
    expect(h.statuses.size).toBe(0);
  });
  it('does not label a normal startup failure as an incompatible version', async () => {
    const port = new SdkMessagePort(
      Promise.reject(new Error('cancelled')),
      'test',
    );
    await Promise.resolve();
    await Promise.resolve();
    expect(port.state).toBe('disconnected');
  });
});
