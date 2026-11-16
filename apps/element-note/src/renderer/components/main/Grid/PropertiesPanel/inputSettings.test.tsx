import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { KeyPosition } from '@src/types/key/keys';
import InputTabContent from './single/InputTabContent';
import StyleTabContent from './single/StyleTabContent';
import KeySoundControls from './KeySoundControls';
import { KeyCaptureButton } from './PartControls';
import { Tabs } from './PropertyInputs';
import { TABS } from './types';

// The two states of a color name themselves.
vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('@components/main/Modal/content/pickers/SoundPicker', () => ({
  default: ({ onSoundSelect }: { onSoundSelect: (path: string) => void }) => (
    <button onClick={() => onSoundSelect('/sounds/tap.wav')}>
      Pick test sound
    </button>
  ),
}));

describe('input, sound and appearance settings', () => {
  let root: Root;
  let container: HTMLDivElement;
  const update = vi.fn();
  const listen = vi.fn();
  const t = (key: string) => key;
  const position = {
    dx: 0,
    dy: 0,
    width: 60,
    height: 60,
    soundEnabled: false,
  } as KeyPosition;

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    update.mockReset();
    listen.mockReset();
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
  });

  it('binds a key from the button that names it', () => {
    const render = (listening: boolean, keyName?: string) =>
      act(() =>
        root.render(
          <KeyCaptureButton
            keyName={keyName}
            listening={listening}
            onListen={listen}
            t={t}
          />,
        ),
      );
    render(false, 'A');
    const button = container.querySelector('button')!;
    expect(button.textContent).toBe('A');
    expect(button.getAttribute('aria-pressed')).toBe('false');
    act(() => button.click());
    expect(listen).toHaveBeenCalledOnce();

    // While it listens it says what to do, in the same place.
    render(true, 'A');
    expect(button.textContent).toBe('propertiesPanel.pressAnyKey');
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(button.querySelector('[role="status"]')).not.toBeNull();

    render(false);
    expect(button.textContent).toBe('propertiesPanel.clickToSet');
  });

  it('keeps the sound file and volume in place, greyed out, while the sound is off', () => {
    act(() =>
      root.render(
        <InputTabContent
          keyIndex={4}
          keyPosition={position}
          onKeyUpdate={update}
          t={t}
        />,
      ),
    );
    // Whether the key makes a sound is the part's switch, in the page head.
    expect(container.querySelector('[role="switch"]')).toBeNull();
    const details = container.querySelector('input')!.closest('.en-dimmed')!;
    expect(details.getAttribute('data-off')).toBe('true');
    expect(details.hasAttribute('inert')).toBe(true);
    expect(details.textContent).toContain('propertiesPanel.keySound');
    expect(details.textContent).toContain('propertiesPanel.soundVolume');
  });

  it('chooses a sound for the key from its row, and closes the chooser when the sound is turned off', () => {
    const render = (enabled: boolean) =>
      act(() =>
        root.render(
          <InputTabContent
            keyIndex={7}
            keyPosition={{ ...position, soundEnabled: enabled }}
            onKeyUpdate={update}
            t={t}
          />,
        ),
      );
    render(true);
    const choose = Array.from(container.querySelectorAll('button')).find(
      (button) => button.textContent === 'propertiesPanel.chooseSound',
    )!;
    expect(choose.getAttribute('aria-expanded')).toBe('false');
    act(() => choose.click());
    expect(choose.getAttribute('aria-expanded')).toBe('true');
    act(() =>
      Array.from(container.querySelectorAll('button'))
        .find((button) => button.textContent === 'Pick test sound')!
        .click(),
    );
    expect(update).toHaveBeenCalledWith({
      index: 7,
      soundPath: '/sounds/tap.wav',
    });
    render(false);
    expect(container.textContent).not.toContain('Pick test sound');
    expect(
      container
        .querySelector('input')!
        .closest('.en-dimmed')!
        .hasAttribute('inert'),
    ).toBe(true);
  });

  it('keeps a mixed sound selection editable without writing over any selected key', () => {
    act(() =>
      root.render(
        <KeySoundControls
          value={position}
          mixed={{ enabled: true, path: true }}
          onChange={update}
          t={t}
        />,
      ),
    );
    expect(container.textContent).toContain('propertiesPanel.mixedValues');
    expect(
      container
        .querySelector('input')!
        .closest('.en-dimmed')!
        .hasAttribute('inert'),
    ).toBe(false);
    expect(update).not.toHaveBeenCalled();
  });

  it('keeps the key, its sound, its position and its size out of Appearance', () => {
    act(() =>
      root.render(
        <StyleTabContent
          keyIndex={0}
          keyPosition={position}
          keyCode="A"
          keyInfo={{ globalKey: 'A', displayName: 'A' }}
          onKeyUpdate={update}
          t={t}
        />,
      ),
    );
    expect(container.textContent).toContain('propertiesPanel.backgroundColor');
    for (const elsewhere of [
      'propertiesPanel.keySound',
      'propertiesPanel.soundVolume',
      'propertiesPanel.position',
      'propertiesPanel.size',
      'propertiesPanel.statType',
    ]) {
      expect(container.textContent).not.toContain(elsewhere);
    }
  });

  it('moves between editor sections with the keyboard and keeps one tab stop', () => {
    act(() =>
      root.render(
        <Tabs
          activeTab={TABS.INPUT}
          availableTabs={[TABS.INPUT, TABS.STYLE, TABS.NOTE, TABS.COUNTER]}
          onTabChange={update}
          t={t}
        />,
      ),
    );
    const tabs = container.querySelectorAll<HTMLButtonElement>('[role="tab"]');
    expect(Array.from(tabs).filter((tab) => tab.tabIndex === 0)).toHaveLength(
      1,
    );
    act(() =>
      tabs[0].dispatchEvent(
        new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }),
      ),
    );
    expect(update).toHaveBeenLastCalledWith(TABS.STYLE);
    expect(document.activeElement).toBe(tabs[1]);
    act(() =>
      tabs[1].dispatchEvent(
        new KeyboardEvent('keydown', { key: 'End', bubbles: true }),
      ),
    );
    expect(update).toHaveBeenLastCalledWith(TABS.COUNTER);
  });
});
