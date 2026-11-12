import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Alert from './Alert';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe('alert dialog', () => {
  let root: Root;
  let container: HTMLDivElement;

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    vi.useFakeTimers({ toFake: ['requestAnimationFrame'] });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.useRealTimers();
  });

  const answers = () =>
    Array.from(
      document.querySelectorAll<HTMLButtonElement>('.dmn-alert-answers button'),
    );
  const open = (props: Partial<React.ComponentProps<typeof Alert>>) => {
    act(() => root.render(<Alert isOpen message="Message" {...props} />));
    // The dialog takes focus on the frame after it opens.
    act(() => vi.advanceTimersToNextFrame());
  };

  it('has one answer for a message, and opens on it', () => {
    const onConfirm = vi.fn();
    open({ onConfirm });
    expect(answers().map((answer) => answer.textContent)).toEqual([
      'common.confirm',
    ]);
    expect(document.activeElement).toBe(answers()[0]);
    // Backing out of a message is its one answer.
    act(() =>
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })),
    );
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('asks with backing out on the left and going ahead on the right', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    open({ type: 'confirm', confirmText: 'Save', onConfirm, onCancel });
    const [cancel, confirm] = answers();
    expect([cancel.textContent, confirm.textContent]).toEqual([
      'common.cancel',
      'Save',
    ]);
    expect([cancel.dataset.variant, confirm.dataset.variant]).toEqual([
      'secondary',
      'primary',
    ]);
    // Enter goes ahead.
    expect(document.activeElement).toBe(confirm);
    act(() => confirm.click());
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('opens on Cancel when going ahead cannot be taken back', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    open({
      type: 'confirm',
      confirmText: 'Delete',
      danger: true,
      onConfirm,
      onCancel,
    });
    const [cancel, confirm] = answers();
    expect(confirm.dataset.variant).toBe('danger');
    expect(cancel.dataset.variant).toBe('secondary');
    expect(document.activeElement).toBe(cancel);
    act(() =>
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })),
    );
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
