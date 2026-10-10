import { describe, expect, it, vi } from 'vitest';
import { FakeMessagePort } from '../__tests__/fakeMessagePort';
import { LiveMirrorIpcCoordinator } from './liveMirrorIpcCoordinator';

const sessionId = '123456789012345678901234567890ab';
const settle = async () => {
  for (let i = 0; i < 5; i++) await Promise.resolve();
};

describe('live in-game mirror messages', () => {
  it('announces availability and replies with the captured frame', async () => {
    const messages = new FakeMessagePort();
    const surface = {
      frame: vi.fn().mockResolvedValue({ sessionId, frames: [] }),
      end: vi.fn().mockResolvedValue(undefined),
    };
    const coordinator = new LiveMirrorIpcCoordinator(messages, surface);
    expect(messages.sent[0]).toMatchObject({
      name: 'mirror.available',
      payload: { protocolVersion: 1 },
    });
    messages.command('mirror.frame', { sessionId }, 'frame-1');
    await settle();
    expect(surface.frame).toHaveBeenCalledWith(sessionId);
    expect(messages.sent.at(-1)).toMatchObject({
      name: 'mirror.frame.ready',
      correlationId: 'frame-1',
    });
    await coordinator.dispose();
  });

  it('drops a frame completed after the session ends', async () => {
    const messages = new FakeMessagePort();
    let finishFrame: (value: unknown) => void;
    const surface = {
      frame: vi.fn().mockImplementation(
        () =>
          new Promise((resolve) => {
            finishFrame = resolve;
          }),
      ),
      end: vi.fn().mockResolvedValue(undefined),
    };
    const coordinator = new LiveMirrorIpcCoordinator(messages, surface);
    messages.command('mirror.frame', { sessionId }, 'frame-1');
    await settle();
    messages.command('mirror.end', { sessionId }, 'end-1');
    await settle();
    finishFrame({ sessionId, frames: [] });
    await settle();
    expect(
      messages.sent.some((message) => message.name === 'mirror.frame.ready'),
    ).toBe(false);
    expect(
      messages.sent.some(
        (message) =>
          message.name === 'mirror.ended' && message.correlationId === 'end-1',
      ),
    ).toBe(true);
    await coordinator.dispose();
  });

  it('clears the old session on disconnect before accepting another frame', async () => {
    const messages = new FakeMessagePort();
    const surface = {
      frame: vi.fn().mockResolvedValue({ sessionId, frames: [] }),
      end: vi.fn().mockResolvedValue(undefined),
    };
    const coordinator = new LiveMirrorIpcCoordinator(messages, surface);
    messages.changeState('retrying');
    await settle();
    expect(surface.end).toHaveBeenCalledWith(null);
    messages.changeState('ready');
    messages.command('mirror.frame', { sessionId }, 'frame-2');
    await settle();
    expect(
      messages.sent.some(
        (message) =>
          message.name === 'mirror.frame.ready' &&
          message.correlationId === 'frame-2',
      ),
    ).toBe(true);
    await coordinator.dispose();
  });
});
