import type {
  MessagePort,
  Unsubscribe,
} from '../application/ports/messagePort';

/** What the profile coming into use shows. */
export type ProfileKeyViewersSource =
  { kind: 'file'; path: string } | { kind: 'firstStart' } | { kind: 'keep' };

export interface ProfileKeyViewersPort {
  /** The Element profile the key viewers on screen belong to, if Element has said. */
  owner(): Promise<string | null>;
  /** Saves the key viewers on screen to `path`, as "Save all" would. */
  save(path: string): Promise<void>;
  /**
   * Saves the key viewers on screen to `save`, then shows `owner`'s from
   * `load`. Refused unless the key viewers on screen are `expected`'s.
   */
  swap(
    save: string | null,
    load: ProfileKeyViewersSource,
    owner: string,
    expected: string | null,
  ): Promise<void>;
}

export const profileKeyViewersProtocolVersion = 1;

/**
 * Element's profiles each keep their own key viewers. The game owns the
 * profiles and the files; ElementNote only saves and loads every key viewer at
 * once when asked, and says whose key viewers are on screen when it connects.
 * Requests run one after another, in the order they came.
 */
export class ProfileKeyViewersIpcCoordinator {
  private readonly subscriptions: Unsubscribe[] = [];
  private queue: Promise<void> = Promise.resolve();
  private disposed = false;

  constructor(
    private readonly messages: MessagePort,
    private readonly keyViewers: ProfileKeyViewersPort,
  ) {
    this.subscriptions.push(
      messages.onCommand('key-viewers.save', (payload, message) => {
        this.run(message.id, 'key-viewers.save.result', async () => {
          const path = readPath((payload as { path?: unknown })?.path);
          if (!path) throw new RequestError('key-viewers-path-invalid');
          await this.keyViewers.save(path);
        });
      }),
      messages.onCommand('key-viewers.swap', (payload, message) => {
        this.run(message.id, 'key-viewers.swap.result', async () => {
          const request = readSwap(payload);
          if (!request) throw new RequestError('key-viewers-request-invalid');
          await this.keyViewers.swap(
            request.save,
            request.load,
            request.owner,
            request.expected,
          );
        });
      }),
      messages.onState((state) => {
        if (state === 'ready') void this.announce();
      }),
    );
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
  }

  private async announce(): Promise<void> {
    const owner = await this.owner();
    if (this.disposed) return;
    this.messages.emit('key-viewers.available', {
      protocolVersion: profileKeyViewersProtocolVersion,
      owner,
    });
  }

  private async owner(): Promise<string | null> {
    try {
      return await this.keyViewers.owner();
    } catch (error) {
      console.error('Failed to read whose key viewers are on screen', error);
      return null;
    }
  }

  // Every answer says whose key viewers are on screen, even after a failure:
  // the game plans its next swap on that.
  private run(id: string, result: string, work: () => Promise<void>): void {
    this.queue = this.queue.then(async () => {
      if (this.disposed) return;
      try {
        await work();
        this.messages.reply(id, result, {
          ok: true,
          owner: await this.owner(),
        });
      } catch (error) {
        console.error(`Failed to answer ${result}`, error);
        this.messages.reply(id, result, {
          ok: false,
          code: errorCode(error),
          message: String((error as { message?: unknown })?.message ?? error),
          owner: await this.owner(),
        });
      }
    });
  }
}

function errorCode(error: unknown): string {
  if (error instanceof RequestError) return error.code;
  const text = String((error as { message?: unknown })?.message ?? error);
  return text.includes('key-viewers-owner-changed')
    ? 'key-viewers-owner-changed'
    : 'key-viewers-failed';
}

class RequestError extends Error {
  constructor(readonly code: string) {
    super(code);
  }
}

function readPath(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

/** A swap request, or null when it is not one. */
export function readSwap(payload: unknown): {
  save: string | null;
  load: ProfileKeyViewersSource;
  owner: string;
  expected: string | null;
} | null {
  if (typeof payload !== 'object' || payload === null) return null;
  const { save, load, owner, expected } = payload as Record<string, unknown>;
  const owned = readPath(owner);
  if (!owned) return null;
  if (save !== null && save !== undefined && !readPath(save)) return null;
  if (expected !== null && expected !== undefined && !readPath(expected))
    return null;
  const source = readSource(load);
  if (!source) return null;
  return {
    save: readPath(save),
    load: source,
    owner: owned,
    expected: readPath(expected),
  };
}

function readSource(value: unknown): ProfileKeyViewersSource | null {
  if (typeof value !== 'object' || value === null) return null;
  const { kind, path } = value as Record<string, unknown>;
  if (kind === 'firstStart' || kind === 'keep') return { kind };
  if (kind === 'file') {
    const file = readPath(path);
    return file ? { kind, path: file } : null;
  }
  return null;
}
