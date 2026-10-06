import { Channel, invoke } from '@tauri-apps/api/core';
import type { IpcTransport } from '@adofai-ipc/client';
type NativeEvent =
  { kind: 'message'; text: string } | { kind: 'closed'; reason: string };
/** An ordered, bounded native byte pipe. SDK protocol parsing stays in TypeScript. */
export class NativeWebSocketTransport implements IpcTransport {
  private readonly socketId = crypto.randomUUID();
  private readonly messages = new Set<(text: string) => void>();
  private readonly closed = new Set<(reason?: unknown) => void>();
  private ended = false;
  private chain: Promise<unknown> = Promise.resolve();
  constructor(
    private readonly url: string,
    private readonly protocol: string,
  ) {}
  async connect(): Promise<void> {
    const events = new Channel<NativeEvent>();
    events.onmessage = (event) => {
      if (this.ended) return;
      if (event.kind === 'message')
        this.messages.forEach((listener) => listener(event.text));
      else {
        this.ended = true;
        this.closed.forEach((listener) => listener(event.reason));
      }
    };
    await invoke('adofai_ipc_ws_connect', {
      socketId: this.socketId,
      url: this.url,
      protocol: this.protocol,
      events,
    });
    if (this.ended)
      await invoke('adofai_ipc_ws_close', { socketId: this.socketId });
  }
  send(text: string): Promise<void> {
    if (this.ended)
      return Promise.reject(new Error('Game IPC is disconnected'));
    const sending = this.chain.then(() =>
      invoke<void>('adofai_ipc_ws_send', { socketId: this.socketId, text }),
    );
    this.chain = sending.catch((error) => {
      this.closed.forEach((listener) => listener(error));
    });
    return sending;
  }
  async close(): Promise<void> {
    this.ended = true;
    await invoke('adofai_ipc_ws_close', { socketId: this.socketId }).catch(
      () => {},
    );
    this.messages.clear();
    this.closed.clear();
  }
  onMessage(listener: (text: string) => void): () => void {
    this.messages.add(listener);
    return () => this.messages.delete(listener);
  }
  onClose(listener: (reason?: unknown) => void): () => void {
    this.closed.add(listener);
    return () => this.closed.delete(listener);
  }
}
