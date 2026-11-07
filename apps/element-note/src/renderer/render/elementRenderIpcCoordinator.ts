import type {
  MessageMetadata,
  MessagePort,
  SchedulerPort,
  Unsubscribe,
} from '../application/ports/messagePort';
import type { RenderSurfacePort } from '../application/ports/renderSurfacePort';

/** Bumped when Element's render messages change incompatibly. */
export const elementRenderProtocolVersion = 1;

/** Every answer goes back to Element as this command. */
export const elementRenderReply = 'render.reply';

// Element pulses while it records. A game that stops talking (a crash, or a
// render cancelled without End) must not keep live input gated for long.
const leaseMs = 30_000;

const requests = [
  'render.begin',
  'render.frame',
  'render.end',
  'render.pulse',
] as const;
type Request = (typeof requests)[number];

/**
 * Element's replay renders on the `element` channel. Element waits for each
 * frame on the game thread, where events from the app would only arrive after
 * it stops waiting, so answers go back as a command Element runs off that thread.
 */
export class ElementRenderIpcCoordinator {
  private sessionId: string | null = null;
  private framePending = false;
  private lastContact = 0;
  private disposed = false;
  private readonly subscriptions: Unsubscribe[] = [];

  constructor(
    private readonly messages: MessagePort,
    private readonly surface: RenderSurfacePort,
    scheduler: SchedulerPort,
    private readonly now: () => number = Date.now,
  ) {
    for (const name of requests)
      this.subscriptions.push(
        messages.onCommand(name, (payload, message) => {
          void this.handle(
            name,
            (payload ?? {}) as Record<string, unknown>,
            message,
          );
        }),
      );
    this.subscriptions.push(
      messages.onState((state) => {
        if (state === 'ready') this.announce();
        else void this.end();
      }),
      // The main window's wall clock, never the render clock.
      scheduler.repeat(() => this.keepAlive(), 1000),
    );
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
    await this.end();
  }

  private announce(): void {
    this.messages.emit('render.available', {
      protocolVersion: elementRenderProtocolVersion,
      // render.begin may start the counts from a run's recorded ones.
      recordedCounters: true,
    });
  }

  private keepAlive(): void {
    if (!this.sessionId) return;
    if (this.now() - this.lastContact > leaseMs) {
      void this.end();
      return;
    }
    void this.surface.touch().catch(() => this.end());
  }

  private async end(): Promise<void> {
    const current = this.sessionId;
    this.sessionId = null;
    this.framePending = false;
    if (current) await this.surface.end(current).catch(() => {});
  }

  private async handle(
    name: Request,
    payload: Record<string, unknown>,
    message: MessageMetadata,
  ): Promise<void> {
    if (this.disposed) return;
    const session =
      typeof payload.sessionId === 'string' &&
      /^[a-f0-9]{32}$/i.test(payload.sessionId)
        ? payload.sessionId
        : null;
    if (name === 'render.pulse' || name === 'render.end') {
      // Neither expects an answer; only the current session's count.
      if (!session || session !== this.sessionId) return;
      if (name === 'render.pulse') this.lastContact = this.now();
      else await this.end();
      return;
    }
    try {
      if (!session) throw new Error('render-invalid-session');
      let result: unknown;
      if (name === 'render.begin') {
        if (this.sessionId) throw new Error('render-busy');
        this.sessionId = session;
        this.lastContact = this.now();
        result = await this.surface.begin(payload);
      } else {
        if (this.sessionId !== session) throw new Error('render-session-ended');
        if (this.framePending) throw new Error('render-frame-busy');
        this.lastContact = this.now();
        this.framePending = true;
        try {
          result = await this.surface.frame(payload);
        } finally {
          if (this.sessionId === session) this.framePending = false;
        }
      }
      if (this.sessionId !== session) throw new Error('render-session-ended');
      this.answer(message.id, session, { ok: true, result });
    } catch (error) {
      if (session && this.sessionId === session) await this.end();
      this.answer(message.id, session, { ok: false, error: describe(error) });
    }
  }

  private answer(
    requestId: string,
    sessionId: string | null,
    outcome: { ok: boolean; result?: unknown; error?: unknown },
  ): void {
    if (this.disposed || this.messages.state !== 'ready') return;
    try {
      this.messages.send(elementRenderReply, {
        requestId,
        sessionId,
        ...outcome,
      });
    } catch {
      // The game left; it stops waiting on its own timeout.
    }
  }
}

/** Backend errors begin with a code such as `render-busy`; the rest may name local paths. */
function describe(error: unknown): { code: string; message: string } {
  const text = error instanceof Error ? error.message : String(error);
  const code = /^render-[a-z-]+/.exec(text)?.[0] ?? 'render-failed';
  return { code, message: text.slice(0, 500) };
}
