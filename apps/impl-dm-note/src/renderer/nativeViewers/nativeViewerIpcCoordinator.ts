import type {
  MessagePort,
  Unsubscribe,
} from '../application/ports/messagePort';

export interface NativeViewerSurfacePort {
  describe(sessionId: string): Promise<unknown>;
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

/** Describes pages without capturing pixels or maintaining a file session. */
export class NativeViewerIpcCoordinator {
  private readonly subscriptions: Unsubscribe[] = [];
  private requestPending = false;
  private generation = 0;
  private disposed = false;

  constructor(
    private readonly messages: MessagePort,
    private readonly surface: NativeViewerSurfacePort,
  ) {
    this.subscriptions.push(
      messages.onCommand('mirror.native.describe', (payload, message) => {
        void this.describe(payload as Record<string, unknown>, message.id);
      }),
      messages.onState((state) => {
        this.generation++;
        if (state === 'ready')
          messages.emit('mirror.available', { nativeProtocolVersion: 1 });
      }),
    );
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.generation++;
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
    if (this.requestPending) {
      this.reply(id, 'mirror.failed', {
        code: 'mirror-busy',
        message: 'The previous viewer request is still being prepared.',
        retryable: true,
      });
      return;
    }
    this.requestPending = true;
    const generation = this.generation;
    try {
      const result = await this.surface.describe(sessionId);
      if (this.generation === generation)
        this.reply(id, 'mirror.native.ready', result);
    } catch (error) {
      if (this.generation !== generation) return;
      const candidate =
        typeof error === 'object' && error !== null && 'code' in error
          ? error.code
          : undefined;
      const code =
        typeof candidate === 'string' &&
        Object.prototype.hasOwnProperty.call(failures, candidate)
          ? (candidate as keyof typeof failures)
          : 'native-unavailable';
      // Native URLs carry the OBS token. Never forward raw errors or URLs.
      this.reply(id, 'mirror.failed', {
        code,
        message: failures[code],
        retryable: code !== 'invalid-session',
      });
    } finally {
      this.requestPending = false;
    }
  }

  private reply(id: string, name: string, payload: unknown): void {
    if (!this.disposed && this.messages.state === 'ready')
      this.messages.reply(id, name, payload);
  }
}
