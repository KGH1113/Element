import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FakeMessagePort, testScheduler } from '../__tests__/fakeMessagePort';
import { RenderIpcCoordinator } from './renderIpcCoordinator';
const sessionId = '123456789012345678901234567890ab';
const capabilities = {
  applicationId: 'app',
  applicationVersion: '0.1.0',
  platform: 'macos',
  nativeCapture: true,
  multiViewerCapture: true,
};
const settle = async () => {
  for (let i = 0; i < 5; i++) await Promise.resolve();
};
function setup() {
  const messages = new FakeMessagePort();
  const surface = {
    begin: vi.fn().mockResolvedValue({ surfaces: [] }),
    reset: vi.fn().mockResolvedValue({}),
    frame: vi.fn().mockResolvedValue({ frameIndex: 0 }),
    end: vi.fn().mockResolvedValue({ ended: true }),
    touch: vi.fn().mockResolvedValue(undefined),
  };
  const coordinator = new RenderIpcCoordinator({
    messages,
    surface,
    scheduler: testScheduler,
    capabilities,
  });
  messages.receive(
    'dmnote.attached',
    { protocolVersion: 2, leaseMs: 15000 },
    messages.sent[0].id,
  );
  return { messages, surface, coordinator };
}
describe('direct render messages', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());
  it('acknowledges native capture immediately and never polls for a command', async () => {
    const { messages, coordinator } = setup();
    messages.command('dmnote.begin', { sessionId }, 'begin');
    await settle();
    for (let frame = 0; frame < 10; frame++) {
      messages.command(
        'dmnote.frame',
        { sessionId, frameIndex: frame },
        `frame-${frame}`,
      );
      await settle();
    }
    expect(
      messages.sent.filter((message) => message.name === 'dmnote.frame.ready'),
    ).toHaveLength(10);
    expect(
      messages.sent.find((message) => message.name === 'dmnote.frame.ready')
        ?.correlationId,
    ).toBe('frame-0');
    expect(
      messages.sent.some((message) => /poll|hello|reply/.test(message.name)),
    ).toBe(false);
    await coordinator.dispose();
  });
  it('handles end while capture is stalled and discards the late frame', async () => {
    const { messages, surface, coordinator } = setup();
    let resolveFrame: (value: unknown) => void;
    surface.frame.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveFrame = resolve;
        }),
    );
    messages.command('dmnote.begin', { sessionId });
    await settle();
    messages.command('dmnote.frame', { sessionId, frameIndex: 0 }, 'frame');
    await settle();
    messages.command('dmnote.end', { sessionId }, 'end');
    await settle();
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    expect(
      messages.sent.some(
        (message) =>
          message.name === 'dmnote.ended' && message.correlationId === 'end',
      ),
    ).toBe(true);
    resolveFrame({ frameIndex: 0 });
    await settle();
    expect(
      messages.sent.some((message) => message.name === 'dmnote.frame.ready'),
    ).toBe(false);
    expect(
      messages.sent.some(
        (message) =>
          message.name === 'dmnote.failed' && message.correlationId === 'frame',
      ),
    ).toBe(true);
    await coordinator.dispose();
  });
  it('releases physical inputs on disconnect, reconnects capabilities, and uses wall clock pulses', async () => {
    const { messages, surface, coordinator } = setup();
    messages.command('dmnote.begin', { sessionId });
    await settle();
    await vi.advanceTimersByTimeAsync(1000);
    expect(surface.touch).toHaveBeenCalledTimes(1);
    expect(messages.sent.at(-1).name).toBe('dmnote.pulse');
    messages.changeState('retrying');
    await settle();
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    messages.changeState('ready');
    expect(messages.sent.at(-1).name).toBe('dmnote.attach');
    await coordinator.dispose();
    const count = messages.sent.length;
    await vi.advanceTimersByTimeAsync(5000);
    expect(messages.sent).toHaveLength(count);
  });
  it('reattaches once when another app releases the renderer without forming a broadcast loop', async () => {
    const { messages, coordinator } = setup();
    const attachment = messages.sent.find(
      (message) => message.name === 'dmnote.attach',
    );
    messages.receive(
      'command.rejected',
      { code: 'dmnote_busy' },
      attachment.id,
    );
    messages.receive('dmnote.availability.changed', {
      available: true,
      busy: false,
    });
    expect(
      messages.sent.filter((message) => message.name === 'dmnote.attach'),
    ).toHaveLength(1);
    messages.receive('dmnote.availability.changed', {
      available: false,
      busy: false,
    });
    messages.receive('dmnote.availability.changed', {
      available: false,
      busy: false,
    });
    expect(
      messages.sent.filter((message) => message.name === 'dmnote.attach'),
    ).toHaveLength(2);
    await coordinator.dispose();
  });
  it('does not execute duplicate commands twice', async () => {
    const { messages, surface, coordinator } = setup();
    messages.command('dmnote.begin', { sessionId }, 'begin');
    await settle();
    messages.command('dmnote.begin', { sessionId }, 'begin');
    await settle();
    expect(surface.begin).toHaveBeenCalledTimes(1);
    expect(
      messages.sent.filter((message) => message.name === 'dmnote.begun'),
    ).toHaveLength(2);
    await coordinator.dispose();
  });
});
