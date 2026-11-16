import { beforeEach, describe, expect, it, vi } from 'vitest';

const nativeInvoke = vi.hoisted(() => vi.fn());
vi.mock('@tauri-apps/api/core', () => ({ invoke: nativeInvoke }));

import {
  answerFlushWithoutEditor,
  finishEditorLifecycle,
  flushEditorBeforeClose,
} from './editorLifecycle';
import { clearEditorSaveFailure, setEditorSaveFailure } from './invoke';

describe('v2 editor lifecycle', () => {
  it('commits the focused editor and waits before acknowledging shutdown', async () => {
    const order: string[] = [];
    await finishEditorLifecycle(
      { handshakeId: 'closing', action: 'quit' },
      {
        blurFocusedEditor: () => {
          order.push('blur');
        },
        flush: async () => {
          order.push('flush');
        },
        acknowledge: async (id) => {
          order.push(`ack:${id}`);
        },
        cancel: async () => {
          order.push('cancel');
        },
      },
    );
    expect(order).toEqual(['blur', 'flush', 'ack:closing']);
  });

  it('cancels restart when saving fails', async () => {
    const acknowledge = vi.fn();
    const cancel = vi.fn().mockResolvedValue(undefined);
    const failure = new Error('save failed');
    await expect(
      finishEditorLifecycle(
        { handshakeId: 'restart', action: 'restart' },
        {
          blurFocusedEditor: () => {},
          flush: async () => {
            throw failure;
          },
          acknowledge,
          cancel,
        },
      ),
    ).rejects.toBe(failure);
    expect(acknowledge).not.toHaveBeenCalled();
    expect(cancel).toHaveBeenCalledWith('restart');
  });
});

describe('a window that edits nothing', () => {
  beforeEach(() => {
    nativeInvoke.mockReset().mockResolvedValue(undefined);
  });

  it('answers an undo request at once', async () => {
    await answerFlushWithoutEditor({ handshakeId: 'undo', action: 'history' });

    expect(nativeInvoke).toHaveBeenCalledExactlyOnceWith(
      'app_quit_after_editor_flush',
      { handshakeId: 'undo' },
    );
  });

  it('does not hold undo or quitting back over a save of its own that failed', async () => {
    setEditorSaveFailure('plugin:viewer', new Error('rejected'));
    try {
      await answerFlushWithoutEditor({ handshakeId: 'quit', action: 'quit' });
      expect(nativeInvoke).toHaveBeenCalledExactlyOnceWith(
        'app_quit_after_editor_flush',
        { handshakeId: 'quit' },
      );

      // The editing window still refuses while its changes are unsaved.
      nativeInvoke.mockClear();
      await expect(
        flushEditorBeforeClose({ handshakeId: 'undo', action: 'history' }),
      ).rejects.toThrow('rejected');
      expect(nativeInvoke).toHaveBeenCalledExactlyOnceWith(
        'app_cancel_editor_flush',
        { handshakeId: 'undo' },
      );
    } finally {
      clearEditorSaveFailure('plugin:viewer');
    }
  });
});
