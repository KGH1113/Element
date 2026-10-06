import { invoke } from '@tauri-apps/api/core';
import {
  KeyLimiterIpcCoordinator,
  type KeyLimiterCoordinatorOptions,
} from '../ipc/keyLimiterIpcCoordinator';
import { RenderIpcCoordinator } from '../render/renderIpcCoordinator';
import type { SchedulerPort } from '../application/ports/messagePort';
import { SdkMessagePort } from '../infrastructure/ipc/sdkMessagePort';
import { connectIpc, type IpcConnection } from '@adofai-ipc/client';
import { NativeWebSocketTransport } from '../infrastructure/ipc/nativeWebSocketTransport';
import { ConnectionSupervisor } from '../application/connectionSupervisor';
import type { MessageState } from '../application/ports/messagePort';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
export const wallClockScheduler: SchedulerPort = {
  delay(callback, milliseconds) {
    const id = setTimeout(callback, milliseconds);
    return () => clearTimeout(id);
  },
  repeat(callback, milliseconds) {
    const id = setInterval(callback, milliseconds);
    return () => clearInterval(id);
  },
};
let supervisor: ConnectionSupervisor<IpcConnection> | undefined;
const messagePorts = new Map<string, SdkMessagePort>();
function applicationMessages(namespace: string): SdkMessagePort {
  supervisor ??= new ConnectionSupervisor(
    {
      connect: () =>
        connectIpc({
          transportFactory: (url, protocol) =>
            new NativeWebSocketTransport(url, protocol),
        }),
      isIncompatible: (error) =>
        (error as { code?: string })?.code === 'INCOMPATIBLE',
    },
    wallClockScheduler,
  );
  let port = messagePorts.get(namespace);
  if (!port) {
    port = new SdkMessagePort(supervisor.start(), namespace);
    messagePorts.set(namespace, port);
    supervisor.onState((state: MessageState, error) =>
      port.setConnectionState(state, error),
    );
  }
  return port;
}
async function closeApplicationMessages(): Promise<void> {
  const current = supervisor;
  supervisor = undefined;
  current?.stop();
  await current
    ?.start()
    .then((connection) => connection.close())
    .catch(() => {});
  messagePorts.clear();
}
export function createKeyLimiterCoordinator(
  options: Omit<KeyLimiterCoordinatorOptions, 'messages' | 'scheduler'> = {},
): KeyLimiterIpcCoordinator {
  return new KeyLimiterIpcCoordinator({
    ...options,
    messages: applicationMessages('impl-resourcepack'),
    scheduler: wallClockScheduler,
  });
}
export function startRenderMessages(): () => Promise<void> {
  const platform = /Mac/.test(navigator.userAgent)
    ? 'macos'
    : /Windows/.test(navigator.userAgent)
      ? 'windows'
      : 'unsupported';
  const coordinator = new RenderIpcCoordinator({
    messages: applicationMessages('tuf-replay-renderer'),
    scheduler: wallClockScheduler,
    capabilities: {
      applicationId: crypto.randomUUID(),
      applicationVersion: __APP_VERSION__,
      platform,
      nativeCapture: platform !== 'unsupported',
      multiViewerCapture: true,
    },
    surface: {
      begin: (request) =>
        invoke('dmnote_render_begin', {
          request: {
            ...request,
            pluginElements: JSON.parse(
              JSON.stringify(usePluginDisplayElementStore.getState().elements),
            ),
            localStorage: Object.fromEntries(
              Object.keys(localStorage).map((key) => [
                key,
                localStorage.getItem(key),
              ]),
            ),
          },
        }),
      reset: (request) => invoke('dmnote_render_reset', { request }),
      frame: (request) => invoke('dmnote_render_frame', { request }),
      end: (sessionId) => invoke('dmnote_render_end', { sessionId }),
      touch: () => invoke('dmnote_render_touch'),
    },
  });
  const stop = async () => {
    await coordinator.dispose();
    await closeApplicationMessages();
  };
  window.addEventListener(
    'pagehide',
    () => {
      void stop();
    },
    { once: true },
  );
  return stop;
}
