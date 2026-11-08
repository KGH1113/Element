/**
 * 통계(KPS)·그래프·노브 저장이 실패했을 때 화면을 저장된 배치로 되돌린다.
 * 저장 안 된 항목이 화면에 남으면 그 뒤의 저장도 모두 실패하고 종료·실행 취소가 계속 막힌다.
 */

import { clearEditorSaveFailure } from '@api/invoke';
import {
  onItemPositionsSaveFailed,
  type ItemPositionsField,
} from '@api/modules/itemsApi';
import {
  itemPositionsChannel,
  syncItemPositionsToOverlay,
} from './itemPositions';

/** restored: 화면이 저장된 배치로 돌아갔는지. false면 저장된 배치를 읽지도 못했다. */
type ItemSaveFailureListener = (failure: {
  field: ItemPositionsField;
  restored: boolean;
}) => void;

const listeners = new Set<ItemSaveFailureListener>();
const reverting = new Set<ItemPositionsField>();

export function onItemSaveFailed(
  listener: ItemSaveFailureListener,
): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export async function revertUnsavedItemPositions(
  field: ItemPositionsField,
  error: unknown,
): Promise<void> {
  console.error(`Failed to save ${field}`, error);
  // 연달아 실패한 저장은 진행 중인 되돌리기 하나로 묶어 한 번만 알린다.
  if (reverting.has(field)) return;
  reverting.add(field);
  let restored = false;
  try {
    const channel = itemPositionsChannel(field);
    channel.store().setPositions(await channel.load());
    syncItemPositionsToOverlay(field);
    // 화면이 저장된 배치와 같아졌으니 종료를 막을 저장 안 된 변경이 없다.
    clearEditorSaveFailure(`editor:${field}`);
    restored = true;
  } catch (restoreError) {
    console.error(`Failed to show the saved ${field} again`, restoreError);
  } finally {
    reverting.delete(field);
  }
  listeners.forEach((listener) => listener({ field, restored }));
}

/** 메인 창에서 한 번 켠다. 실패한 저장마다 화면을 저장된 배치로 되돌린다. */
export function startItemSaveRecovery(): () => void {
  return onItemPositionsSaveFailed((field, error) => {
    void revertUnsavedItemPositions(field, error);
  });
}
