import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FakeMessagePort, testScheduler } from '../__tests__/fakeMessagePort';
import {
  ElementRenderIpcCoordinator,
  elementRenderReply,
} from './elementRenderIpcCoordinator';

const sessionId = '123456789012345678901234567890ab';
const otherSession = 'ffffffffffffffffffffffffffffffff';
const videos = [{ viewerKind: 'hand', path: '/cache/hand.mov', frames: 2 }];
const settle = async () => {
  for (let i = 0; i < 20; i++) await Promise.resolve();
};

function setup() {
  const messages = new FakeMessagePort();
  let clock = 0;
  const surface = {
    begin: vi.fn().mockResolvedValue({ surfaces: [{ viewerKind: 'hand' }] }),
    frame: vi.fn().mockResolvedValue(null),
    finish: vi.fn().mockResolvedValue({ videos }),
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
  const frame = (frameIndex: number, session = sessionId) =>
    messages.command('render.frame', {
      sessionId: session,
      frameIndex,
      outputTimeUs: 1_000_000 + frameIndex * 16_667,
      events: [],
    });
  return { messages, surface, coordinator, replies, advance, frame };
}

describe('Element render messages', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('announces video renders when the game channel is ready', async () => {
    const { messages, coordinator } = setup();
    expect(messages.sent[0]).toMatchObject({
      name: 'render.available',
      payload: { protocolVersion: 2, recordedCounters: true },
    });
    await coordinator.dispose();
  });

  it('queues frames without answers and answers finish with the videos', async () => {
    const { messages, surface, coordinator, replies, frame } = setup();
    const begin = messages.command('render.begin', {
      sessionId,
      fps: 60,
      ffmpegPath: '/ffmpeg',
      surfaces: [{ viewerKind: 'hand' }],
    });
    await settle();
    expect(surface.begin).toHaveBeenCalledWith(
      expect.objectContaining({ sessionId, fps: 60, ffmpegPath: '/ffmpeg' }),
    );
    expect(replies()[0].payload).toMatchObject({
      requestId: begin,
      sessionId,
      ok: true,
      result: { surfaces: [{ viewerKind: 'hand' }] },
    });
    frame(0);
    frame(2);
    await settle();
    expect(
      surface.frame.mock.calls.map(([request]) => request.frameIndex),
    ).toEqual([0, 2]);
    expect(replies()).toHaveLength(1);
    const finish = messages.command('render.finish', { sessionId });
    await settle();
    expect(surface.finish).toHaveBeenCalledWith({ sessionId });
    expect(replies()[1].payload).toMatchObject({
      requestId: finish,
      sessionId,
      ok: true,
      result: { videos },
    });
    // Answers are commands to the game, never events it would read late.
    expect(replies().every((reply) => reply.correlationId === undefined)).toBe(
      true,
    );
    expect(surface.end).not.toHaveBeenCalled();
    await coordinator.dispose();
  });

  it('hands frames on in order, after begin, even when one is slow', async () => {
    const { messages, surface, coordinator, frame } = setup();
    let began!: (value: unknown) => void;
    surface.begin.mockReturnValueOnce(
      new Promise((resolve) => (began = resolve)),
    );
    let queued!: (value: unknown) => void;
    surface.frame.mockReturnValueOnce(
      new Promise((resolve) => (queued = resolve)),
    );
    messages.command('render.begin', { sessionId });
    frame(0);
    frame(1);
    await settle();
    expect(surface.frame).not.toHaveBeenCalled();
    began({ surfaces: [] });
    await settle();
    expect(surface.frame).toHaveBeenCalledTimes(1);
    queued(null);
    await settle();
    expect(
      surface.frame.mock.calls.map(([request]) => request.frameIndex),
    ).toEqual([0, 1]);
    await coordinator.dispose();
  });

  it('waits for queued frames before finishing', async () => {
    const { messages, surface, coordinator, replies, frame } = setup();
    let queued!: (value: unknown) => void;
    surface.frame.mockReturnValueOnce(
      new Promise((resolve) => (queued = resolve)),
    );
    messages.command('render.begin', { sessionId });
    await settle();
    frame(0);
    messages.command('render.finish', { sessionId });
    await settle();
    expect(surface.finish).not.toHaveBeenCalled();
    queued(null);
    await settle();
    expect(surface.finish).toHaveBeenCalledTimes(1);
    expect(replies()[1].payload).toMatchObject({ ok: true });
    await coordinator.dispose();
  });

  it('keeps the running session when another begin arrives', async () => {
    const { messages, surface, coordinator, replies, frame } = setup();
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
    frame(0);
    await settle();
    expect(surface.frame).toHaveBeenCalledTimes(1);
    await coordinator.dispose();
  });

  it('drops frames after a failed one and finishes with its code', async () => {
    const { messages, surface, coordinator, replies, frame } = setup();
    surface.frame.mockRejectedValueOnce(
      'render-frame-out-of-sequence: /Users/someone/cache',
    );
    messages.command('render.begin', { sessionId });
    await settle();
    frame(0);
    frame(1);
    await settle();
    expect(surface.frame).toHaveBeenCalledTimes(1);
    expect(replies()).toHaveLength(1);
    messages.command('render.finish', { sessionId });
    await settle();
    expect(surface.finish).not.toHaveBeenCalled();
    expect(replies()[1].payload).toMatchObject({
      ok: false,
      error: { code: 'render-frame-out-of-sequence' },
    });
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    await coordinator.dispose();
  });

  it('reports a failed finish and ends the session', async () => {
    const { messages, surface, coordinator, replies } = setup();
    surface.finish.mockRejectedValueOnce('render-ffmpeg-failed: exit status 1');
    messages.command('render.begin', { sessionId });
    await settle();
    messages.command('render.finish', { sessionId });
    await settle();
    expect(replies()[1].payload).toMatchObject({
      ok: false,
      error: { code: 'render-ffmpeg-failed' },
    });
    expect(surface.end).toHaveBeenCalledWith(sessionId);
    await coordinator.dispose();
  });

  it('ignores frames of other sessions and refuses malformed ids', async () => {
    const { messages, surface, coordinator, replies, frame } = setup();
    messages.command('render.begin', { sessionId: 'not-a-session' });
    await settle();
    expect(replies()[0].payload).toMatchObject({
      ok: false,
      error: { code: 'render-invalid-session' },
    });
    messages.command('render.begin', { sessionId });
    await settle();
    frame(0, otherSession);
    await settle();
    expect(surface.frame).not.toHaveBeenCalled();
    messages.command('render.finish', { sessionId: otherSession });
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

  it('keeps finishing the videos while the game waits in silence', async () => {
    const { messages, surface, coordinator, replies, advance } = setup();
    let finished!: (value: unknown) => void;
    surface.finish.mockReturnValueOnce(
      new Promise((resolve) => (finished = resolve)),
    );
    messages.command('render.begin', { sessionId });
    await settle();
    messages.command('render.finish', { sessionId });
    await settle();
    advance(60_000);
    await settle();
    expect(surface.end).not.toHaveBeenCalled();
    finished({ videos });
    await settle();
    expect(replies()[1].payload).toMatchObject({
      ok: true,
      result: { videos },
    });
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
