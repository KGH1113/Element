import { afterEach, describe, expect, it, vi } from 'vitest';

const dependencies = vi.hoisted(() => ({ connect: vi.fn(), invoke: vi.fn() }));
vi.mock('@adofai-ipc/client', () => ({ tryConnect: dependencies.connect }));
vi.mock('@tauri-apps/api/core', () => ({ invoke: dependencies.invoke }));
vi.mock('../ipc/nativeIpcFetch', () => ({ nativeIpcFetch: vi.fn() }));
vi.mock('@stores/plugin/usePluginDisplayElementStore', () => ({
  usePluginDisplayElementStore: { getState: () => ({ elements: [] }) },
}));
import { runRenderIpcCoordinator } from './renderIpcCoordinator';

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe('automatic render IPC', () => {
  it('processes cancellation while a native frame is pending and restores the session', async () => {
    vi.useFakeTimers();
    vi.stubGlobal('localStorage', Object.create({ getItem: () => null }));
    const replies = new Set<string>();
    let gated = false,
      frameStarted = false,
      endPosted = false;
    let rejectFrame: (error: Error) => void;
    const frame = new Promise((_resolve, reject) => {
      rejectFrame = reject;
    });
    void frame.catch(() => {});
    const sessionId = '123456789012345678901234567890ab';
    const params = {
      sessionId,
      width: 320,
      height: 320,
      viewerKind: 'hand',
      initialOutputTimeUs: -200000,
    };
    dependencies.invoke.mockImplementation(async (name: string) => {
      if (name === 'dmnote_render_begin') {
        gated = true;
        return {};
      }
      if (name === 'dmnote_render_frame') {
        frameStarted = true;
        return frame;
      }
      if (name === 'dmnote_render_end') {
        gated = false;
        rejectFrame?.(new Error('render-session-ended'));
        return { ended: true };
      }
    });
    const call = vi.fn(async ({ method, params: request }) => {
      if (method === 'dmnote.hello') return { protocolVersion: 1 };
      if (method === 'dmnote.reply') {
        replies.add(request.id);
        if (request.id === 'end') window.dispatchEvent(new Event('pagehide'));
        return { accepted: true };
      }
      if (!replies.has('begin'))
        return {
          active: true,
          command: { id: 'begin', method: 'begin', params },
        };
      if (!frameStarted)
        return {
          active: true,
          command: { id: 'frame', method: 'frame', params },
        };
      if (!endPosted) {
        expect(gated).toBe(true);
        endPosted = true;
      }
      return { active: true, command: { id: 'end', method: 'end', params } };
    });
    dependencies.connect.mockResolvedValue({
      call,
      waitForNamespace: vi.fn().mockResolvedValue({ status: 'ready' }),
    });
    const running = runRenderIpcCoordinator();
    await vi.advanceTimersByTimeAsync(500);
    await running;
    expect(frameStarted).toBe(true);
    expect(replies.has('end')).toBe(true);
    expect(gated).toBe(false);
    expect(
      dependencies.invoke.mock.calls.filter(
        ([name]) => name === 'dmnote_render_begin',
      ),
    ).toHaveLength(1);
    expect(
      dependencies.invoke.mock.calls.filter(
        ([name]) => name === 'dmnote_render_frame',
      ),
    ).toHaveLength(1);
  });
});
