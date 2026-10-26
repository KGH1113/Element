import { afterEach, describe, expect, it, vi } from 'vitest';
import { FakeMessagePort, testScheduler } from '../__tests__/fakeMessagePort';
import { NativeViewerIpcCoordinator } from './nativeViewerIpcCoordinator';

const sessionId = '123456789012345678901234567890ab';
const settle = async () => {
  for (let i = 0; i < 5; i++) await Promise.resolve();
};

describe('native in-game viewers', () => {
  it('announces only native support and describes the OBS pages', async () => {
    const messages = new FakeMessagePort();
    const result = { sessionId, surfaces: [] };
    const surface = { describe: vi.fn().mockResolvedValue(result) };
    const coordinator = new NativeViewerIpcCoordinator(messages, surface);
    expect(messages.sent[0]).toMatchObject({
      name: 'mirror.available',
      payload: { nativeProtocolVersion: 1, nativePush: false },
    });
    expect(messages.sent[0].payload).not.toHaveProperty('protocolVersion');
    messages.command('mirror.native.describe', { sessionId }, 'native-1');
    await settle();
    expect(surface.describe).toHaveBeenCalledWith(sessionId);
    expect(messages.sent.at(-1)).toMatchObject({
      name: 'mirror.native.ready',
      correlationId: 'native-1',
      payload: result,
    });
    await coordinator.dispose();
  });

  it('rejects invalid sessions before calling the native adapter', async () => {
    const messages = new FakeMessagePort();
    const surface = { describe: vi.fn() };
    const coordinator = new NativeViewerIpcCoordinator(messages, surface);
    messages.command(
      'mirror.native.describe',
      { sessionId: '../invalid' },
      'bad',
    );
    await settle();
    expect(surface.describe).not.toHaveBeenCalled();
    expect(messages.sent.at(-1)).toMatchObject({
      name: 'mirror.failed',
      correlationId: 'bad',
      payload: { code: 'invalid-session', retryable: false },
    });
    await coordinator.dispose();
  });

  it.each([
    { error: { code: 'obs-required' }, code: 'obs-required' },
    {
      error: new Error('http://127.0.0.1/?token=must-not-leak'),
      code: 'native-unavailable',
    },
  ])('returns a safe failure: $code', async ({ error, code }) => {
    const messages = new FakeMessagePort();
    const surface = { describe: vi.fn().mockRejectedValue(error) };
    const coordinator = new NativeViewerIpcCoordinator(messages, surface);
    messages.command('mirror.native.describe', { sessionId }, 'native-1');
    await settle();
    expect(messages.sent.at(-1)).toMatchObject({
      name: 'mirror.failed',
      payload: { code, retryable: true },
    });
    expect(JSON.stringify(messages.sent)).not.toContain('must-not-leak');
    await coordinator.dispose();
  });

  it.each(['disconnect', 'dispose'] as const)(
    'shares one native call and drops a pending result after %s',
    async (action) => {
      const messages = new FakeMessagePort();
      let finish: (value: unknown) => void;
      const surface = {
        describe: vi.fn().mockImplementation(
          () =>
            new Promise((resolve) => {
              finish = resolve;
            }),
        ),
      };
      const coordinator = new NativeViewerIpcCoordinator(messages, surface);
      messages.command('mirror.native.describe', { sessionId }, 'native-1');
      messages.command('mirror.native.describe', { sessionId }, 'native-2');
      expect(surface.describe).toHaveBeenCalledTimes(1);
      if (action === 'disconnect') {
        messages.changeState('retrying');
        messages.changeState('ready');
      } else await coordinator.dispose();
      finish({ sessionId, surfaces: [] });
      await settle();
      expect(
        messages.sent.some((message) => message.name === 'mirror.native.ready'),
      ).toBe(false);
      if (action === 'disconnect') {
        surface.describe.mockResolvedValue({ sessionId, surfaces: [] });
        messages.command('mirror.native.describe', { sessionId }, 'native-3');
        await settle();
        expect(messages.sent.at(-1)).toMatchObject({
          name: 'mirror.native.ready',
          correlationId: 'native-3',
        });
      }
      await coordinator.dispose();
    },
  );

  it('answers concurrent requests from one native call', async () => {
    const messages = new FakeMessagePort();
    const result = { sessionId, surfaces: [] };
    const surface = { describe: vi.fn().mockResolvedValue(result) };
    const coordinator = new NativeViewerIpcCoordinator(messages, surface);
    messages.command('mirror.native.describe', { sessionId }, 'native-1');
    messages.command('mirror.native.describe', { sessionId }, 'native-2');
    await settle();
    expect(surface.describe).toHaveBeenCalledTimes(1);
    const replies = messages.sent.filter(
      (message) => message.name === 'mirror.native.ready',
    );
    expect(replies.map((message) => message.correlationId)).toEqual([
      'native-1',
      'native-2',
    ]);
    await coordinator.dispose();
  });
});

