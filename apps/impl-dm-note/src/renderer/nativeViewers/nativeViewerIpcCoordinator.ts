import type {
  MessagePort,
  SchedulerPort,
  Unsubscribe,
} from '../application/ports/messagePort';

export interface NativeViewerSurfacePort {
  describe(sessionId: string): Promise<unknown>;
  /** Fires when OBS mode, its token/port or a replay render changes. */
  onChanged?(listener: () => void): Unsubscribe;
}

const failures = {
  'invalid-session': 'Invalid in-game viewer session.',
  'obs-required':
    'Enable OBS mode in ImplDmNote to use the native in-game viewers.',
  'obs-not-ready': 'The OBS viewer service is starting. Try again shortly.',
  'replay-render-active':
    'A replay render is using ImplDmNote. Try again after it finishes.',
  'native-unavailable':
    'The native in-game viewers could not be prepared. Try again.',
} as const;

type FailureCode = keyof typeof failures;
// A success carries `result`; a failure carries `code`. (Discriminated
// narrowing on `ok` needs strictNullChecks, which this project disables.)
interface Outcome {
  ok: boolean;
  result?: unknown;
  code?: FailureCode;
}

interface Subscription {
  sessionId: string;
  generation: number;
  last?: string;
}

// Changes arrive in bursts (OBS start emits status, snapshot and port).
const changeDebounceMs = 100;
// OBS sets running before its port; describe again once it can answer.
const notReadyRetryMs = 500;

/**
 * Describes pages without capturing pixels or maintaining a file session.
 * After a game session describes once, descriptor changes are pushed as
 * uncorrelated events so the game does not need to poll.
 */
export class NativeViewerIpcCoordinator {
  private readonly subscriptions: Unsubscribe[] = [];
  private readonly inFlight = new Map<string, Promise<Outcome>>();
  private session?: Subscription;
  private pendingChange?: Unsubscribe;
  private generation = 0;
  private disposed = false;

  constructor(
    private readonly messages: MessagePort,
    private readonly surface: NativeViewerSurfacePort,
    private readonly scheduler?: SchedulerPort,
  ) {
    this.subscriptions.push(
      messages.onCommand('mirror.native.describe', (payload, message) => {
        void this.describe(payload as Record<string, unknown>, message.id);
      }),
      messages.onState((state) => {
        this.generation++;
        this.session = undefined;
        this.cancelChange();
        if (state === 'ready')
          messages.emit('mirror.available', {
            nativeProtocolVersion: 1,
            nativePush: this.canPush,
          });
      }),
    );
    if (surface.onChanged && scheduler)
      this.subscriptions.push(
        surface.onChanged(() => this.scheduleChange(changeDebounceMs)),
      );
  }

  private get canPush(): boolean {
    return Boolean(this.surface.onChanged && this.scheduler);
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.generation++;
    this.session = undefined;
    this.cancelChange();
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
  }

  private async describe(
    payload: Record<string, unknown>,
    id: string,
  ): Promise<void> {
    const sessionId = payload?.sessionId;
    if (typeof sessionId !== 'string' || !/^[a-f0-9]{32}$/i.test(sessionId)) {
      this.reply(id, 'mirror.failed', {
        code: 'invalid-session',
        message: failures['invalid-session'],
        retryable: false,
      });
      return;
    }
    const generation = this.generation;
    // The latest valid session subscribes to pushes, even when this describe
    // fails, so turning OBS mode on later still reaches the game.
    if (this.session?.sessionId !== sessionId)
      this.session = { sessionId, generation };
    const outcome = await this.describeOnce(sessionId);
    if (this.disposed || this.generation !== generation) return;
    if (this.session?.sessionId === sessionId)
      this.session.last = fingerprint(outcome);
    if (outcome.ok) this.reply(id, 'mirror.native.ready', outcome.result);
    else
      this.reply(
        id,
        'mirror.failed',
        failure(outcome.code ?? 'native-unavailable'),
      );
  }

  /** Concurrent requests for one session share a single native call. */
  private describeOnce(sessionId: string): Promise<Outcome> {
    let pending = this.inFlight.get(sessionId);
    if (!pending) {
      pending = this.surface
        .describe(sessionId)
        .then((result): Outcome => ({ ok: true, result }))
        .catch((error: unknown): Outcome => ({
          ok: false,
          code: codeOf(error),
        }))
        .finally(() => this.inFlight.delete(sessionId));
      this.inFlight.set(sessionId, pending);
    }
    return pending;
  }

  private scheduleChange(milliseconds: number): void {
    if (this.disposed || !this.scheduler || !this.session) return;
    this.cancelChange();
    this.pendingChange = this.scheduler.delay(() => {
      this.pendingChange = undefined;
      void this.push();
    }, milliseconds);
  }

  private cancelChange(): void {
    this.pendingChange?.();
    this.pendingChange = undefined;
  }

  private async push(): Promise<void> {
    const session = this.session;
    if (!session || session.generation !== this.generation) return;
    const outcome = await this.describeOnce(session.sessionId);
    if (this.disposed || this.session !== session) return;
    const next = fingerprint(outcome);
    if (next !== session.last) {
      session.last = next;
      // Native URLs carry the OBS token: only this IPC channel receives them.
      if (outcome.ok) this.emit('mirror.native.changed', outcome.result);
      else
        this.emit('mirror.native.unavailable', {
          sessionId: session.sessionId,
          ...failure(outcome.code ?? 'native-unavailable'),
        });
    }
    if (!outcome.ok && outcome.code === 'obs-not-ready')
      this.scheduleChange(notReadyRetryMs);
  }

  private emit(name: string, payload: unknown): void {
    if (!this.disposed && this.messages.state === 'ready')
      this.messages.emit(name, payload);
  }

  private reply(id: string, name: string, payload: unknown): void {
    if (!this.disposed && this.messages.state === 'ready')
      this.messages.reply(id, name, payload);
  }
}

function codeOf(error: unknown): FailureCode {
  const candidate =
    typeof error === 'object' && error !== null && 'code' in error
      ? error.code
      : undefined;
  // Native errors may carry the OBS URL and token. Never forward raw errors.
  return typeof candidate === 'string' &&
    Object.prototype.hasOwnProperty.call(failures, candidate)
    ? (candidate as FailureCode)
    : 'native-unavailable';
}

function failure(code: FailureCode) {
  return {
    code,
    message: failures[code],
    retryable: code !== 'invalid-session',
  };
}

function fingerprint(outcome: Outcome): string {
  return outcome.ok
    ? `ready:${JSON.stringify(outcome.result)}`
    : `failed:${outcome.code}`;
}
