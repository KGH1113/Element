import { invoke, flushEditorInvocations } from './invoke';

export interface EditorCloseRequest {
  handshakeId: string;
  action: 'quit' | 'restart' | 'history';
}

export interface LifecycleDependencies {
  blurFocusedEditor(): void;
  flush(): Promise<void>;
  acknowledge(handshakeId: string): Promise<void>;
  cancel(handshakeId: string): Promise<void>;
}

export async function finishEditorLifecycle(
  request: EditorCloseRequest,
  dependencies: LifecycleDependencies,
): Promise<void> {
  try {
    dependencies.blurFocusedEditor();
    await dependencies.flush();
    await dependencies.acknowledge(request.handshakeId);
  } catch (error) {
    await dependencies.cancel(request.handshakeId).catch(() => undefined);
    throw error;
  }
}

const answers: Pick<LifecycleDependencies, 'acknowledge' | 'cancel'> = {
  acknowledge: (handshakeId) =>
    invoke<void>('app_quit_after_editor_flush', { handshakeId }),
  cancel: (handshakeId) =>
    invoke<void>('app_cancel_editor_flush', { handshakeId }),
};

export function flushEditorBeforeClose(request: EditorCloseRequest) {
  return finishEditorLifecycle(request, {
    blurFocusedEditor() {
      if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
      }
    },
    flush: flushEditorInvocations,
    ...answers,
  });
}

/**
 * 편집 화면이 없는 창(키뷰어)의 응답. 네이티브는 모든 창의 응답을 기다리므로, 보내던
 * 요청만 끝나면 바로 답한다. 이 창의 저장 실패는 종료나 실행 취소를 막지 않는다:
 * 고칠 화면이 없고, 알리는 일은 편집 창이 맡는다.
 */
export function answerFlushWithoutEditor(request: EditorCloseRequest) {
  return finishEditorLifecycle(request, {
    blurFocusedEditor() {},
    flush: () => flushEditorInvocations().catch(() => undefined),
    ...answers,
  });
}
