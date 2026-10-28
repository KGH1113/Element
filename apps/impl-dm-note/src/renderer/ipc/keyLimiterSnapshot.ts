import type {
  KeyMappings,
  KeyPositions,
  KeyViewerTab,
  SelectedViewerTabs,
} from '@src/types/key/keys';
import { ghostKeyLabels } from '@src/renderer/editor/model/ghostKeys';

export const KEY_LIMITER_SCHEMA_VERSION = 2 as const;
export const KEY_LIMITER_SOURCE = 'impl-dm-note' as const;

export interface KeyLimiterProfile {
  id: string;
  name: string;
}

export interface KeyLimiterSyncRequest {
  schemaVersion: typeof KEY_LIMITER_SCHEMA_VERSION;
  source: typeof KEY_LIMITER_SOURCE;
  clientVersion: string;
  sessionId: string;
  revision: number;
  enabled: boolean;
  profile: KeyLimiterProfile;
  keys: string[];
  excludedGhostKeys: string[];
}

export interface KeyLimiterSnapshotSource {
  selectedViewerTabs: SelectedViewerTabs;
  tabs: KeyViewerTab[];
  keyMappings: KeyMappings;
  positions: KeyPositions;
  /** The Enable Key Limiter toggle in ImplDmNote. */
  keyLimiterEnabled?: boolean;
}

function pushUnique(target: string[], seen: Set<string>, key: string): void {
  if (seen.has(key)) return;
  seen.add(key);
  target.push(key);
}

export function buildKeyLimiterSyncRequest(
  source: KeyLimiterSnapshotSource,
  sessionId: string,
  revision: number,
  clientVersion: string,
): KeyLimiterSyncRequest {
  const handMode = source.selectedViewerTabs.hand;
  const footMode = source.selectedViewerTabs.foot;
  const modes = [handMode, footMode];
  const keys: string[] = [];
  const included = new Set<string>();
  const ghostCandidates: string[] = [];
  const seenGhostCandidates = new Set<string>();

  modes.forEach((mode) => {
    const mappings = source.keyMappings[mode] ?? [];
    const positions = source.positions[mode] ?? [];

    mappings.forEach((key, index) => {
      if (positions[index]?.keyLimiterExcluded === true) {
        pushUnique(ghostCandidates, seenGhostCandidates, key);
        return;
      }

      pushUnique(keys, included, key);
    });
    // Ghost keys never limit input; report them so the game can explain why.
    ghostKeyLabels(mappings, positions).forEach((key) =>
      pushUnique(ghostCandidates, seenGhostCandidates, key),
    );
  });

  const excludedGhostKeys = ghostCandidates.filter((key) => !included.has(key));
  const handTab = source.tabs.find((tab) => tab.id === handMode);
  const footTab = source.tabs.find((tab) => tab.id === footMode);

  return {
    schemaVersion: KEY_LIMITER_SCHEMA_VERSION,
    source: KEY_LIMITER_SOURCE,
    clientVersion,
    sessionId,
    revision,
    enabled: source.keyLimiterEnabled !== false && keys.length > 0,
    profile: {
      id: `dual:${encodeURIComponent(handMode)}:${encodeURIComponent(
        footMode,
      )}`,
      name: `${handTab?.name ?? handMode} + ${footTab?.name ?? footMode}`,
    },
    keys,
    excludedGhostKeys,
  };
}

export function getKeyLimiterSnapshotFingerprint(
  request: KeyLimiterSyncRequest,
): string {
  return JSON.stringify({
    enabled: request.enabled,
    profile: request.profile,
    keys: request.keys,
    excludedGhostKeys: request.excludedGhostKeys,
  });
}
