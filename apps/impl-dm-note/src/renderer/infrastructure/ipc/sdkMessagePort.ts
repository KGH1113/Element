import type { IpcChannel, IpcConnection } from '@adofai-ipc/client';
import type {
  MessageMetadata,
  MessagePort,
  MessageState,
  Unsubscribe,
} from '../../application/ports/messagePort';

/** Maps SDK lifecycle into the application port; feature code does not own sockets. */
export class SdkMessagePort implements MessagePort {
  state: MessageState = 'connecting';
  private channel?: IpcChannel;
  private readonly stateListeners = new Set<
    (state: MessageState, error?: unknown) => void
  >();
  private readonly listeners: Array<{
    name: string;
    command: boolean;
    listener: (payload: unknown, metadata: MessageMetadata) => void;
    cancel?: Unsubscribe;
  }> = [];
  constructor(connection: Promise<IpcConnection>, namespace: string) {
    void connection
      .then((client) => {
        this.channel = client.namespace(namespace);
        this.listeners.forEach((listener) => this.install(listener));
        client.onState((state) => {
          if (state === 'incompatible') this.setState('version-mismatch');
          else if (state === 'closed') this.setState('disposed');
          else if (state !== 'connected')
            this.setState(
              state === 'reconnecting' ? 'retrying' : 'disconnected',
            );
        });
        this.channel.onStatus((status) =>
          this.setState(
            status === 'ready'
              ? 'ready'
              : status === 'error'
                ? 'disconnected'
                : 'connecting',
          ),
        );
        this.setState(this.channel.status === 'ready' ? 'ready' : 'connecting');
      })
      .catch((error) => this.setState('version-mismatch', error));
  }
  onState(
    listener: (state: MessageState, error?: unknown) => void,
  ): Unsubscribe {
    this.stateListeners.add(listener);
    listener(this.state);
    return () => this.stateListeners.delete(listener);
  }
  on(
    name: string,
    listener: (payload: unknown, metadata: MessageMetadata) => void,
  ): Unsubscribe {
    return this.add(name, false, listener);
  }
  onCommand(
    name: string,
    listener: (payload: unknown, metadata: MessageMetadata) => void,
  ): Unsubscribe {
    return this.add(name, true, listener);
  }
  send(name: string, payload: unknown): string {
    if (!this.channel || this.state !== 'ready')
      throw new Error('Game IPC is not ready');
    return this.channel.send(name, payload);
  }
  emit(name: string, payload: unknown): string {
    if (!this.channel || this.state !== 'ready')
      throw new Error('Game IPC is not ready');
    return this.channel.emit(name, payload);
  }
  reply(id: string, name: string, payload: unknown): string {
    if (!this.channel || this.state !== 'ready')
      throw new Error('Game IPC is not ready');
    return this.channel.reply(id, name, payload);
  }
  private add(
    name: string,
    command: boolean,
    listener: (payload: unknown, metadata: MessageMetadata) => void,
  ): Unsubscribe {
    const subscription = {
      name,
      command,
      listener,
      cancel: undefined as Unsubscribe | undefined,
    };
    this.listeners.push(subscription);
    if (this.channel) this.install(subscription);
    return () => {
      subscription.cancel?.();
      const index = this.listeners.indexOf(subscription);
      if (index >= 0) this.listeners.splice(index, 1);
    };
  }
  private install(subscription: (typeof this.listeners)[number]): void {
    subscription.cancel = subscription.command
      ? this.channel.onCommand(subscription.name, subscription.listener)
      : this.channel.on(subscription.name, subscription.listener);
  }
  setConnectionState(state: MessageState, error?: unknown): void {
    this.setState(state, error);
  }
  private setState(state: MessageState, error?: unknown): void {
    if (this.state === state && !error) return;
    this.state = state;
    this.stateListeners.forEach((listener) => listener(state, error));
  }
}
