import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useSettingsStore } from '@stores/useSettingsStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import { getDefaultNoteSettings } from '@src/renderer/defaults';
import { RainingFrameLimitRow, RainingGroups } from './RainingSettings';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, params?: Record<string, string | number>) =>
      params ? `${key}:${Object.values(params).join(',')}` : key,
  }),
}));

describe('Raining settings', () => {
  let root: Root;
  let container: HTMLDivElement;
  const update = vi.fn(async () => ({}));
  const setOwn = vi.fn(async () => ({}));
  const originalApi = window.api;
  const shared = getDefaultNoteSettings();

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    update.mockClear();
    setOwn.mockClear();
    window.api = {
      settings: { update },
      noteTab: { set: setOwn },
    } as unknown as typeof window.api;
    useSettingsStore.setState({ noteSettings: shared, tabNoteOverrides: {} });
    useKeyStore.setState({
      tabs: [
        { id: 'hand-16', name: '16K', viewerKind: 'hand' },
        { id: 'foot-pedal', name: 'Pedal', viewerKind: 'foot' },
      ],
    });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    window.api = originalApi;
  });

  const control = <T extends HTMLElement>(label: string) =>
    container.querySelector<T>(`[aria-label="${label}"]`)!;
  const type = (input: HTMLInputElement, value: string) => {
    act(() => input.focus());
    act(() => {
      Object.getOwnPropertyDescriptor(
        HTMLInputElement.prototype,
        'value',
      )!.set!.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
  };

  it('has every value in a row of its own and no dialog to open', () => {
    act(() => root.render(<RainingGroups />));
    expect(
      Array.from(
        container.querySelectorAll('.dmn-setting-row__label'),
        (label) => label.textContent,
      ),
    ).toEqual([
      'raining.scope',
      'raining.speed',
      'raining.length',
      'raining.downward',
      'raining.fade',
      'raining.shortSame',
      'raining.shortLength',
      'raining.shortTime',
      'raining.keyDelay',
    ]);
    expect(document.querySelector('[data-dmn-modal-backdrop]')).toBeNull();
  });

  it('keeps a switch at once, for every key viewer', () => {
    act(() => root.render(<RainingGroups />));
    act(() => control<HTMLButtonElement>('raining.downward').click());
    expect(update).toHaveBeenCalledTimes(1);
    expect(update).toHaveBeenLastCalledWith({
      noteSettings: { ...shared, reverse: true },
    });
    expect(useSettingsStore.getState().noteSettings.reverse).toBe(true);
    expect(setOwn).not.toHaveBeenCalled();
  });

  it('keeps a number when its field is left, not at every digit', () => {
    act(() => root.render(<RainingGroups />));
    const speed = control<HTMLInputElement>('raining.speed');
    type(speed, '2');
    type(speed, '25');
    type(speed, '250');
    expect(update).not.toHaveBeenCalled();
    act(() => speed.blur());
    expect(update).toHaveBeenCalledTimes(1);
    expect(update).toHaveBeenLastCalledWith({
      noteSettings: { ...shared, speed: 250 },
    });
  });

  it('greys what follows a switch that is off, in place', () => {
    act(() => root.render(<RainingGroups />));
    const length = () => control<HTMLInputElement>('raining.shortLength');
    expect(length().disabled).toBe(true);
    act(() => control<HTMLButtonElement>('raining.shortSame').click());
    expect(length().disabled).toBe(false);
  });

  it('offers the time Raining takes to its end as the delay of the key press', () => {
    act(() => root.render(<RainingGroups />));
    const travelMs = Math.round((shared.trackHeight / shared.speed) * 1000);
    const use = Array.from(container.querySelectorAll('button')).find(
      (button) => button.textContent === `raining.useRecommended:${travelMs}`,
    )!;
    act(() => use.click());
    expect(update).toHaveBeenLastCalledWith({
      noteSettings: { ...shared, keyDisplayDelayMs: travelMs },
    });
  });

  it('gives one key viewer values of its own and forgets them again', () => {
    act(() => root.render(<RainingGroups />));
    // Nothing to forget while every key viewer shares the values.
    const same = () =>
      Array.from(container.querySelectorAll('button')).find(
        (button) => button.textContent === 'raining.sameAsAll',
      )!;
    expect(same().style.visibility).toBe('hidden');

    act(() => control<HTMLButtonElement>('raining.scope').click());
    const option = Array.from(
      document.querySelectorAll<HTMLElement>('[role="option"]'),
    ).find((item) => item.textContent === 'viewers.foot · Pedal')!;
    act(() => {
      option.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
      option.click();
    });
    act(() => control<HTMLButtonElement>('raining.downward').click());
    // Only what differs is kept for that key viewer; the shared values stay.
    expect(setOwn).toHaveBeenLastCalledWith('foot-pedal', { reverse: true });
    expect(update).not.toHaveBeenCalled();
    expect(useSettingsStore.getState().noteSettings.reverse).toBe(false);
    expect(useSettingsStore.getState().tabNoteOverrides).toEqual({
      'foot-pedal': { reverse: true },
    });

    expect(same().style.visibility).toBe('');
    act(() => same().click());
    expect(setOwn).toHaveBeenLastCalledWith('foot-pedal', null);
    expect(useSettingsStore.getState().tabNoteOverrides).toEqual({});
  });

  it('keeps how often Raining is drawn apart from how it looks', () => {
    act(() => root.render(<RainingFrameLimitRow />));
    const limit = control<HTMLInputElement>('raining.frameLimit');
    type(limit, '120');
    act(() => limit.blur());
    expect(update).toHaveBeenLastCalledWith({
      noteSettings: { ...shared, frameLimit: 120 },
    });
  });
});
