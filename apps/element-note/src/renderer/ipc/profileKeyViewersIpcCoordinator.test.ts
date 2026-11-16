import { describe, expect, it, vi } from 'vitest';
import { FakeMessagePort } from '../__tests__/fakeMessagePort';
import {
  ProfileKeyViewersIpcCoordinator,
  readSwap,
  type ProfileKeyViewersPort,
} from './profileKeyViewersIpcCoordinator';

const settle = async () => {
  for (let i = 0; i < 10; i++) await Promise.resolve();
};

function keyViewers(overrides: Partial<ProfileKeyViewersPort> = {}) {
  return {
    owner: vi.fn().mockResolvedValue('profile-a'),
    save: vi.fn().mockResolvedValue(undefined),
    swap: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

describe("Element profiles' key viewers", () => {
  it('tells the game whose key viewers are on screen', async () => {
    const messages = new FakeMessagePort();
    const coordinator = new ProfileKeyViewersIpcCoordinator(
      messages,
      keyViewers(),
    );
    await settle();
    expect(messages.sent[0]).toMatchObject({
      name: 'key-viewers.available',
      payload: { protocolVersion: 1, owner: 'profile-a' },
    });
    coordinator.dispose();
  });

  it('saves to the named file and answers', async () => {
    const messages = new FakeMessagePort();
    const port = keyViewers();
    const coordinator = new ProfileKeyViewersIpcCoordinator(messages, port);
    messages.command(
      'key-viewers.save',
      { path: '/mods/Element/Profiles/a.json' },
      'save-1',
    );
    await settle();
    expect(port.save).toHaveBeenCalledWith('/mods/Element/Profiles/a.json');
    expect(messages.sent.at(-1)).toMatchObject({
      name: 'key-viewers.save.result',
      correlationId: 'save-1',
      payload: { ok: true, owner: 'profile-a' },
    });
    coordinator.dispose();
  });

  it('swaps one request at a time, in order', async () => {
    const messages = new FakeMessagePort();
    const order: string[] = [];
    let finishFirst: () => void = () => {};
    const port = keyViewers({
      swap: vi.fn(async (_save, _load, owner: string) => {
        order.push('start ' + owner);
        if (owner === 'b')
          await new Promise<void>((resolve) => (finishFirst = resolve));
        order.push('end ' + owner);
      }),
    });
    const coordinator = new ProfileKeyViewersIpcCoordinator(messages, port);
    messages.command(
      'key-viewers.swap',
      {
        save: '/p/a.json',
        load: { kind: 'firstStart' },
        owner: 'b',
        expected: 'a',
      },
      'swap-1',
    );
    messages.command(
      'key-viewers.swap',
      { save: null, load: { kind: 'file', path: '/p/c.json' }, owner: 'c' },
      'swap-2',
    );
    await settle();
    expect(order).toEqual(['start b']);
    finishFirst();
    await settle();
    expect(order).toEqual(['start b', 'end b', 'start c', 'end c']);
    expect(port.swap).toHaveBeenNthCalledWith(
      1,
      '/p/a.json',
      { kind: 'firstStart' },
      'b',
      'a',
    );
    expect(port.swap).toHaveBeenNthCalledWith(
      2,
      null,
      { kind: 'file', path: '/p/c.json' },
      'c',
      null,
    );
    coordinator.dispose();
  });

  it('answers a failed or malformed request so the game does not wait', async () => {
    const messages = new FakeMessagePort();
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const port = keyViewers({
      save: vi.fn().mockRejectedValue(new Error('disk full')),
    });
    const coordinator = new ProfileKeyViewersIpcCoordinator(messages, port);
    messages.command('key-viewers.save', { path: '/p/a.json' }, 'save-2');
    messages.command('key-viewers.swap', { owner: 'x' }, 'swap-3');
    await settle();
    expect(messages.sent.at(-2)).toMatchObject({
      correlationId: 'save-2',
      payload: { ok: false, code: 'key-viewers-failed' },
    });
    expect(messages.sent.at(-1)).toMatchObject({
      correlationId: 'swap-3',
      payload: { ok: false, code: 'key-viewers-request-invalid' },
    });
    expect(port.swap).not.toHaveBeenCalled();
    error.mockRestore();
    coordinator.dispose();
  });

  it('tells the game whose key viewers are on screen when a swap is refused', async () => {
    const messages = new FakeMessagePort();
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const port = keyViewers({
      owner: vi.fn().mockResolvedValue('b'),
      swap: vi.fn().mockRejectedValue('key-viewers-owner-changed'),
    });
    const coordinator = new ProfileKeyViewersIpcCoordinator(messages, port);
    messages.command(
      'key-viewers.swap',
      { save: '/p/a.json', load: { kind: 'keep' }, owner: 'c', expected: 'a' },
      'swap-4',
    );
    await settle();
    expect(messages.sent.at(-1)).toMatchObject({
      correlationId: 'swap-4',
      payload: { ok: false, code: 'key-viewers-owner-changed', owner: 'b' },
    });
    error.mockRestore();
    coordinator.dispose();
  });

  it('reads only whole swap requests', () => {
    expect(readSwap({ load: { kind: 'keep' }, owner: 'a' })).toEqual({
      save: null,
      load: { kind: 'keep' },
      owner: 'a',
      expected: null,
    });
    expect(readSwap({ load: { kind: 'file' }, owner: 'a' })).toBeNull();
    expect(readSwap({ load: { kind: 'keep' }, owner: '' })).toBeNull();
    expect(
      readSwap({ save: 3, load: { kind: 'keep' }, owner: 'a' }),
    ).toBeNull();
  });
});
