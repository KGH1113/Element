import { describe, expect, it } from 'vitest';
import {
  connectIpc,
  type IpcMessage,
  type IpcTransport,
} from 'adofai-ipc/transport';
import { SdkMessagePort } from './sdkMessagePort';

/** Exercises the actual pinned SDK through its native-transport boundary. */
class GameTransport implements IpcTransport {
  readonly sent: IpcMessage[] = [];
  private readonly messages = new Set<(text: string) => void>();
  private readonly closed = new Set<(reason?: unknown) => void>();

  constructor(private readonly featureMajor: number) {}

  async connect(): Promise<void> {
    this.receive({
      kind: 'welcome',
      name: 'connection.welcome',
      id: 'welcome',
      payload: {
        server: 'AdofaiIpc',
        serverVersion: '2.1.0',
        protocolVersion: 3,
        peerId: 'app',
        connectionId: 'session',
      },
    });
  }

  send(text: string): void {
    const message = JSON.parse(text) as IpcMessage;
    this.sent.push(message);
    if (message.kind === 'subscribe')
      this.receive({
        kind: 'namespace',
        namespace: message.namespace,
        name: 'namespace.state',
        id: 'ready',
        payload: { status: 'ready', featureProtocolMajor: this.featureMajor },
      });
  }

  async close(): Promise<void> {
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

  receive(message: IpcMessage): void {
    for (const listener of this.messages) listener(JSON.stringify(message));
  }
}

describe('ADOFAI-IPC 2.1 transport compatibility', () => {
  for (const [namespace, major, command, reply] of [
    ['impl-resourcepack', 1, 'mirror.frame', 'mirror.frame.ready'],
    ['tuf-replay-renderer', 2, 'dmnote.frame', 'dmnote.frame.ready'],
  ] as const) {
    it(`preserves commands and correlated replies for ${namespace}`, async () => {
      const transport = new GameTransport(major);
      const connection = await connectIpc({
        startPort: 32145,
        endPort: 32145,
        transportFactory: (url, protocol) => {
          expect(url).toBe('ws://127.0.0.1:32145/ipc/ws');
          expect(protocol).toBe('adofai-ipc.v3');
          return transport;
        },
      });
      try {
        const port = new SdkMessagePort(Promise.resolve(connection), namespace);
        const received: unknown[] = [];
        port.onCommand(command, (payload, message) => {
          received.push(payload);
          port.reply(message.id, reply, { frames: [] });
        });
        await Promise.resolve();
        expect(port.state).toBe('ready');
        transport.receive({
          kind: 'command',
          namespace,
          name: command,
          id: 'frame-1',
          payload: { sessionId: 'capture' },
        });
        expect(received).toEqual([{ sessionId: 'capture' }]);
        expect(transport.sent.at(-1)).toMatchObject({
          kind: 'event',
          namespace,
          name: reply,
          correlationId: 'frame-1',
          payload: { frames: [] },
        });
        let correlationId: string | undefined;
        port.on('key-limiter.profile.applied', (_payload, message) => {
          correlationId = message.correlationId;
        });
        const id = port.send('key-limiter.profile.set', { revision: 1 });
        transport.receive({
          kind: 'event',
          namespace,
          name: 'key-limiter.profile.applied',
          id: 'applied',
          correlationId: id,
          payload: { applied: true },
        });
        expect(correlationId).toBe(id);
      } finally {
        connection.close();
      }
    });
  }

  it('blocks domain messages when the feature major differs', async () => {
    const transport = new GameTransport(2);
    const connection = await connectIpc({
      transportFactory: () => transport,
    });
    try {
      const port = new SdkMessagePort(
        Promise.resolve(connection),
        'impl-resourcepack',
      );
      await Promise.resolve();
      expect(port.state).toBe('version-mismatch');
      expect(() => port.send('key-limiter.profile.set', {})).toThrow(
        'not ready',
      );
      expect(transport.sent.some((message) => message.kind === 'command')).toBe(
        false,
      );
    } finally {
      connection.close();
    }
  });
});
