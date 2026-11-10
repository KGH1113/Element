import { invoke } from '../invoke';
import { subscribe } from './shared';

export interface HistoryStatus {
  historyRevision: number;
  historyEpoch: number;
  statusSeq: number;
  canUndo: boolean;
  canRedo: boolean;
  busy: boolean;
  truncated?: { reason: string } | null;
}

const restoredListeners = new Set<() => void>();

async function restore(direction: 'undo' | 'redo'): Promise<HistoryStatus> {
  const status = await invoke<HistoryStatus>(`history_${direction}`, {
    operationId: crypto.randomUUID(),
  });
  restoredListeners.forEach((listener) => listener());
  return status;
}

export const historyApi = {
  status: () => invoke<HistoryStatus>('history_status'),
  undo: () => restore('undo'),
  redo: () => restore('redo'),
  onStatus: (listener: (status: HistoryStatus) => void) =>
    subscribe<HistoryStatus>('history:status', listener),
  onRestored(listener: () => void) {
    restoredListeners.add(listener);
    return () => {
      restoredListeners.delete(listener);
    };
  },
};
