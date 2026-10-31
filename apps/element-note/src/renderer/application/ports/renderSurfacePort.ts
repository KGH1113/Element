export interface RenderSurfacePort {
  begin(request: Record<string, unknown>): Promise<unknown>;
  reset(request: Record<string, unknown>): Promise<unknown>;
  frame(request: Record<string, unknown>): Promise<unknown>;
  end(sessionId: string | null): Promise<unknown>;
  touch(): Promise<void>;
}
