export interface RenderSurfacePort {
  begin(request: Record<string, unknown>): Promise<unknown>;
  /** Queues a frame; resolves once it is queued, not drawn. */
  frame(request: Record<string, unknown>): Promise<unknown>;
  /** Resolves with the videos once every queued frame is in them. */
  finish(request: Record<string, unknown>): Promise<unknown>;
  end(sessionId: string | null): Promise<unknown>;
  touch(): Promise<void>;
}
