import type {
  MessageState,
  SchedulerPort,
  Unsubscribe,
} from './ports/messagePort';
export interface ConnectionFactoryPort<T> {
  connect(): Promise<T>;
  isIncompatible(error: unknown): boolean;
}
/** Initial discovery policy; established connection recovery belongs to its transport adapter. */
export class ConnectionSupervisor<T> {
  private pending?: Promise<T>;
  private stopped = false;
  private retry?: Unsubscribe;
  private wake?: () => void;
  private readonly listeners = new Set<
    (state: MessageState, error?: unknown) => void
  >();
  constructor(
    private readonly factory: ConnectionFactoryPort<T>,
    private readonly scheduler: SchedulerPort,
  ) {}
  onState(
    listener: (state: MessageState, error?: unknown) => void,
  ): Unsubscribe {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }
  start(): Promise<T> {
    return (this.pending ??= this.connect());
  }
  stop(): void {
    this.stopped = true;
    this.retry?.();
    this.wake?.();
  }
  private async connect(): Promise<T> {
    let retryMs = 500;
    while (!this.stopped) {
      this.listeners.forEach((listener) => listener('connecting'));
      try {
        return await this.factory.connect();
      } catch (error) {
        if (this.factory.isIncompatible(error)) {
          this.listeners.forEach((listener) =>
            listener('version-mismatch', error),
          );
          throw error;
        }
        this.listeners.forEach((listener) => listener('retrying', error));
        await new Promise<void>((resolve) => {
          this.wake = resolve;
          this.retry = this.scheduler.delay(resolve, retryMs);
        });
        this.wake = undefined;
        this.retry = undefined;
        retryMs = Math.min(retryMs * 2, 10000);
      }
    }
    throw new Error('Application IPC stopped');
  }
}
