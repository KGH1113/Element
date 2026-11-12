import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorChanges } from '@src/types/editor';
import type { KeyPosition } from '@src/types/key/keys';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import { useSettingsStore } from '@stores/useSettingsStore';
import {
  useGridSelectionStore,
  type SelectedElement,
} from '@stores/grid/useGridSelectionStore';
import {
  makeSpritePose,
  makeSpritePosition,
} from '@utils/sprite/spriteFixtures';
import { SPRITE_FEELS } from '@utils/sprite/spriteMotion';
import PropertiesPanel from '../PropertiesPanel';

vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, options?: Record<string, unknown>) => {
      const given = options?.number ?? options?.keys ?? options?.value;
      return given === undefined ? key : `${key} ${given}`;
    },
    i18n: { language: 'en' },
  }),
}));

const SPRITE: SelectedElement = { type: 'sprite', id: 'sprite-0', index: 0 };

describe('the sidebar pages of an image', () => {
  let root: Root;
  let container: HTMLDivElement;
  const originalApi = window.api;
  const commitChanges = vi.fn(async (_changes: EditorChanges) => undefined);

  const select = (selectedElements: SelectedElement[]) =>
    act(() => useGridSelectionStore.setState({ selectedElements }));
  const rows = () =>
    Array.from(container.querySelectorAll('.en-row')).map((row) => [
      row.querySelector('.en-row-label')?.textContent,
      row.querySelector('.en-row-value')?.textContent,
    ]);
  const press = (label: string) =>
    act(() =>
      Array.from(container.querySelectorAll('button'))
        .find(
          (button) =>
            button.getAttribute('aria-label') === label ||
            button.textContent === label,
        )!
        .click(),
    );
  const sprite = () => useSpriteStore.getState().positions.hand[0];
  /** Changes the image in the store, as the backend or an undo would. */
  const setSprite = (overrides: Partial<ReactiveSpritePosition>) =>
    act(() =>
      useSpriteStore.setState((state) => ({
        positions: {
          hand: state.positions.hand.map((item, index) =>
            index === 0 ? { ...item, ...overrides } : item,
          ),
        },
      })),
    );
  /** The choices of the chooser that is open. */
  const tiles = () =>
    Array.from(document.body.querySelectorAll<HTMLButtonElement>('.en-tile'));
  const tileState = (tile: HTMLButtonElement) => [
    tile.querySelector('.en-tile-label')?.textContent,
    tile.getAttribute('aria-pressed'),
  ];
  // What the open page is about: its title, or, on the page of the element
  // itself, which has none, the name the page goes by.
  const title = () =>
    container.querySelector('.en-page-title')?.textContent ??
    container.querySelector('.en-page')?.getAttribute('aria-label');
  const back = () =>
    container.querySelector<HTMLButtonElement>('.en-page-back')!;

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    commitChanges.mockReset();
    commitChanges.mockResolvedValue(undefined);
    window.api = {
      editor: { commitChanges },
    } as unknown as typeof window.api;
    useSettingsStore.setState({ useCustomCSS: false });
    useKeyStore.setState({
      selectedKeyType: 'hand',
      keyMappings: { hand: ['A'] },
      positions: {
        hand: [
          { id: 'key-a', dx: 0, dy: 0, width: 60, height: 60 },
        ] as KeyPosition[],
      },
    });
    useSpriteStore.setState({
      positions: {
        hand: [
          makeSpritePosition({
            id: 'cat',
            width: 120,
            height: 80,
            zIndex: 1,
            baseImage: '/images/cat.png',
            poses: [
              makeSpritePose({ poseId: 'tap', triggers: ['key-a'] }),
              makeSpritePose({ poseId: 'new', triggers: [] }),
            ],
          }),
          makeSpritePosition({ id: 'dog', zIndex: 2 }),
        ],
      },
      isLocalUpdateInProgress: false,
    });
    useGridSelectionStore.setState({ selectedElements: [] });
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    act(() =>
      root.render(
        <PropertiesPanel
          onPositionChange={vi.fn()}
          onKeyUpdate={vi.fn()}
          renderElementList={({ selectionCount }) => (
            <p data-element-list>{selectionCount}</p>
          )}
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

  it('opens on the selected image: its picture, its box, its looks and how it moves, all on one page', () => {
    select([SPRITE]);
    expect(title()).toBe('sidebar.addSprite 1');
    expect(rows().map(([label]) => label)).toEqual([
      'sidebar.addSprite',
      'propertiesPanel.position',
      'propertiesPanel.size',
      'sprite.rotation',
      'sprite.visible',
      'sprite.rest',
      'sprite.lookNumber 1',
      'sprite.lookNumber 2',
      'sprite.addLook',
      'sprite.reaction',
      'sprite.feel',
      'sprite.changeTime',
      'sprite.returnTime',
    ]);
    expect(rows().filter(([, value]) => value !== undefined)).toEqual([
      ['sidebar.addSprite', 'cat.png'],
      // Each look says when it is shown; one still waits for a key.
      ['sprite.rest', 'sprite.restWhen'],
      ['sprite.lookNumber 1', 'sprite.lookWhen A'],
      ['sprite.lookNumber 2', 'sprite.needsKey'],
      ['sprite.addLook', 'sprite.addLookWhen'],
      ['sprite.reaction', 'sprite.whileHeld'],
      ['sprite.feel', 'sprite.feelSmooth'],
    ]);
    expect(container.querySelector('.en-row-image')).not.toBeNull();
    // Every look is drawn at the start of its row.
    expect(container.querySelectorAll('.en-row .en-look-thumb')).toHaveLength(
      3,
    );
    // Nothing the user reads is code or a unit of it.
    expect(container.textContent).not.toMatch(/cubic-bezier|\bms\b|\d+ms/);

    // The CSS part is there only while custom CSS is in use.
    act(() => useSettingsStore.setState({ useCustomCSS: true }));
    // It has a page of its own, in a group of its own after the others.
    const groups = container.querySelectorAll('.en-group');
    expect(
      Array.from(
        groups[groups.length - 1].querySelectorAll('.en-row-label'),
        (name) => name.textContent,
      ),
    ).toEqual(['propertiesPanel.sectionCss']);
  });

  it('chooses the image from its row: no page leads to the chooser', () => {
    select([SPRITE]);
    const imageRow = () =>
      container.querySelector<HTMLButtonElement>(
        '.en-row-hit[aria-label="sidebar.addSprite"]',
      )!;
    expect(imageRow().getAttribute('aria-haspopup')).toBe('dialog');
    act(() => imageRow().click());
    expect(imageRow().getAttribute('aria-expanded')).toBe('true');
    // Still on the element's page, with the chooser open beside it.
    expect(title()).toBe('sidebar.addSprite 1');
    expect(back().textContent).toBe('sidebar.elements');
    expect(document.body.textContent).toContain('sprite.chooseImage');
    act(() => imageRow().click());
    expect(document.body.textContent).not.toContain('sprite.chooseImage');
  });

  it("edits the image's box on its page: a number typed in a row is saved", () => {
    select([SPRITE]);
    const field = container.querySelector<HTMLInputElement>(
      'input[aria-label="propertiesPanel.size W"]',
    )!;
    expect(field.closest('.en-row')!.querySelector('.en-row-hit')).toBeNull();
    act(() => {
      field.focus();
      Object.getOwnPropertyDescriptor(
        HTMLInputElement.prototype,
        'value',
      )!.set!.call(field, '150');
      field.dispatchEvent(new Event('input', { bubbles: true }));
    });
    expect(useSpriteStore.getState().positions.hand[0].width).toBe(150);
    expect(title()).toBe('sidebar.addSprite 1');

    act(() =>
      container
        .querySelector<HTMLButtonElement>(
          '[role="switch"][aria-label="sprite.visible"]',
        )!
        .click(),
    );
    expect(useSpriteStore.getState().positions.hand[0].hidden).toBe(true);
  });

  it('opens a look from the page of the image and returns to it: no page lies between', () => {
    select([SPRITE]);
    press('sprite.lookNumber 2');
    expect(title()).toBe('sprite.lookNumber 2');
    expect(back().textContent).toBe('sidebar.addSprite 1');
    act(() => back().click());
    expect(title()).toBe('sidebar.addSprite 1');

    press('sprite.rest');
    expect(title()).toBe('sprite.rest');
    expect(back().textContent).toBe('sidebar.addSprite 1');
    act(() => back().click());
    expect(title()).toBe('sidebar.addSprite 1');
  });

  it('adds a look that waits for a key: on screen, opened, and not saved', () => {
    select([SPRITE]);
    const add = container.querySelector<HTMLButtonElement>(
      '.en-row-hit[aria-label="sprite.addLook"]',
    )!;
    // It runs an action: it is not a chooser and has no page of its own.
    expect(add.getAttribute('aria-haspopup')).toBeNull();
    act(() => add.click());

    const poses = sprite().poses;
    expect(poses).toHaveLength(3);
    expect(poses[2].triggers).toEqual([]);
    expect(title()).toBe('sprite.lookNumber 3');
    expect(commitChanges).not.toHaveBeenCalled();

    act(() => back().click());
    expect(rows().find(([label]) => label === 'sprite.lookNumber 3')).toEqual([
      'sprite.lookNumber 3',
      'sprite.needsKey',
    ]);
  });

  it('removes a look from its page and returns to the page of the image', () => {
    select([SPRITE]);
    press('sprite.lookNumber 1');
    press('sprite.removeLook');
    expect(title()).toBe('sidebar.addSprite 1');
    expect(sprite().poses.map((pose) => pose.poseId)).toEqual(['new']);
  });

  it('chooses what a press does in the tray: each choice is a tile that shows it', () => {
    select([SPRITE]);
    const reactionRow = () =>
      container.querySelector<HTMLButtonElement>(
        '.en-row-hit[aria-label="sprite.reaction"]',
      )!;
    expect(reactionRow().getAttribute('aria-haspopup')).toBe('dialog');
    act(() => reactionRow().click());
    expect(title()).toBe('sidebar.addSprite 1');
    expect(tiles().map(tileState)).toEqual([
      ['sprite.whileHeld', 'true'],
      ['sprite.onPress', 'false'],
    ]);
    // Each tile carries a picture of what happens, one per way of reacting.
    expect(
      tiles().map((tile) =>
        tile.querySelector('.en-react')?.getAttribute('data-mode'),
      ),
    ).toEqual(['whileHeld', 'onPress']);

    act(() => tiles()[1].click());
    expect(sprite().activation).toBe('onPress');
    expect(commitChanges).toHaveBeenCalledTimes(1);
    expect(tiles().map(tileState)).toEqual([
      ['sprite.whileHeld', 'false'],
      ['sprite.onPress', 'true'],
    ]);
    expect(rows().find(([label]) => label === 'sprite.reaction')?.[1]).toBe(
      'sprite.onPress',
    );
  });

  it('keeps the time that does not apply in place, greyed out', () => {
    select([SPRITE]);
    const off = () =>
      ['sprite.changeTime', 'sprite.returnTime'].map((label) => {
        const row = Array.from(container.querySelectorAll('.en-row')).find(
          (item) => item.querySelector('.en-row-label')?.textContent === label,
        )!;
        return [label, row.hasAttribute('inert')];
      });
    expect(off()).toEqual([
      ['sprite.changeTime', false],
      ['sprite.returnTime', true],
    ]);

    setSprite({ activation: 'onPress' });
    expect(off()).toEqual([
      ['sprite.changeTime', true],
      ['sprite.returnTime', false],
    ]);
  });

  it('reads a time in seconds and saves a new one once, when the slider is let go', async () => {
    select([SPRITE]);
    const slider = container.querySelector<HTMLElement>(
      '[role="slider"][aria-label="sprite.changeTime"]',
    )!;
    // 90 thousandths of a second, as the user reads them.
    expect(slider.getAttribute('aria-valuetext')).toBe('sprite.seconds 0.09');
    expect(slider.parentElement!.textContent).toBe('sprite.seconds 0.09');

    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      top: 0,
      right: 110,
      bottom: 4,
      width: 110,
      height: 4,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    const drag = (type: string, clientX: number) =>
      act(() => {
        slider.dispatchEvent(
          new MouseEvent(type, { bubbles: true, clientX, button: 0 }),
        );
      });
    drag('pointerdown', 20);
    drag('pointermove', 40);
    drag('pointermove', 60);
    expect(slider.getAttribute('aria-valuetext')).toBe('sprite.seconds 0.3');
    // Nothing is written while the thumb is held.
    expect(sprite().transitionMs).toBe(90);
    expect(commitChanges).not.toHaveBeenCalled();

    drag('pointerup', 60);
    await act(async () => {});
    expect(sprite().transitionMs).toBe(300);
    expect(commitChanges).toHaveBeenCalledTimes(1);
    vi.restoreAllMocks();
  });

  it('chooses how the change feels by name; a curve is never typed', () => {
    select([SPRITE]);
    const feelRow = () =>
      container.querySelector<HTMLButtonElement>(
        '.en-row-hit[aria-label="sprite.feel"]',
      )!;
    act(() => feelRow().click());
    expect(tiles().map(tileState)).toEqual([
      ['sprite.feelSmooth', 'true'],
      ['sprite.feelQuickStart', 'false'],
      ['sprite.feelSlowStart', 'false'],
      ['sprite.feelSteady', 'false'],
      ['sprite.feelBouncy', 'false'],
    ]);
    // Each tile moves with the curve it stands for.
    expect(
      tiles().map((tile) =>
        tile
          .querySelector<HTMLElement>('.en-feel')!
          .style.getPropertyValue('--en-feel-easing'),
      ),
    ).toEqual(SPRITE_FEELS.map((feel) => feel.easing));
    // The chooser has nothing to type into.
    expect(tiles()[0].closest('.en-tiles')!.querySelector('input')).toBeNull();

    act(() => tiles()[4].click());
    expect(sprite().transitionEasing).toBe('cubic-bezier(0.34, 1.56, 0.64, 1)');
    expect(rows().find(([label]) => label === 'sprite.feel')?.[1]).toBe(
      'sprite.feelBouncy',
    );
  });

  it('keeps a stored curve that is none of the feels, as one more choice that is already chosen', () => {
    setSprite({ transitionEasing: 'steps(4, end)' });
    select([SPRITE]);
    expect(rows().find(([label]) => label === 'sprite.feel')?.[1]).toBe(
      'sprite.feelCustom',
    );
    press('sprite.feel');
    expect(tiles().map(tileState).at(-1)).toEqual([
      'sprite.feelCustom',
      'true',
    ]);
    expect(tiles()).toHaveLength(SPRITE_FEELS.length + 1);
    const custom = tiles().at(-1)!;
    expect(custom.getAttribute('aria-disabled')).toBe('true');

    // Pressing it changes nothing: the curve is kept as it is.
    act(() => custom.click());
    expect(sprite().transitionEasing).toBe('steps(4, end)');
    expect(commitChanges).not.toHaveBeenCalled();

    // Choosing a feel replaces it, and it is no longer offered.
    act(() => tiles()[3].click());
    expect(sprite().transitionEasing).toBe('linear');
    expect(tiles().map(tileState)).toEqual([
      ['sprite.feelSmooth', 'false'],
      ['sprite.feelQuickStart', 'false'],
      ['sprite.feelSlowStart', 'false'],
      ['sprite.feelSteady', 'true'],
      ['sprite.feelBouncy', 'false'],
    ]);
  });

  it('has no name to edit on the page of the image: names belong to the list', () => {
    select([SPRITE]);
    expect(container.querySelector('.en-page-title-row')).toBeNull();
    expect(container.querySelector('.en-page-head input')).toBeNull();
    expect(
      Array.from(
        container.querySelectorAll('.en-page-actions button'),
        (button) => button.textContent,
      ),
    ).toEqual(['tooltip.delete']);
  });

  it('shows the list, not a page, for several images or an image with other elements', () => {
    select([SPRITE, { type: 'sprite', id: 'sprite-1', index: 1 }]);
    expect(container.querySelector('[data-element-list]')?.textContent).toBe(
      '0',
    );
    select([SPRITE, { type: 'key', id: 'key-0', index: 0 }]);
    expect(container.querySelector('[data-element-list]')?.textContent).toBe(
      '0',
    );
    expect(container.querySelector('.en-row')).toBeNull();
  });
});
