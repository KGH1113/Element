import { useEffect } from 'react';
import { useKeyStore } from '@stores/data/useKeyStore';
import type { KeyLimiterIpcCoordinator } from '@src/renderer/ipc/keyLimiterIpcCoordinator';
import {
  buildKeyLimiterSyncRequest,
  getKeyLimiterSnapshotFingerprint,
} from '@src/renderer/ipc/keyLimiterSnapshot';
import { createKeyLimiterCoordinator } from '../../bootstrap/ipcRuntime';
import { useKeyLimiterStatusStore } from '@stores/useKeyLimiterStatusStore';

function createSessionId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }

  return `impl-dm-note-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export function startKeyLimiterSync(
  coordinator: KeyLimiterIpcCoordinator = createKeyLimiterCoordinator(),
): () => void {
  const sessionId = createSessionId();
  let revision = 0;
  let lastFingerprint: string | null = null;

  const enqueueCurrentState = () => {
    const state = useKeyStore.getState();
    if (!state.isBootstrapped) return;

    const nextRevision = revision + 1;
    const request = buildKeyLimiterSyncRequest(
      state,
      sessionId,
      nextRevision,
      __APP_VERSION__,
    );
    const fingerprint = getKeyLimiterSnapshotFingerprint(request);
    if (fingerprint === lastFingerprint) return;

    revision = nextRevision;
    lastFingerprint = fingerprint;
    coordinator.enqueue(request);
  };

  const unsubscribe = useKeyStore.subscribe((state, previous) => {
    if (
      state.isBootstrapped !== previous.isBootstrapped ||
      state.selectedViewerTabs !== previous.selectedViewerTabs ||
      state.tabs !== previous.tabs ||
      state.keyMappings !== previous.keyMappings ||
      state.positions !== previous.positions
    ) {
      enqueueCurrentState();
    }
  });

  enqueueCurrentState();

  return () => {
    unsubscribe();
    coordinator.dispose();
  };
}

export function useKeyLimiterSync(): void {
  useEffect(() => {
    if (window.__dmn_window_type !== 'main') return;
    const status = useKeyLimiterStatusStore.getState();
    const coordinator = createKeyLimiterCoordinator({
      onStateChange: (state, error) =>
        useKeyLimiterStatusStore.getState().setConnectionState(state, error),
      onSyncStart: (request) =>
        useKeyLimiterStatusStore.getState().markSyncStarted(request),
      onSyncResult: (request, response) =>
        useKeyLimiterStatusStore.getState().markSyncResult(request, response),
      onSyncError: (request, error) =>
        useKeyLimiterStatusStore.getState().markSyncError(request, error),
    });

    status.reset();
    const stop = startKeyLimiterSync(coordinator);
    return () => {
      stop();
      useKeyLimiterStatusStore.getState().reset();
    };
  }, []);
}