describe('native viewer change pushes', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  const setup = () => {
    vi.useFakeTimers();
    const messages = new FakeMessagePort();
    let changed: () => void = () => undefined;
    const unsubscribe = vi.fn();
    const surface = {
      describe: vi.fn().mockResolvedValue({ sessionId, surfaces: [] }),
      onChanged: vi.fn((listener: () => void) => {
        changed = listener;
        return unsubscribe;
      }),
    };
    const coordinator = new NativeViewerIpcCoordinator(
      messages,
      surface,
      testScheduler,
    );
    const pushes = () =>
      messages.sent.filter(
        (message) =>
          message.name === 'mirror.native.changed' ||
          message.name === 'mirror.native.unavailable',
      );
    return {
      messages,
      surface,
      coordinator,
      unsubscribe,
      pushes,
      change: () => changed(),
    };
  };

  const flush = async (milliseconds = 100) => {
    await vi.advanceTimersByTimeAsync(milliseconds);
    await settle();
  };

  it('announces push support', async () => {
    const { messages, coordinator } = setup();
    expect(messages.sent[0]).toMatchObject({
      name: 'mirror.available',
      payload: { nativeProtocolVersion: 1, nativePush: true },
    });
    await coordinator.dispose();
  });

  it('pushes only changed descriptors to the subscribed session', async () => {
    const { messages, surface, coordinator, pushes, change } = setup();
    change();
    await flush();
    expect(surface.describe).not.toHaveBeenCalled();

    messages.command('mirror.native.describe', { sessionId }, 'native-1');
    await settle();
    change();
    await flush();
    expect(pushes()).toHaveLength(0);

    const moved = {
      sessionId,
      surfaces: [
        { viewerKind: 'hand', url: 'http://127.0.0.1:1/', width: 1, height: 1 },
      ],
    };
    surface.describe.mockResolvedValue(moved);
    change();
    change();
    await flush();
    // One describe, one unchanged push, then one debounced push for both changes.
    expect(surface.describe).toHaveBeenCalledTimes(3);
    expect(pushes()).toHaveLength(1);
    expect(pushes()[0]).toMatchObject({
      name: 'mirror.native.changed',
      payload: moved,
    });
    expect(pushes()[0].correlationId).toBeUndefined();

    surface.describe.mockRejectedValue({ code: 'obs-required' });
    change();
    await flush();
    expect(pushes().at(-1)).toMatchObject({
      name: 'mirror.native.unavailable',
      payload: { sessionId, code: 'obs-required', retryable: true },
    });
    await coordinator.dispose();
  });

  it('retries while OBS is still starting', async () => {
    const { messages, surface, coordinator, pushes, change } = setup();
    surface.describe.mockRejectedValue({ code: 'obs-required' });
    messages.command('mirror.native.describe', { sessionId }, 'native-1');
    await settle();
    surface.describe.mockRejectedValueOnce({ code: 'obs-not-ready' });
    change();
    await flush();
    expect(pushes().at(-1)).toMatchObject({
      payload: { code: 'obs-not-ready' },
    });
    surface.describe.mockResolvedValue({ sessionId, surfaces: [] });
    await flush(500);
    expect(pushes().at(-1)).toMatchObject({ name: 'mirror.native.changed' });
    await coordinator.dispose();
  });

  it.each(['disconnect', 'dispose'] as const)(
    'stops pushing after %s',
    async (action) => {
      const { messages, coordinator, pushes, change, unsubscribe, surface } =
        setup();
      messages.command('mirror.native.describe', { sessionId }, 'native-1');
      await settle();
      surface.describe.mockResolvedValue({ sessionId, surfaces: [1] });
      if (action === 'disconnect') {
        messages.changeState('retrying');
        messages.changeState('ready');
      } else await coordinator.dispose();
      change();
      await flush();
      expect(pushes()).toHaveLength(0);
      if (action === 'dispose') expect(unsubscribe).toHaveBeenCalled();
      await coordinator.dispose();
    },
  );
});
