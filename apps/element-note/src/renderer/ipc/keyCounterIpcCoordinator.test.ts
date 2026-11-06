import { describe, expect, it, vi } from 'vitest';
import { FakeMessagePort } from '../__tests__/fakeMessagePort';
import {
  KeyCounterIpcCoordinator,
  readCounts,
  type KeyCounterPort,
} from './keyCounterIpcCoordinator';

const settle = async () => {
  for (let i = 0; i < 5; i++) await Promise.resolve();
};

function counters(overrides: Partial<KeyCounterPort> = {}) {
  return {
    read: vi.fn().mockResolvedValue({ hand: { A: 3 } }),
    beginReplay: vi.fn().mockResolvedValue(undefined),
    endReplay: vi.fn().mockResolvedValue(true),
    ...overrides,
  };
}

describe('key counts for TUFReplay runs', () => {
  it('tells the game it shares counts and answers a read', async () => {
    const messages = new FakeMessagePort();
    const port = counters();
    const coordinator = new KeyCounterIpcCoordinator(messages, port);
    expect(messages.sent[0]).toMatchObject({
      name: 'counters.available',
      payload: { protocolVersion: 1 },
    });
    messages.command('counters.read', {}, 'read-1');
    await settle();
    expect(messages.sent.at(-1)).toMatchObject({
      name: 'counters.read.result',
      correlationId: 'read-1',
      payload: { ok: true, counters: { hand: { A: 3 } } },
    });
    await coordinator.dispose();
  });

  it('answers a failed read so the game does not wait', async () => {
    const messages = new FakeMessagePort();
    const port = counters({ read: vi.fn().mockRejectedValue(new Error('x')) });
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const coordinator = new KeyCounterIpcCoordinator(messages, port);
    messages.command('counters.read', {}, 'read-2');
    await settle();
    expect(messages.sent.at(-1)).toMatchObject({
      correlationId: 'read-2',
      payload: { ok: false, code: 'counters-unavailable' },
    });
    error.mockRestore();
    await coordinator.dispose();
  });

  it('counts a replay from the recorded counts and ends it once', async () => {
    const messages = new FakeMessagePort();
    const port = counters();
    const coordinator = new KeyCounterIpcCoordinator(messages, port);
    messages.command('counters.replay.begin', {
      counters: { hand: { A: 40 } },
    });
    await settle();
    expect(port.beginReplay).toHaveBeenCalledWith({ hand: { A: 40 } });
    messages.command('counters.replay.end', {});
    messages.command('counters.replay.end', {});
    await settle();
    expect(port.endReplay).toHaveBeenCalledTimes(1);
    await coordinator.dispose();
  });

  it('starts a replay without recorded counts from the current ones', async () => {
    const messages = new FakeMessagePort();
    const port = counters();
    const coordinator = new KeyCounterIpcCoordinator(messages, port);
    messages.command('counters.replay.begin', { counters: null });
    await settle();
    expect(port.beginReplay).toHaveBeenCalledWith(null);
    await coordinator.dispose();
  });

  it('counts the player again when the game leaves or the app closes', async () => {
    const messages = new FakeMessagePort();
    const port = counters();
    const coordinator = new KeyCounterIpcCoordinator(messages, port);
    messages.command('counters.replay.begin', { counters: null });
    await settle();
    messages.changeState('disconnected');
    await settle();
    expect(port.endReplay).toHaveBeenCalledTimes(1);

    messages.changeState('ready');
    messages.command('counters.replay.begin', { counters: null });
    await settle();
    await coordinator.dispose();
    expect(port.endReplay).toHaveBeenCalledTimes(2);
  });

  it('accepts only whole, non-negative presses', () => {
    expect(readCounts({ hand: { A: 1, 'NUMPAD 1': 0 } })).toEqual({
      hand: { A: 1, 'NUMPAD 1': 0 },
    });
    expect(readCounts({ hand: { A: -1 } })).toBeNull();
    expect(readCounts({ hand: { A: 1.5 } })).toBeNull();
    expect(readCounts({ hand: { A: '3' } })).toBeNull();
    expect(readCounts({ hand: [1] })).toBeNull();
    expect(readCounts(null)).toBeNull();
  });
});
