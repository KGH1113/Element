import type {
  MessageMetadata,
  MessagePort,
  SchedulerPort,
  Unsubscribe,
} from '../application/ports/messagePort';
import type { RenderSurfacePort } from '../application/ports/renderSurfacePort';

/**
 * Bumped when Element's render messages change incompatibly.
 * 2: the viewers draw into videos; frames get no answer and `render.finish`
 * returns the videos.
 */
export const elementRenderProtocolVersion = 2;

/** Every answer goes back to Element as this command. */
export const elementRenderReply = 'render.reply';

// Element pulses while it records. A game that stops talking (a crash, or a
// render cancelled without End) must not keep live input gated for long.
const leaseMs = 30_000;

const requests = [
  'render.begin',
  'render.frame',
  'render.finish',
  'render.end',
  'render.pulse',
] as const;
type Request = (typeof requests)[number];

/**
 * Element's replay renders on the `element` channel. The game never waits for a
 * frame: frames are queued in the order they arrive and answered by nothing.
 * The viewers draw them into videos, and `render.finish` answers with the
 * videos once every frame is in, or with why they failed. Answers go back as a
 * command, which Element runs off its game thread, where it is waiting.
 */
export class ElementRenderIpcCoordinator {
  private sessionId: string | null = null;
  // Settles with whether the current session began; frames wait for it.
  private began: Promise<boolean> = Promise.resolve(false);
  // The frames handed to the backend, one after another.
  private frames: Promise<void> = Promise.resolve();
  private failure: unknown = null;
  private finishing = false;
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
    // Closing the videos goes on without the game, which waits for them.
    if (!this.finishing && this.now() - this.lastContact > leaseMs) {
      void this.end();
      return;
    }
    void this.surface.touch().catch(() => this.end());
  }

  private async end(): Promise<void> {
    const current = this.sessionId;
    this.sessionId = null;
    this.began = Promise.resolve(false);
    this.failure = null;
    this.finishing = false;
    if (current) await this.surface.end(current).catch(() => {});
  }

  private handle(
    name: Request,
    payload: Record<string, unknown>,
    message: MessageMetadata,
  ): Promise<void> | void {
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
      else return this.end();
      return;
    }
    if (name === 'render.frame') return this.queueFrame(session, payload);
    return this.answerRequest(name, session, payload, message);
  }

  // Synchronous up to the queue, so frames keep the order they arrived in.
  private queueFrame(
    session: string | null,
    payload: Record<string, unknown>,
  ): void {
    if (!session || session !== this.sessionId || this.finishing) return;
    this.lastContact = this.now();
    const began = this.began;
    this.frames = this.frames
      .then(() => began)
      .then((ready) => {
        if (ready && this.sessionId === session && this.failure === null)
          return this.surface.frame(payload).then(() => {});
      })
      .catch((error: unknown) => {
        // Later frames are dropped; finish answers with this.
        if (this.sessionId === session) this.failure ??= error;
      });
  }

  private async answerRequest(
    name: 'render.begin' | 'render.finish',
    session: string | null,
    payload: Record<string, unknown>,
    message: MessageMetadata,
  ): Promise<void> {
    try {
      if (!session) throw new Error('render-invalid-session');
      let result: unknown;
      if (name === 'render.begin') {
        if (this.sessionId) throw new Error('render-busy');
        this.sessionId = session;
        this.lastContact = this.now();
        this.failure = null;
        const beginning = this.surface.begin(payload);
        this.began = beginning.then(
          () => true,
          () => false,
        );
        result = await beginning;
      } else {
        if (this.sessionId !== session) throw new Error('render-session-ended');
        if (this.finishing) throw new Error('render-session-finishing');
        this.finishing = true;
        this.lastContact = this.now();
        try {
          await this.frames;
          if (this.failure !== null) throw this.failure;
          result = await this.surface.finish(payload);
        } finally {
          if (this.sessionId === session) {
            this.finishing = false;
            this.lastContact = this.now();
          }
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
