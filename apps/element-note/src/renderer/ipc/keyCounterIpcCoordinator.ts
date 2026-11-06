import type {
  MessagePort,
  Unsubscribe,
} from '../application/ports/messagePort';

/** Tab id → key slot label → presses. */
export type KeyCounts = Record<string, Record<string, number>>;

export interface KeyCounterPort {
  /** The counts the windows show now. */
  read(): Promise<KeyCounts>;
  /** Presses count from `counts` (or the current counts) and stay out of the saved totals. */
  beginReplay(counts: KeyCounts | null): Promise<void>;
  /** The player's own counts show and count again. */
  endReplay(): Promise<unknown>;
}

export const keyCounterProtocolVersion = 1;

/**
 * The game's side of TUFReplay's saved runs. Element reads the counts as a run
 * starts recording; while TUFReplay replays a run in the game, presses count
 * from the counts that run was recorded at and never reach the saved totals.
 */
export class KeyCounterIpcCoordinator {
  private readonly subscriptions: Unsubscribe[] = [];
  private replaying = false;
  private disposed = false;

  constructor(
    private readonly messages: MessagePort,
    private readonly counters: KeyCounterPort,
  ) {
    this.subscriptions.push(
      messages.onCommand('counters.read', (_payload, message) => {
        void this.read(message.id);
      }),
      messages.onCommand('counters.replay.begin', (payload) => {
        void this.beginReplay(payload);
      }),
      messages.onCommand('counters.replay.end', () => {
        void this.endReplay();
      }),
      messages.onState((state) => {
        if (state === 'ready') this.announce();
        // Without the game no replay is running.
        else void this.endReplay();
      }),
    );
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.subscriptions.forEach((unsubscribe) => unsubscribe());
    await this.endReplay();
  }

  private announce(): void {
    this.messages.emit('counters.available', {
      protocolVersion: keyCounterProtocolVersion,
    });
  }

  private async read(id: string): Promise<void> {
    try {
      const counters = await this.counters.read();
      this.messages.reply(id, 'counters.read.result', { ok: true, counters });
    } catch (error) {
      console.error('Failed to read key counts for the game', error);
      this.messages.reply(id, 'counters.read.result', {
        ok: false,
        code: 'counters-unavailable',
        message: 'ElementNote could not read its key counts.',
      });
    }
  }

  private async beginReplay(payload: unknown): Promise<void> {
    if (this.disposed) return;
    this.replaying = true;
    const recorded = readCounts(
      (payload as { counters?: unknown } | undefined)?.counters,
    );
    try {
      await this.counters.beginReplay(recorded);
    } catch (error) {
      console.error('Failed to count a TUFReplay replay apart', error);
    }
  }

  private async endReplay(): Promise<void> {
    if (!this.replaying) return;
    this.replaying = false;
    try {
      await this.counters.endReplay();
    } catch (error) {
      console.error('Failed to count the player keys again', error);
    }
  }
}

const maxCount = 0xffffffff;

/** A run's recorded counts, or null when they are missing or not whole presses. */
export function readCounts(value: unknown): KeyCounts | null {
  if (!isRecord(value)) return null;
  const counts: KeyCounts = {};
  for (const [tab, keys] of Object.entries(value)) {
    if (!isRecord(keys)) return null;
    const tabCounts: Record<string, number> = {};
    for (const [key, presses] of Object.entries(keys)) {
      if (
        typeof presses !== 'number' ||
        !Number.isInteger(presses) ||
        presses < 0 ||
        presses > maxCount
      )
        return null;
      tabCounts[key] = presses;
    }
    counts[tab] = tabCounts;
  }
  return counts;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
