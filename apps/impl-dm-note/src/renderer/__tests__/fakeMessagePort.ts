import type {
  MessageMetadata,
  MessagePort,
  MessageState,
  SchedulerPort,
} from '../application/ports/messagePort';
export const testScheduler: SchedulerPort = {
  delay(callback, milliseconds) {
    const id = setTimeout(callback, milliseconds);
    return () => clearTimeout(id);
  },
  repeat(callback, milliseconds) {
    const id = setInterval(callback, milliseconds);
    return () => clearInterval(id);
  },
};
export class FakeMessagePort implements MessagePort {
  state: MessageState = 'ready';
  sent: Array<{
    id: string;
    name: string;
    payload: unknown;
    correlationId?: string;
  }> = [];
  private sequence = 0;
  private states = new Set<(state: MessageState, error?: unknown) => void>();
  private events = new Map<
    string,
    Set<(payload: unknown, message: MessageMetadata) => void>
  >();
  private commands = new Map<
    string,
    Set<(payload: unknown, message: MessageMetadata) => void>
  >();
  onState(listener: (state: MessageState, error?: unknown) => void) {
    this.states.add(listener);
    listener(this.state);
    return () => this.states.delete(listener);
  }
  on(
    name: string,
    listener: (payload: unknown, message: MessageMetadata) => void,
  ) {
    const set = this.events.get(name) ?? new Set();
    set.add(listener);
    this.events.set(name, set);
    return () => set.delete(listener);
  }
  onCommand(
    name: string,
    listener: (payload: unknown, message: MessageMetadata) => void,
  ) {
    const set = this.commands.get(name) ?? new Set();
    set.add(listener);
    this.commands.set(name, set);
    return () => set.delete(listener);
  }
  send(name: string, payload: unknown) {
    const id = `message-${++this.sequence}`;
    this.sent.push({ id, name, payload });
    return id;
  }
  emit(name: string, payload: unknown) {
    return this.send(name, payload);
  }
  reply(correlationId: string, name: string, payload: unknown) {
    const id = this.send(name, payload);
    this.sent[this.sent.length - 1].correlationId = correlationId;
    return id;
  }
  changeState(state: MessageState, error?: unknown) {
    this.state = state;
    this.states.forEach((listener) => listener(state, error));
  }
  receive(name: string, payload: unknown, correlationId?: string) {
    this.events
      .get(name)
      ?.forEach((listener) =>
        listener(payload, { id: 'event', correlationId }),
      );
  }
  command(name: string, payload: unknown, id = `command-${++this.sequence}`) {
    this.commands.get(name)?.forEach((listener) => listener(payload, { id }));
    return id;
  }
}
