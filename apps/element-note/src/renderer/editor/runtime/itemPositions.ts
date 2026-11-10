/**
 * 통계(KPS)·그래프·노브 배치를 화면·백엔드·오버레이에 반영한다.
 * 기존 편집기는 요소 ID를 만들지 않으므로, 백엔드가 저장하며 정한 ID를 화면이 받아 온다.
 */

import type { ItemPositionsField } from '@api/modules/itemsApi';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useGraphItemStore } from '@stores/data/useGraphItemStore';
import { useKnobItemStore } from '@stores/data/useKnobItemStore';
import type { StatItemPositions } from '@src/types/key/statItems';
import type { GraphItemPositions } from '@src/types/key/graphItems';
import type { KnobItemPositions } from '@src/types/key/knobs';

export type { ItemPositionsField };

export interface ItemPositionsByField {
  statPositions: StatItemPositions;
  graphPositions: GraphItemPositions;
  knobPositions: KnobItemPositions;
}

interface ItemPositionsStore<T> {
  positions: T;
  setPositions: (positions: T) => void;
  setLocalUpdateInProgress: (value: boolean) => void;
}

interface ItemPositionsChannel<T> {
  store: () => ItemPositionsStore<T>;
  load: () => Promise<T>;
  save: (positions: T) => Promise<T>;
  overlayEvent: string;
}

const channels: {
  [F in ItemPositionsField]: ItemPositionsChannel<ItemPositionsByField[F]>;
} = {
  statPositions: {
    store: () => useStatItemStore.getState(),
    load: () => window.api.statItems.getPositions(),
    save: (positions) => window.api.statItems.updatePositions(positions),
    overlayEvent: 'statPositions:sync',
  },
  graphPositions: {
    store: () => useGraphItemStore.getState(),
    load: () => window.api.graphItems.getPositions(),
    save: (positions) => window.api.graphItems.updatePositions(positions),
    overlayEvent: 'graphPositions:sync',
  },
  knobPositions: {
    store: () => useKnobItemStore.getState(),
    load: () => window.api.knobItems.getPositions(),
    save: (positions) => window.api.knobItems.updatePositions(positions),
    overlayEvent: 'knobPositions:sync',
  },
};

export function itemPositionsChannel<F extends ItemPositionsField>(
  field: F,
): ItemPositionsChannel<ItemPositionsByField[F]> {
  return channels[field];
}

/**
 * 저장을 요청한 뒤 화면이 바뀌지 않았다면 백엔드가 저장한 배치로 바꿔 새 ID를 받는다.
 * 그사이 다른 편집이 있었다면 그 편집의 저장이 다시 ID를 정하므로 건드리지 않는다.
 */
export function adoptSavedItemPositions<F extends ItemPositionsField>(
  field: F,
  sent: ItemPositionsByField[F],
  saved: ItemPositionsByField[F],
): void {
  const store = channels[field].store();
  if (store.positions === sent) store.setPositions(saved);
}

/** 저장하고 새 ID를 받아 온다. 실패는 호출한 쪽으로 넘긴다. */
export async function saveItemPositions<F extends ItemPositionsField>(
  field: F,
  positions: ItemPositionsByField[F],
): Promise<void> {
  const saved = await itemPositionsChannel(field).save(positions);
  adoptSavedItemPositions(field, positions, saved);
}

export function syncItemPositionsToOverlay(field: ItemPositionsField): void {
  const channel = itemPositionsChannel(field);
  try {
    window.api.bridge.sendTo('overlay', channel.overlayEvent, {
      positions: channel.store().positions,
    });
  } catch {
    /* 오버레이가 없으면 무시 */
  }
}

/**
 * 배치를 화면에 먼저 반영하고 저장한다.
 * 저장이 실패하면 itemSaveRecovery가 화면을 저장된 배치로 되돌린다.
 */
export async function persistItemPositions<F extends ItemPositionsField>(
  field: F,
  next: ItemPositionsByField[F],
  errorMessage?: string,
): Promise<void> {
  const channel = itemPositionsChannel(field);
  const store = channel.store();
  store.setLocalUpdateInProgress(true);
  store.setPositions(next);
  try {
    await saveItemPositions(field, next);
  } catch (error) {
    console.error(errorMessage ?? `Failed to update ${field}`, error);
  } finally {
    store.setLocalUpdateInProgress(false);
  }
  syncItemPositionsToOverlay(field);
}
