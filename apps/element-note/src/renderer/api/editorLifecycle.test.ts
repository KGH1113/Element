import { describe, expect, it, vi } from 'vitest';
import { finishEditorLifecycle } from './editorLifecycle';

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
