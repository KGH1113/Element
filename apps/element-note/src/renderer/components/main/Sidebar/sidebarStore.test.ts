import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SIDEBAR_WIDTH, useSidebarStore } from './sidebarStore';

describe('sidebar store', () => {
  let saved: Map<string, string>;

  beforeEach(() => {
    saved = new Map();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => saved.get(key) ?? null,
      setItem: (key: string, value: string) => void saved.set(key, value),
    });
    useSidebarStore.setState({ width: SIDEBAR_WIDTH.default });
  });
  afterEach(() => vi.unstubAllGlobals());

  it('keeps the width between its limits', () => {
    useSidebarStore.getState().setWidth(10);
    expect(useSidebarStore.getState().width).toBe(SIDEBAR_WIDTH.min);
    useSidebarStore.getState().setWidth(5000);
    expect(useSidebarStore.getState().width).toBe(SIDEBAR_WIDTH.max);
    useSidebarStore.getState().setWidth(400.4);
    expect(useSidebarStore.getState().width).toBe(400);
  });

  it('remembers the width only when asked to save it', () => {
    useSidebarStore.getState().setWidth(420);
    expect(saved.has('elementnote:sidebar-width')).toBe(false);
    useSidebarStore.getState().setWidth(420, true);
    expect(saved.get('elementnote:sidebar-width')).toBe('420');
  });

  it('counts delete requests so the canvas can react to each one', () => {
    const before = useSidebarStore.getState().deleteSignal;
    useSidebarStore.getState().requestDelete();
    useSidebarStore.getState().requestDelete();
    expect(useSidebarStore.getState().deleteSignal).toBe(before + 2);
  });
});
