import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  VIEWER_HEALTHY_MS,
  VIEWER_RETRY_FIRST_MS,
  ViewerErrorBoundary,
} from './ViewerErrorBoundary';

describe('ViewerErrorBoundary', () => {
  let root: Root;
  let container: HTMLDivElement;
  let broken: boolean;

  function Drawing({ keys }: { keys: string[] }) {
    if (broken)
      throw new TypeError(
        "Cannot read properties of undefined (reading 'hidden')",
      );
    return <div className="viewer">{keys.join(' ')}</div>;
  }

  const draw = (keys: string[]) =>
    act(() =>
      root.render(
        <ViewerErrorBoundary retryOn={[keys]}>
          <Drawing keys={keys} />
        </ViewerErrorBoundary>,
      ),
    );
  const wait = (ms: number) => act(() => vi.advanceTimersByTime(ms));
  const shown = () => container.querySelector('.viewer')?.textContent ?? null;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(console, 'error').mockImplementation(() => {});
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    broken = false;
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('draws again as soon as the data changes, instead of losing the viewer', () => {
    draw(['A', 'B']);
    broken = true;
    const pasted = ['A', 'B', 'A'];
    draw(pasted);
    expect(shown()).toBeNull();

    broken = false;
    draw(pasted); // the same data: still waiting
    expect(shown()).toBeNull();
    draw(['A', 'B', 'A', 'B']);
    expect(shown()).toBe('A B A B');
  });

  it('draws again after a wait that grows while it keeps failing', () => {
    broken = true;
    draw(['A']);
    wait(VIEWER_RETRY_FIRST_MS);
    expect(shown()).toBeNull();

    broken = false;
    wait(VIEWER_RETRY_FIRST_MS);
    expect(shown()).toBeNull();
    wait(VIEWER_RETRY_FIRST_MS);
    expect(shown()).toBe('A');
  });

  it('starts from the first wait again once the viewer stayed healthy', () => {
    broken = true;
    const keys = ['A'];
    draw(keys);
    broken = false;
    wait(VIEWER_RETRY_FIRST_MS);
    expect(shown()).toBe('A');
    wait(VIEWER_HEALTHY_MS);

    broken = true;
    draw(keys);
    broken = false;
    wait(VIEWER_RETRY_FIRST_MS);
    expect(shown()).toBe('A');
  });
});
