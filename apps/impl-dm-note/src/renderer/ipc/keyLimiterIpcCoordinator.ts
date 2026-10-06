import type {
  MessagePort,
  MessageState,
  SchedulerPort,
  Unsubscribe,
} from '../application/ports/messagePort';
import type { KeyLimiterSyncRequest } from './keyLimiterSnapshot';
export type KeyLimiterConnectionState = MessageState;
export interface KeyLimiterSyncResponse {
  ok: boolean;
  applied: boolean;
  reason: string;
  message?: string;
  enabled: boolean;
  sessionId: string;
  revision: number;
  supportedKeys: string[];
  unsupportedKeys: string[];
}
export interface KeyLimiterCoordinatorOptions {
  messages: MessagePort;
  scheduler: SchedulerPort;
  debounceMs?: number;
  acknowledgementMs?: number;
  onStateChange?: (state: KeyLimiterConnectionState, error?: unknown) => void;
  onSyncStart?: (request: KeyLimiterSyncRequest) => void;
  onSyncResult?: (
    request: KeyLimiterSyncRequest,
    response: KeyLimiterSyncResponse,
  ) => void;
  onSyncError?: (request: KeyLimiterSyncRequest, error: unknown) => void;
}
/** Publishes the latest desired profile; transport readiness and results arrive as events. */
export class KeyLimiterIpcCoordinator {
  private latest: KeyLimiterSyncRequest | null = null;
  private pending: KeyLimiterSyncRequest | null = null;
  private inFlight: { id: string; request: KeyLimiterSyncRequest } | null =
    null;
  private cancelDebounce?: Unsubscribe;
  private cancelAcknowledgement?: Unsubscribe;
  private readonly subscriptions: Unsubscribe[];
  private disposed = false;
  private state: MessageState;
  constructor(private readonly options: KeyLimiterCoordinatorOptions) {
    this.state = options.messages.state;
    this.subscriptions = [
      options.messages.on('key-limiter.profile.applied', (payload, message) => {
        const current = this.inFlight;
        if (!current || message.correlationId !== current.id) return;
        this.clearInFlight();
        const result = payload as KeyLimiterSyncResponse;
        const valid =
          result &&
          typeof result.ok === 'boolean' &&
          typeof result.applied === 'boolean' &&
          typeof result.enabled === 'boolean' &&
          typeof result.reason === 'string' &&
          Number.isSafeInteger(result.revision) &&
          Array.isArray(result.supportedKeys) &&
          result.supportedKeys.every((key) => typeof key === 'string') &&
          Array.isArray(result.unsupportedKeys) &&
          result.unsupportedKeys.every((key) => typeof key === 'string');
        if (valid) options.onSyncResult?.(current.request, result);
        else
          options.onSyncError?.(
            current.request,
            new Error(
              'The game returned an unreadable key profile. Update the mod and reconnect.',
            ),
          );
        this.flush();
      }),
      ...['command.rejected', 'command.failed'].map((name) =>
        options.messages.on(name, (payload, message) => {
          const current = this.inFlight;
          if (!current || message.correlationId !== current.id) return;
          this.clearInFlight();
          options.onSyncError?.(current.request, payload);
          this.flush();
        }),
      ),
      options.messages.onState((state, error) => {
        this.state = state;
        options.onStateChange?.(state, error);
        if (state !== 'ready') {
          this.clearInFlight();
          this.pending = this.latest;
        } else {
          this.pending = this.latest;
          this.flush();
        }
      }),
    ];
  }
  get connectionState(): KeyLimiterConnectionState {
    return this.state;
  }
  enqueue(request: KeyLimiterSyncRequest): void {
    if (this.disposed) return;
    this.latest = this.pending = request;
    this.cancelDebounce?.();
    this.cancelDebounce = this.options.scheduler.delay(() => {
      this.cancelDebounce = undefined;
      this.flush();
    }, this.options.debounceMs ?? 75);
  }
  async flushNow(): Promise<void> {
    this.cancelDebounce?.();
    this.cancelDebounce = undefined;
    this.flush();
  }
  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.cancelDebounce?.();
    this.clearInFlight();
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
    this.latest = this.pending = null;
    this.state = 'disposed';
    this.options.onStateChange?.('disposed');
  }
  private clearInFlight(): void {
    this.cancelAcknowledgement?.();
    this.cancelAcknowledgement = undefined;
    this.inFlight = null;
  }
  private flush(): void {
    if (
      this.disposed ||
      this.state !== 'ready' ||
      this.inFlight ||
      !this.pending
    )
      return;
    const request = this.pending;
    this.pending = null;
    try {
      this.options.onSyncStart?.(request);
      const id = this.options.messages.send('key-limiter.profile.set', request);
      this.inFlight = { id, request };
      this.cancelAcknowledgement = this.options.scheduler.delay(() => {
        if (this.inFlight?.id !== id) return;
        this.clearInFlight();
        this.options.onSyncError?.(
          request,
          new Error(
            'The game did not confirm the key profile. Reconnect ADOFAI to try again.',
          ),
        );
        this.flush();
      }, this.options.acknowledgementMs ?? 5000);
    } catch (error) {
      this.pending = this.latest;
      this.options.onSyncError?.(request, error);
    }
  }
}
