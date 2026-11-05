import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FakeMessagePort, testScheduler } from '../__tests__/fakeMessagePort';
import {
  ElementRenderIpcCoordinator,
  elementRenderReply,
} from './elementRenderIpcCoordinator';

const sessionId = '123456789012345678901234567890ab';
const otherSession = 'ffffffffffffffffffffffffffffffff';
const settle = async () => {
  for (let i = 0; i < 5; i++) await Promise.resolve();
};

function setup() {
  const messages = new FakeMessagePort();
  let clock = 0;
  const surface = {
    begin: vi.fn().mockResolvedValue({ surfaces: [{ viewerKind: 'hand' }] }),
    reset: vi.fn().mockResolvedValue({}),
    frame: vi.fn().mockResolvedValue({ frames: [] }),
    end: vi.fn().mockResolvedValue({ ended: true }),
    touch: vi.fn().mockResolvedValue(undefined),
  };
  const coordinator = new ElementRenderIpcCoordinator(
    messages,
    surface,
    testScheduler,
    () => clock,
  );
  const replies = () =>
    messages.sent.filter((message) => message.name === elementRenderReply);
  const advance = (milliseconds: number) => {
    clock += milliseconds;
    vi.advanceTimersByTime(milliseconds);
  };
  return { messages, surface, coordinator, replies, advance };
}

describe('Element render messages', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('announces renders when the game channel is ready', async () => {
    const { messages, coordinator } = setup();
    expect(messages.sent[0]).toMatchObject({
      name: 'render.available',
      payload: { protocolVersion: 1 },
    });
    await coordinator.dispose();
  });

  it('answers begin and frames with commands matched to the request', async () => {
    const { messages, surface, coordinator, replies } = setup();
    const begin = messages.command('render.begin', {
      sessionId,
      surfaces: [{ viewerKind: 'hand' }],
    });
    await settle();
    expect(surface.begin).toHaveBeenCalledWith(
      expect.objectContaining({ sessionId }),
    );
    expect(replies()[0].payload).toMatchObject({
      requestId: begin,
      sessionId,
      ok: true,
      result: { surfaces: [{ viewerKind: 'hand' }] },
    });
    const frame = messages.command('render.frame', {
      sessionId,
      frameIndex: 0,
      outputTimeUs: 1_000_000,
      events: [],
    });
    await settle();
    expect(replies()[1].payload).toMatchObject({
      requestId: frame,
      ok: true,
    });
    // Answers are commands to the game, never events it would read late.
    expect(replies().every((reply) => reply.correlationId === undefined)).toBe(
      true,
    );
    await coordinator.dispose();
  });

  it('keeps the running session when another begin arrives', async () => {
    const { messages, surface, coordinator, replies } = setup();
    messages.command('render.begin', { sessionId });
    await settle();
    messages.command('render.begin', { sessionId: otherSession });
    await settle();
    expect(replies()[1].payload).toMatchObject({
      ok: false,
      error: { code: 'render-busy' },
    });
    expect(surface.begin).toHaveBeenCalledTimes(1);
    expect(surface.end).not.toHaveBeenCalled();
    messages.command('render.frame', { sessionId, frameIndex: 0 });
    await settle();
    expect(replies()[2].payload).toMatchObject({ ok: true });
    await coordinator.dispose();
  });

  it('ends the session and reports the code when a frame fails', async () => {
    const { messages, surface, coordinator, replies } = setup();
    surface.frame.mockRejectedValueOnce(
      'render-frame-timeout: /Users/someone/cache',
    );
    messages.command('render.begin', { sessionId });
    await settle();
    messages.command('render.frame', { sessionId, frameIndex: 0 });
    await settle();
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    expect(replies()[1].payload).toMatchObject({
      ok: false,
      error: { code: 'render-frame-timeout' },
    });
    messages.command('render.frame', { sessionId, frameIndex: 1 });
    await settle();
    expect(replies()[2].payload).toMatchObject({
      ok: false,
      error: { code: 'render-session-ended' },
    });
    await coordinator.dispose();
  });

  it('refuses frames for other sessions and malformed ids', async () => {
    const { messages, coordinator, replies } = setup();
    messages.command('render.begin', { sessionId: 'not-a-session' });
    await settle();
    expect(replies()[0].payload).toMatchObject({
      ok: false,
      error: { code: 'render-invalid-session' },
    });
    messages.command('render.begin', { sessionId });
    await settle();
    messages.command('render.frame', { sessionId: otherSession });
    await settle();
    expect(replies()[2].payload).toMatchObject({
      ok: false,
      error: { code: 'render-session-ended' },
    });
    await coordinator.dispose();
  });

  it('ends on End, and only for the current session', async () => {
    const { messages, surface, coordinator } = setup();
    messages.command('render.begin', { sessionId });
    await settle();
    messages.command('render.end', { sessionId: otherSession });
    await settle();
    expect(surface.end).not.toHaveBeenCalled();
    messages.command('render.end', { sessionId });
    await settle();
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    await coordinator.dispose();
  });

  it('keeps the backend alive while the game pulses and ends when it stops', async () => {
    const { messages, surface, coordinator, advance } = setup();
    messages.command('render.begin', { sessionId });
    await settle();
    for (let second = 0; second < 40; second++) {
      advance(1000);
      messages.command('render.pulse', { sessionId });
      await settle();
    }
    expect(surface.touch).toHaveBeenCalled();
    expect(surface.end).not.toHaveBeenCalled();
    advance(31_000);
    await settle();
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    await coordinator.dispose();
  });

  it('ends the session when the game connection drops', async () => {
    const { messages, surface, coordinator } = setup();
    messages.command('render.begin', { sessionId });
    await settle();
    messages.changeState('retrying');
    await settle();
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    await coordinator.dispose();
  });
});
