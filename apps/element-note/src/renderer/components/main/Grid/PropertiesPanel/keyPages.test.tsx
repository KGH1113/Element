import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { KeyPosition } from '@src/types/key/keys';
import type { StatItemPosition } from '@src/types/key/statItems';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useStatItemStore } from '@stores/data/useStatItemStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import {
  useGridSelectionStore,
  type SelectedElement,
} from '@stores/grid/useGridSelectionStore';
import PropertiesPanel from '../PropertiesPanel';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'en' },
  }),
}));

const KEY: SelectedElement = { type: 'key', id: 'key-0', index: 0 };
const STAT: SelectedElement = { type: 'stat', id: 'stat-0', index: 0 };

describe('the sidebar pages of a key and of a stat', () => {
  let root: Root;
  let container: HTMLDivElement;
  const originalApi = window.api;
  const onKeyUpdate = vi.fn();
  const onPositionChange = vi.fn();

  const select = (selectedElements: SelectedElement[]) =>
    act(() => useGridSelectionStore.setState({ selectedElements }));
  // The groups of rows, each by the names of its rows.
  const groups = () =>
    Array.from(container.querySelectorAll('.en-group')).map((group) =>
      Array.from(group.querySelectorAll('.en-row-label')).map(
        (label) => label.textContent,
      ),
    );
  // Every part is a row: edited or chosen where it is, or entered.
  const row = (label: string) =>
    Array.from(container.querySelectorAll('.en-row')).find(
      (item) => item.querySelector('.en-row-label')?.textContent === label,
    )!;
  const open = (label: string) =>
    act(() => {
      const part = row(label);
      (part.matches('button')
        ? (part as HTMLButtonElement)
        : part.querySelector<HTMLButtonElement>('.en-row-hit')!
      ).click();
    });
  // What the open page is about: its title, or, on the page of the element
  // itself, which has none, the name the page goes by.
  const title = () =>
    container.querySelector('.en-page-title')?.textContent ??
    container.querySelector('.en-page')?.getAttribute('aria-label');
  const back = () =>
    container.querySelector<HTMLButtonElement>('.en-page-back')!;
  const headSwitch = () =>
    container.querySelector<HTMLButtonElement>(
      '.en-page-trailing [role="switch"]',
    )!;
  const type = (field: HTMLInputElement, text: string) =>
    act(() => {
      field.focus();
      Object.getOwnPropertyDescriptor(
        HTMLInputElement.prototype,
        'value',
      )!.set!.call(field, text);
      field.dispatchEvent(new Event('input', { bubbles: true }));
    });

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    onKeyUpdate.mockReset();
    onPositionChange.mockReset();
    window.api = {} as unknown as typeof window.api;
    useSettingsStore.setState({ useCustomCSS: false });
    useKeyStore.setState({
      selectedKeyType: 'hand',
      keyMappings: { hand: ['A'] },
      positions: {
        hand: [
          { id: 'key-a', dx: 10, dy: 20, width: 60, height: 60 },
        ] as KeyPosition[],
      },
    });
    useStatItemStore.setState({
      positions: {
        hand: [
          { dx: 0, dy: 0, width: 80, height: 40, statType: 'kpsMax' },
        ] as StatItemPosition[],
      },
    });
    useGridSelectionStore.setState({ selectedElements: [] });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    act(() =>
      root.render(
        <PropertiesPanel
          onPositionChange={onPositionChange}
          onKeyUpdate={onKeyUpdate}
          renderElementList={() => <p data-element-list />}
        />,
      ),
    );
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    window.api = originalApi;
    useGridSelectionStore.setState({ selectedElements: [] });
  });

  it('opens on the key: what is one value in rows, what is many settings as pages, its ghost last', () => {
    select([KEY]);
    expect(title()).toBe('A');
    expect(groups()).toEqual([
      [
        'propertiesPanel.tabInput',
        'propertiesPanel.position',
        'propertiesPanel.size',
      ],
      [
        'sidebar.partShape',
        'sidebar.partText',
        'propertiesPanel.tabNote',
        'propertiesPanel.tabCounter',
        'propertiesPanel.sectionSound',
      ],
      ['sidebar.ghostKey'],
    ]);
    // The two faces are not switched in the head any more.
    expect(container.querySelector('.en-page-head .dmn-segmented')).toBeNull();
    // Rows that hold a control open nothing.
    for (const label of [
      'propertiesPanel.tabInput',
      'propertiesPanel.position',
      'propertiesPanel.size',
    ]) {
      expect(row(label).querySelector('.en-row-hit')).toBeNull();
    }
  });

  it('binds the key and moves it from its rows, without leaving the page', () => {
    select([KEY]);
    const capture = row('propertiesPanel.tabInput').querySelector('button')!;
    expect(capture.textContent).toBe('A');
    act(() => capture.click());
    expect(capture.getAttribute('aria-pressed')).toBe('true');
    expect(capture.textContent).toBe('propertiesPanel.pressAnyKey');

    type(
      container.querySelector<HTMLInputElement>(
        'input[aria-label="propertiesPanel.position X"]',
      )!,
      '33',
    );
    expect(onPositionChange).toHaveBeenLastCalledWith(0, 33, 20);
    expect(title()).toBe('A');
  });

  it('keeps the sound on one page: its row says off, its switch is in the head', () => {
    select([KEY]);
    expect(row('propertiesPanel.sectionSound').textContent).toContain(
      'sidebar.stateOff',
    );
    open('propertiesPanel.sectionSound');
    expect(title()).toBe('propertiesPanel.sectionSound');
    // The file and the volume, and nothing that leads to another page.
    expect(container.querySelector('.en-row')).toBeNull();
    expect(container.textContent).toContain('propertiesPanel.keySound');
    expect(container.textContent).toContain('propertiesPanel.soundVolume');
    act(() => headSwitch().click());
    expect(onKeyUpdate).toHaveBeenLastCalledWith({
      index: 0,
      soundEnabled: true,
    });
  });

  it('gives the ghost a page of its own, and returns to it from its parts', () => {
    select([KEY]);
    expect(row('sidebar.ghostKey').textContent).toContain('sidebar.stateOff');
    open('sidebar.ghostKey');
    expect(title()).toBe('sidebar.ghostKey');
    expect(back().textContent).toBe('A');
    // The ghost is drawn where its key is: it has no position or size.
    expect(groups()).toEqual([
      ['propertiesPanel.tabInput'],
      [
        'sidebar.partShape',
        'sidebar.partText',
        'propertiesPanel.tabNote',
        'propertiesPanel.tabCounter',
        'propertiesPanel.sectionSound',
      ],
    ]);

    // Turning it on asks for its key at once, in its row.
    act(() => headSwitch().click());
    expect(onKeyUpdate).toHaveBeenLastCalledWith({
      index: 0,
      ghost: expect.objectContaining({ enabled: true }),
    });
    expect(
      row('propertiesPanel.tabInput')
        .querySelector('button')!
        .getAttribute('aria-pressed'),
    ).toBe('true');

    act(() =>
      useKeyStore.setState((state) => ({
        positions: {
          hand: [
            {
              ...state.positions.hand[0],
              ghost: onKeyUpdate.mock.lastCall![0].ghost,
            },
          ],
        },
      })),
    );
    open('sidebar.partShape');
    expect(title()).toBe('sidebar.partShape');
    expect(back().textContent).toBe('sidebar.ghostKey');
    act(() => back().click());
    expect(title()).toBe('sidebar.ghostKey');
    act(() => back().click());
    expect(title()).toBe('A');
  });

  it("chooses a stat's kind in its row, from one list", () => {
    select([STAT]);
    expect(groups()).toEqual([
      [
        'propertiesPanel.statType',
        'propertiesPanel.position',
        'propertiesPanel.size',
      ],
      ['sidebar.partShape', 'sidebar.partText', 'sidebar.partNumber'],
    ]);
    const kind = row('propertiesPanel.statType');
    expect(kind.querySelector('.en-row-hit')).toBeNull();
    const trigger = kind.querySelector<HTMLButtonElement>(
      '[aria-haspopup="listbox"]',
    )!;
    expect(trigger.textContent).toBe('MAX');
    act(() => trigger.click());
    expect(
      Array.from(document.querySelectorAll('[role="option"]')).map(
        (option) => option.textContent,
      ),
    ).toEqual(['KPS', 'AVG', 'MAX', 'Total']);
  });
});
