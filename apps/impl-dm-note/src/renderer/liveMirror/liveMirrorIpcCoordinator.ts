import type {
  MessagePort,
  Unsubscribe,
} from '../application/ports/messagePort';

export interface LiveMirrorSurfacePort {
  frame(sessionId: string): Promise<unknown>;
  end(sessionId: string | null): Promise<void>;
}

/** One requested frame is captured at a time; the game controls the cadence. */
export class LiveMirrorIpcCoordinator {
  private readonly subscriptions: Unsubscribe[] = [];
  private sessionId: string | null = null;
  private framePending = false;
  private disposed = false;
  private wasReady = false;
  private cleanup: Promise<void> = Promise.resolve();

  constructor(
    private readonly messages: MessagePort,
    private readonly surface: LiveMirrorSurfacePort,
  ) {
    this.subscriptions.push(
      messages.onCommand('mirror.frame', (payload, message) => {
        void this.frame(payload as Record<string, unknown>, message.id);
      }),
      messages.onCommand('mirror.end', (payload, message) => {
        void this.end(payload as Record<string, unknown>, message.id);
      }),
      messages.onState((state) => {
        if (state === 'ready') {
          this.wasReady = true;
          messages.emit('mirror.available', { protocolVersion: 1 });
        } else if (this.wasReady) {
          this.wasReady = false;
          this.sessionId = null;
          this.queueCleanup(null);
        }
      }),
    );
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
    this.sessionId = null;
    await this.queueCleanup(null);
  }

  private async frame(
    payload: Record<string, unknown>,
    id: string,
  ): Promise<void> {
    const sessionId = payload?.sessionId;
    if (typeof sessionId !== 'string' || !/^[a-f0-9]{32}$/i.test(sessionId)) {
      this.reply(id, 'mirror.failed', {
        message: 'Invalid in-game viewer session.',
      });
      return;
    }
    if (this.framePending) {
      this.reply(id, 'mirror.failed', {
        message: 'The previous viewer frame is still being captured.',
      });
      return;
    }
    this.framePending = true;
    try {
      this.sessionId = sessionId;
      await this.cleanup;
      if (
        this.disposed ||
        this.sessionId !== sessionId ||
        this.messages.state !== 'ready'
      )
        return;
      const result = await this.surface.frame(sessionId);
      if (!this.disposed && this.sessionId === sessionId)
        this.reply(id, 'mirror.frame.ready', result);
    } catch (error) {
      if (this.sessionId === sessionId)
        this.reply(id, 'mirror.failed', { message: String(error) });
    } finally {
      this.framePending = false;
    }
  }

  private async end(
    payload: Record<string, unknown>,
    id: string,
  ): Promise<void> {
    const sessionId = payload?.sessionId;
    if (typeof sessionId !== 'string' || sessionId !== this.sessionId) {
      this.reply(id, 'mirror.ended', { ended: true });
      return;
    }
    this.sessionId = null;
    await this.queueCleanup(sessionId);
    this.reply(id, 'mirror.ended', { ended: true });
  }

  private queueCleanup(sessionId: string | null): Promise<void> {
    this.cleanup = this.cleanup
      .then(() => this.surface.end(sessionId))
      .catch(() => {});
    return this.cleanup;
  }

  private reply(id: string, name: string, payload: unknown): void {
    if (!this.disposed && this.messages.state === 'ready')
      this.messages.reply(id, name, payload);
  }
}
