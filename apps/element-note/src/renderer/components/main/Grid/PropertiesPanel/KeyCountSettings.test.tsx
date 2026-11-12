import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { applyCounterCacheSnapshot } from '@stores/signals/keyCounterCache';
import type { KeyCounterUpdate } from '@src/types/plugin/api';
import KeyCountSettings from './KeyCountSettings';

describe('key count settings', () => {
  let root: Root;
  let container: HTMLDivElement;
  const originalApi = window.api;
  let onOne: ((update: KeyCounterUpdate) => void) | null = null;
  const setSingleCounter = vi.fn();

  const field = () =>
    container.querySelector<HTMLInputElement>(
      'input[aria-label="Press count"]',
    )!;
  const enter = async (text: string) => {
    await act(async () => {
      const input = field();
      input.focus();
      Object.getOwnPropertyDescriptor(
        HTMLInputElement.prototype,
        'value',
      )!.set!.call(input, text);
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
    await act(async () => {
      field().blur();
    });
  };

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    applyCounterCacheSnapshot({ '4key': { KeyD: 42 } });
    setSingleCounter.mockReset();
    window.api = {
      ...originalApi,
      keys: {
        ...originalApi?.keys,
        onCounterChanged: (listener: (update: KeyCounterUpdate) => void) => {
          onOne = listener;
          return () => {
            onOne = null;
          };
        },
        onCountersChanged: () => () => undefined,
        setSingleCounter,
      },
    } as unknown as typeof window.api;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    act(() =>
      root.render(
        <KeyCountSettings
          mode="4key"
          keyCode="KeyD"
          t={(key) =>
            ({
              'keyCount.label': 'Press count',
              'keyCount.now': 'Count now',
              'keyCount.failed': "Couldn't save it.",
            })[key] ?? key
          }
        />,
      ),
    );
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    window.api = originalApi;
  });

  it('shows the count of the key, and follows it as the key is pressed', () => {
    expect(field().value).toBe('42');
    act(() => onOne?.({ mode: '4key', key: 'KeyD', count: 43 }));
    expect(field().value).toBe('43');
    // Another key's count leaves it alone.
    act(() => onOne?.({ mode: '4key', key: 'KeyF', count: 9 }));
    expect(field().value).toBe('43');
  });

  it('sets the count to what is typed; 0 resets it', async () => {
    setSingleCounter.mockResolvedValue({});
    await enter('1234');
    expect(setSingleCounter).toHaveBeenLastCalledWith('4key', 'KeyD', 1234);
    await enter('0');
    expect(setSingleCounter).toHaveBeenLastCalledWith('4key', 'KeyD', 0);
  });

  it('says so and shows the count as it is when it could not be saved', async () => {
    setSingleCounter.mockRejectedValue(new Error('offline'));
    const quiet = vi.spyOn(console, 'error').mockImplementation(() => {});
    await enter('500');
    expect(container.querySelector('[role=alert]')?.textContent).toBe(
      "Couldn't save it.",
    );
    expect(field().value).toBe('42');
    quiet.mockRestore();
  });
});
