export type MessageState =
  | 'disconnected'
  | 'connecting'
  | 'ready'
  | 'retrying'
  | 'version-mismatch'
  | 'disposed';
export interface MessageMetadata {
  id: string;
  correlationId?: string;
}
export type Unsubscribe = () => void;
/** Application-owned port. No SDK, browser, React or Tauri types cross this boundary. */
export interface MessagePort {
  readonly state: MessageState;
  onState(
    listener: (state: MessageState, error?: unknown) => void,
  ): Unsubscribe;
  on(
    name: string,
    listener: (payload: unknown, message: MessageMetadata) => void,
  ): Unsubscribe;
  onCommand(
    name: string,
    listener: (payload: unknown, message: MessageMetadata) => void,
  ): Unsubscribe;
  send(name: string, payload: unknown): string;
  emit(name: string, payload: unknown): string;
  reply(correlationId: string, name: string, payload: unknown): string;
}
export interface SchedulerPort {
  delay(callback: () => void, milliseconds: number): Unsubscribe;
  repeat(callback: () => void, milliseconds: number): Unsubscribe;
}
