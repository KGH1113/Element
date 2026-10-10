import type {
  IpcChannel,
  IpcConnection,
  IpcConnectionState,
} from '@adofai-ipc/client';
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
  private readonly lifecycle: Unsubscribe[] = [];
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
        this.channel = client.namespace(
          namespace,
          namespace === 'impl-resourcepack'
            ? 1
            : namespace === 'tuf-replay-renderer'
              ? 2
              : undefined,
        );
        this.listeners.forEach((listener) => this.install(listener));
        const update = (
          state: IpcConnectionState = client.state,
          error?: unknown,
        ) => {
          this.setState(
            state === 'incompatible' ||
              this.channel.error?.code === 'feature_protocol_mismatch'
              ? 'version-mismatch'
              : state === 'closed'
                ? 'disposed'
                : state === 'reconnecting'
                  ? 'retrying'
                  : state !== 'connected'
                    ? 'disconnected'
                    : this.channel.status === 'ready'
                      ? 'ready'
                      : this.channel.status === 'error'
                        ? 'disconnected'
                        : 'connecting',
            error,
          );
        };
        const resume = () => {
          if (document.visibilityState === 'visible') client.checkConnection();
        };
        const online = () => client.checkConnection();
        for (const name of ['pageshow', 'focus']) {
          window.addEventListener(name, resume);
          this.lifecycle.push(() => window.removeEventListener(name, resume));
        }
        window.addEventListener('online', online);
        document.addEventListener('visibilitychange', resume);
        this.lifecycle.push(
          () => window.removeEventListener('online', online),
          () => document.removeEventListener('visibilitychange', resume),
          this.channel.onStatus((_status, error) =>
            update(client.state, error),
          ),
        );
        const unsubscribe = client.onState((state) => {
          update(state);
          if (state === 'closed' || state === 'incompatible')
            this.releaseLifecycle();
        });
        if (client.state === 'closed' || client.state === 'incompatible')
          unsubscribe();
        else this.lifecycle.push(unsubscribe);
      })
      .catch((error) =>
        this.setState(
          (error as { code?: string })?.code === 'INCOMPATIBLE'
            ? 'version-mismatch'
            : 'disconnected',
          error,
        ),
      );
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
  private releaseLifecycle(): void {
    this.lifecycle.splice(0).forEach((unsubscribe) => unsubscribe());
    this.listeners.forEach((subscription) => {
      subscription.cancel?.();
      subscription.cancel = undefined;
    });
  }
  private setState(state: MessageState, error?: unknown): void {
    if (this.state === state && !error) return;
    this.state = state;
    this.stateListeners.forEach((listener) => listener(state, error));
  }
}
