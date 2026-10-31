import { useCallback, useSyncExternalStore } from 'react';
import { getKeySignal } from '@stores/signals/keySignals';

/**
 * Preact signal을 React의 외부 store 계약으로 구독한다.
 *
 * 키 상태는 렌더 이후 네이티브 이벤트에서 바뀌므로, 컴파일러의 signals 자동
 * 추적 여부에 기대면 프로덕션 WebView에서 갱신을 놓칠 수 있다.
 */
export function useKeyActive(key: string): boolean {
  const keySignal = getKeySignal(key);
  const subscribe = useCallback(
    (onStoreChange: () => void) => keySignal.subscribe(onStoreChange),
    [keySignal],
  );
  const getSnapshot = useCallback(() => keySignal.value, [keySignal]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
