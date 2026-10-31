import type {
  MessageMetadata,
  MessagePort,
  SchedulerPort,
  Unsubscribe,
} from '../application/ports/messagePort';
import type { RenderSurfacePort } from '../application/ports/renderSurfacePort';
export interface RenderCoordinatorOptions {
  messages: MessagePort;
  surface: RenderSurfacePort;
  scheduler: SchedulerPort;
  capabilities: {
    applicationId: string;
    applicationVersion: string;
    platform: string;
    nativeCapture: boolean;
    multiViewerCapture: boolean;
  };
}
const commands = {
  'dmnote.begin': 'dmnote.begun',
  'dmnote.reset': 'dmnote.reset',
  'dmnote.frame': 'dmnote.frame.ready',
  'dmnote.end': 'dmnote.ended',
} as const;
/** Drives a frozen native surface using messages, independently of the UI and transport. */
export class RenderIpcCoordinator {
  private sessionId: string | null = null;
  private attached = false;
  private waitingForOwner = false;
  private attachId?: string;
  private disposed = false;
  private framePending = false;
  private readonly subscriptions: Unsubscribe[] = [];
  private readonly executions = new Map<
    string,
    { name?: string; payload?: unknown }
  >();
  constructor(private readonly options: RenderCoordinatorOptions) {
    this.subscriptions.push(
      options.messages.on('dmnote.attached', (payload, message) => {
        if (
          message.correlationId === this.attachId &&
          (payload as { protocolVersion?: number })?.protocolVersion === 2
        ) {
          this.attached = true;
          this.waitingForOwner = false;
        }
      }),
    );
    for (const name of ['command.rejected', 'command.failed']) {
      this.subscriptions.push(
        options.messages.on(name, (payload, message) => {
          if (message.correlationId === this.attachId)
            this.waitingForOwner =
              (payload as { code?: string })?.code === 'dmnote_busy';
        }),
      );
    }
    this.subscriptions.push(
      options.messages.on('dmnote.availability.changed', (payload) => {
        const availability = payload as { available?: boolean; busy?: boolean };
        if (
          this.waitingForOwner &&
          availability?.busy === false &&
          availability.available === false &&
          options.messages.state === 'ready'
        )
          this.attach();
      }),
    );
    for (const name of Object.keys(commands) as Array<keyof typeof commands>) {
      this.subscriptions.push(
        options.messages.onCommand(name, (payload, message) => {
          void this.execute(name, payload as Record<string, unknown>, message);
        }),
      );
    }
    this.subscriptions.push(
      options.messages.onState((state) => {
        this.attached = false;
        if (state === 'ready') this.attach();
        else void this.end();
      }),
    );
    // This scheduler uses the main window's physical wall clock, never the render clock.
    this.subscriptions.push(
      options.scheduler.repeat(() => {
        if (!this.attached || options.messages.state !== 'ready') return;
        options.messages.emit('dmnote.pulse', {
          applicationId: options.capabilities.applicationId,
          sessionId: this.sessionId,
        });
        if (this.sessionId)
          void options.surface.touch().catch(() => this.end());
      }, 1000),
    );
  }
  private attach(): void {
    this.waitingForOwner = false;
    this.attachId = this.options.messages.send('dmnote.attach', {
      ...this.options.capabilities,
      protocolVersion: 2,
    });
  }
  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
    await this.end();
    this.executions.clear();
  }
  private async end(): Promise<void> {
    const current = this.sessionId;
    this.sessionId = null;
    this.framePending = false;
    if (current) await this.options.surface.end(current).catch(() => {});
  }
  private async execute(
    name: keyof typeof commands,
    payload: Record<string, unknown>,
    message: MessageMetadata,
  ): Promise<void> {
    if (this.disposed) return;
    const repeated = this.executions.get(message.id);
    if (repeated) {
      if (repeated.name)
        this.options.messages.reply(
          message.id,
          repeated.name,
          repeated.payload,
        );
      return;
    }
    const execution: { name?: string; payload?: unknown } = {};
    this.executions.set(message.id, execution);
    const session =
      typeof payload?.sessionId === 'string' ? payload.sessionId : '';
    try {
      if (!this.attached) throw new Error('render-not-attached');
      if (!/^[a-f0-9]{32}$/i.test(session))
        throw new Error('render-invalid-session');
      let result: unknown;
      if (name === 'dmnote.begin') {
        if (this.sessionId) throw new Error('render-busy');
        this.sessionId = session;
        result = await this.options.surface.begin(payload);
      } else if (name === 'dmnote.end') {
        if (this.sessionId && this.sessionId !== session)
          throw new Error('render-wrong-session');
        await this.end();
        result = { ended: true };
      } else {
        if (this.sessionId !== session) throw new Error('render-wrong-session');
        if (this.framePending) throw new Error('render-frame-busy');
        this.framePending = true;
        try {
          result =
            name === 'dmnote.reset'
              ? await this.options.surface.reset(payload)
              : await this.options.surface.frame(payload);
        } finally {
          if (this.sessionId === session) this.framePending = false;
        }
      }
      if (name !== 'dmnote.end' && this.sessionId !== session)
        throw new Error('render-session-ended');
      execution.name = commands[name];
      execution.payload = result;
    } catch (error) {
      const description = String(error);
      if (this.sessionId === session) await this.end();
      execution.name = 'dmnote.failed';
      execution.payload = {
        code: /busy/.test(description)
          ? 'dmnote_busy'
          : /timeout/.test(description)
            ? 'dmnote_frame_timeout'
            : /session-ended|surface-closed/.test(description)
              ? 'dmnote_disconnected'
              : 'dmnote_capture_failed',
        message: `ElementNote could not ${name === 'dmnote.begin' ? 'prepare the overlay' : name === 'dmnote.frame' ? 'capture this frame' : name === 'dmnote.reset' ? 'reset the overlay' : 'end the render session'}: ${description}`,
      };
    }
    if (!this.disposed && this.options.messages.state === 'ready')
      this.options.messages.reply(
        message.id,
        execution.name,
        execution.payload,
      );
    // A bounded reply cache tolerates duplicate delivery without retaining every frame.
    if (this.executions.size > 64)
      for (const [id, value] of this.executions) {
        if (value.name) {
          this.executions.delete(id);
          break;
        }
      }
  }
}
