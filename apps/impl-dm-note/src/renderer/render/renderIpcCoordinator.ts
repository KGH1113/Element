import { tryConnect } from '@adofai-ipc/client';
import { invoke } from '@tauri-apps/api/core';
import { nativeIpcFetch } from '../ipc/nativeIpcFetch';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';

const NAMESPACE = 'tuf-replay-renderer';
const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
type Command = {
  id: string;
  method: 'begin' | 'reset' | 'frame' | 'end';
  params: Record<string, unknown>;
};

// Main window retains its real clock. Only the isolated render surface receives
// a virtual clock, so connection retries and cleanup still work on a stalled frame.
export async function runRenderIpcCoordinator() {
  const applicationId = crypto.randomUUID();
  let sessionId: string | null = null;
  let stopped = false;
  const end = async () => {
    await invoke('dmnote_render_end', { sessionId }).catch(() => {});
    sessionId = null;
  };
  window.addEventListener('pagehide', () => {
    stopped = true;
    void end();
  });
  while (!stopped) {
    try {
      const client = await tryConnect({ fetch: nativeIpcFetch });
      await client.waitForNamespace(NAMESPACE, {
        status: 'ready',
        timeoutMs: 2000,
        pollIntervalMs: 200,
      });
      const call = <T>(method: string, params: unknown) =>
        client.call<T>({ namespace: NAMESPACE, method, params });
      await call('dmnote.hello', {
        applicationId,
        protocolVersion: 1,
        applicationVersion: __APP_VERSION__,
        platform: /Mac/.test(navigator.userAgent)
          ? 'macos'
          : /Windows/.test(navigator.userAgent)
            ? 'windows'
            : 'unsupported',
        nativeCapture: /Mac|Windows/.test(navigator.userAgent),
      });
      type Reply = {
        id: string;
        result?: unknown;
        error?: { code: string; message: string };
      };
      const executions = new Map<string, { reply?: Reply }>();
      let wake: (() => void) | undefined;
      let touchedAt = 0;
      const execute = async (
        command: Command,
        execution: { reply?: Reply },
      ) => {
        const commandSession = String(command.params.sessionId);
        try {
          let result: unknown;
          if (command.method === 'begin') {
            if (sessionId) throw new Error('render-busy');
            sessionId = commandSession;
            const pluginElements = JSON.parse(
              JSON.stringify(usePluginDisplayElementStore.getState().elements),
            );
            const storage = Object.fromEntries(
              Object.keys(localStorage).map((key) => [
                key,
                localStorage.getItem(key),
              ]),
            );
            result = await invoke('dmnote_render_begin', {
              request: {
                ...command.params,
                pluginElements,
                localStorage: storage,
              },
            });
          } else if (command.method === 'end') {
            result = await invoke('dmnote_render_end', {
              sessionId: commandSession,
            });
            if (sessionId === commandSession) sessionId = null;
          } else
            result = await invoke(`dmnote_render_${command.method}`, {
              request: command.params,
            });
          execution.reply = { id: command.id, result };
        } catch (error) {
          const message = String(error);
          if (sessionId === commandSession) await end();
          const code = /busy/.test(message)
            ? 'dmnote_busy'
            : /timeout/.test(message)
              ? 'dmnote_frame_timeout'
              : /session-ended|surface-closed/.test(message)
                ? 'dmnote_disconnected'
                : 'dmnote_capture_failed';
          execution.reply = {
            id: command.id,
            error: {
              code,
              message: `ImplDmNote failed during ${command.method === 'begin' ? 'overlay initialization' : command.method === 'frame' ? 'frame capture' : command.method === 'reset' ? 'overlay reset' : 'render cleanup'}: ${message}`,
            },
          };
        }
      };
      while (!stopped) {
        const poll = await call<{ command: Command | null; active: boolean }>(
          'dmnote.poll',
          { applicationId },
        );
        // Polling stays responsive while native frame capture is pending. An end
        // command can immediately destroy that surface and release the input gate.
        if (sessionId && performance.now() - touchedAt >= 1000) {
          await invoke('dmnote_render_touch');
          touchedAt = performance.now();
        }
        if (!poll.active && sessionId) await end();
        if (poll.command && !executions.has(poll.command.id)) {
          const execution = {};
          executions.set(poll.command.id, execution);
          void execute(poll.command, execution).finally(() => wake?.());
        }
        let replied = false;
        for (const [id, execution] of executions) {
          if (!execution.reply) continue;
          await call('dmnote.reply', { applicationId, ...execution.reply });
          executions.delete(id);
          replied = true;
        }
        // Do not add a fixed sleep to every captured frame. Completion wakes this
        // loop immediately; pending captures still poll for cancellation.
        if (replied) continue;
        let timer: ReturnType<typeof setTimeout> | undefined;
        await new Promise<void>((resolve) => {
          wake = resolve;
          timer = setTimeout(resolve, poll.active ? (executions.size ? 50 : 5) : 500);
          if ([...executions.values()].some((execution) => execution.reply)) resolve();
        });
        clearTimeout(timer);
        wake = undefined;
      }
    } catch {
      // Restoring early is safe; a disconnected renderer cannot consume frames.
      if (sessionId) await end();
      await sleep(1000);
    }
  }
}
