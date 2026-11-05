import { invoke as nativeInvoke } from '@tauri-apps/api/core';

/** 종료 확인 전에 아직 저장 중인 요청과 이어지는 요청을 모두 기다린다. */
export function createInvocationBarrier() {
  const pending = new Set<Promise<unknown>>();
  const failures = new Map<string, unknown>();

  function track<T>(request: Promise<T>): Promise<T> {
    pending.add(request);
    void request.then(
      () => pending.delete(request),
      () => pending.delete(request),
    );
    return request;
  }

  async function flush(): Promise<void> {
    // blur 핸들러가 예약한 첫 마이크로태스크도 포함한다.
    await Promise.resolve();
    while (pending.size > 0) {
      const results = await Promise.allSettled([...pending]);
      const failure = results.find((result) => result.status === 'rejected');
      if (failure?.status === 'rejected') throw failure.reason;
    }
    if (failures.size > 0) throw failures.values().next().value;
  }

  return {
    track,
    flush,
    setFailure(owner: string, error: unknown) {
      failures.set(owner, error);
    },
    clearFailure(owner: string) {
      failures.delete(owner);
    },
  };
}

const barrier = createInvocationBarrier();
export const trackEditorInvocation = barrier.track;
export const flushEditorInvocations = barrier.flush;
export const setEditorSaveFailure = barrier.setFailure;
export const clearEditorSaveFailure = barrier.clearFailure;

/** 실패한 필드가 실제로 저장될 때까지 종료·히스토리 이동을 막는다. */
export function trackEditorSave<T>(
  request: Promise<T>,
  owners: string[],
): Promise<T> {
  const saving = request.then(
    (result) => {
      owners.forEach(clearEditorSaveFailure);
      return result;
    },
    (error: unknown) => {
      owners.forEach((owner) => setEditorSaveFailure(owner, error));
      throw error;
    },
  );
  return trackEditorInvocation(saving);
}

const lifecycleRequests = new Set([
  'history_undo',
  'history_redo',
  'app_quit',
  'app_restart',
  'window_close',
  'app_quit_after_editor_flush',
  'app_cancel_editor_flush',
]);

export function invoke<T>(
  ...args: Parameters<typeof nativeInvoke>
): Promise<T> {
  const request = nativeInvoke<T>(...args);
  // 이 요청들은 flush 응답을 기다리므로 저장 대기에 포함하면 교착된다.
  return lifecycleRequests.has(args[0]) ? request : barrier.track(request);
}
