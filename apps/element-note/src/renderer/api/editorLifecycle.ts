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

export function flushEditorBeforeClose(request: EditorCloseRequest) {
  return finishEditorLifecycle(request, {
    blurFocusedEditor() {
      if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
      }
    },
    flush: flushEditorInvocations,
    acknowledge: (handshakeId) =>
      invoke<void>('app_quit_after_editor_flush', { handshakeId }),
    cancel: (handshakeId) =>
      invoke<void>('app_cancel_editor_flush', { handshakeId }),
  });
}
