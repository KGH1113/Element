import { create } from 'zustand';
import type { KeyLimiterSyncRequest } from '@src/renderer/ipc/keyLimiterSnapshot';
import type {
  KeyLimiterConnectionState,
  KeyLimiterSyncResponse,
} from '@src/renderer/ipc/keyLimiterIpcCoordinator';

export type KeyLimiterDeliveryState =
  'idle' | 'sending' | 'applied' | 'released' | 'error';

export interface KeyLimiterAcknowledgement {
  sessionId: string;
  revision: number;
  profile: { id: string; name: string };
  enabled: boolean;
  supportedKeys: string[];
  unsupportedKeys: string[];
  acknowledgedAt: number;
}

interface KeyLimiterStatusState {
  connectionState: KeyLimiterConnectionState;
  deliveryState: KeyLimiterDeliveryState;
  latestRevision: number;
  lastAcknowledgement: KeyLimiterAcknowledgement | null;
  error: string | null;
  setConnectionState: (
    connectionState: KeyLimiterConnectionState,
    error?: unknown,
  ) => void;
  markSyncStarted: (request: KeyLimiterSyncRequest) => void;
  markSyncResult: (
    request: KeyLimiterSyncRequest,
    response: KeyLimiterSyncResponse,
  ) => void;
  markSyncError: (request: KeyLimiterSyncRequest, error: unknown) => void;
  reset: () => void;
}

const COMPLETION_DISPLAY_MS = 1_500;
let completionTimer: ReturnType<typeof setTimeout> | null = null;

function errorMessage(error: unknown): string | null {
  if (error instanceof Error) return error.message;
  if (typeof error === 'string') return error;
  if (typeof (error as { message?: unknown })?.message === 'string')
    return (error as { message: string }).message;
  return error == null ? null : String(error);
}

function clearCompletionTimer(): void {
  if (!completionTimer) return;
  clearTimeout(completionTimer);
  completionTimer = null;
}

const initialState = {
  connectionState: 'disconnected' as KeyLimiterConnectionState,
  deliveryState: 'idle' as KeyLimiterDeliveryState,
  latestRevision: 0,
  lastAcknowledgement: null as KeyLimiterAcknowledgement | null,
  error: null as string | null,
};

export const useKeyLimiterStatusStore = create<KeyLimiterStatusState>(
  (set, get) => ({
    ...initialState,
    setConnectionState: (connectionState, error) => {
      set({
        connectionState,
        error:
          connectionState === 'version-mismatch' ||
          connectionState === 'retrying'
            ? errorMessage(error)
            : get().deliveryState === 'error'
              ? get().error
              : null,
      });
    },
    markSyncStarted: (request) => {
      if (request.revision < get().latestRevision) return;
      clearCompletionTimer();
      set({
        deliveryState: 'sending',
        latestRevision: request.revision,
        error: null,
      });
    },
    markSyncResult: (request, response) => {
      if (request.revision < get().latestRevision) return;

      const matchingResponse =
        response?.sessionId === request.sessionId &&
        response?.revision === request.revision;
      const acknowledged =
        response?.ok === true &&
        matchingResponse &&
        (response.applied === true || response.reason === 'duplicate');

      if (!acknowledged) {
        clearCompletionTimer();
        set({
          deliveryState: 'error',
          latestRevision: request.revision,
          error:
            response?.message ||
            response?.reason ||
            'The game could not confirm this key profile. Reconnect ADOFAI and select the profile again.',
        });
        return;
      }

      const deliveryState: KeyLimiterDeliveryState = response.enabled
        ? 'applied'
        : 'released';
      const revision = request.revision;
      clearCompletionTimer();
      set({
        deliveryState,
        latestRevision: revision,
        error: null,
        lastAcknowledgement: {
          sessionId: request.sessionId,
          revision,
          profile: request.profile,
          enabled: response.enabled,
          supportedKeys: response.supportedKeys ?? [],
          unsupportedKeys: response.unsupportedKeys ?? [],
          acknowledgedAt: Date.now(),
        },
      });

      completionTimer = setTimeout(() => {
        completionTimer = null;
        const current = get();
        if (
          current.latestRevision === revision &&
          (current.deliveryState === 'applied' ||
            current.deliveryState === 'released')
        ) {
          set({ deliveryState: 'idle' });
        }
      }, COMPLETION_DISPLAY_MS);
    },
    markSyncError: (request, error) => {
      if (request.revision < get().latestRevision) return;
      clearCompletionTimer();
      set({
        deliveryState: 'error',
        latestRevision: request.revision,
        error: errorMessage(error),
      });
    },
    reset: () => {
      clearCompletionTimer();
      set(initialState);
    },
  }),
);
