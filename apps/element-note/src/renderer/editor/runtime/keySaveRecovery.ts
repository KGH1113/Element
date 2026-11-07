/**
 * 키 변경 저장이 실패했을 때 화면을 저장된 키로 되돌린다.
 * 화면·얼불춤 키 리미터·저장 파일이 서로 다른 키를 갖지 않게 하고, 메인 창이 사용자에게 알린다.
 */

import { clearEditorSaveFailure } from '@api/invoke';
import { projectNativeKeyMappings } from '@api/modules/editorApi';
import { useKeyStore } from '@stores/data/useKeyStore';
import type { KeyPositions } from '@src/types/key/keys';

/** 실패한 저장이 담았던 필드. 이 필드만 저장된 값으로 되돌린다. */
export type UnsavedKeyField = 'keys' | 'keyPositions';

/** restored: 화면이 저장된 키로 돌아갔는지. false면 저장된 키를 읽지도 못했다. */
type KeySaveFailureListener = (failure: { restored: boolean }) => void;

const listeners = new Set<KeySaveFailureListener>();

export function onKeySaveFailed(listener: KeySaveFailureListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export async function revertUnsavedKeyChange(
  error: unknown,
  fields: readonly UnsavedKeyField[],
): Promise<void> {
  console.error('Failed to save the key change', error);
  let restored = false;
  try {
    const { document } = await window.api.editor.get();
    const store = useKeyStore.getState();
    const mappings = projectNativeKeyMappings(document.keys);
    const positions = document.keyPositions as KeyPositions;
    if (fields.includes('keys') && fields.includes('keyPositions')) {
      // 키 수가 바뀐 변경은 매핑과 위치를 함께 되돌려 길이가 어긋난 중간 상태를 만들지 않는다.
      store.setKeyMappingsAndPositions(mappings, positions);
    } else if (fields.includes('keys')) {
      store.setKeyMappings(mappings);
    } else {
      store.setPositions(positions);
    }
    // 화면이 저장된 값과 같아졌으니 종료를 막을 저장 안 된 변경이 없다.
    fields.forEach((field) => clearEditorSaveFailure(`editor:${field}`));
    restored = true;
  } catch (restoreError) {
    console.error('Failed to show the saved keys again', restoreError);
  }
  listeners.forEach((listener) => listener({ restored }));
}
