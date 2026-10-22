import { describe, expect, it, vi } from 'vitest';
import { FakeMessagePort } from '../__tests__/fakeMessagePort';
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
      payload: { nativeProtocolVersion: 1 },
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
    'drops a pending result after %s',
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
      messages.command('mirror.native.describe', { sessionId }, 'native-busy');
      expect(surface.describe).toHaveBeenCalledTimes(1);
      expect(messages.sent.at(-1)).toMatchObject({
        name: 'mirror.failed',
        payload: { code: 'mirror-busy', retryable: true },
      });
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
        messages.command('mirror.native.describe', { sessionId }, 'native-2');
        await settle();
        expect(messages.sent.at(-1)).toMatchObject({
          name: 'mirror.native.ready',
          correlationId: 'native-2',
        });
      }
      await coordinator.dispose();
    },
  );
});
