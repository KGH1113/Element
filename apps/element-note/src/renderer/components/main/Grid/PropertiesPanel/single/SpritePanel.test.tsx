import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorChanges } from '@src/types/editor';
import type { KeyPosition } from '@src/types/key/keys';
import type { ReactiveSpritePosition } from '@src/types/key/sprites';
import { useSpriteTryOutStore } from '@src/renderer/editor/runtime/spriteTryOut';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useSpriteStore } from '@stores/data/useSpriteStore';
import {
  makeSpritePose,
  makeSpritePosition,
} from '@utils/sprite/spriteFixtures';
import { InspectorPageContext } from '../inspectorPageContext';
import SpritePanel, { SpriteImageChooser } from './SpritePanel';
import { SPRITE_PARTS, spriteLookPart } from './spriteParts';

const { probeImageSize } = vi.hoisted(() => ({ probeImageSize: vi.fn() }));
vi.mock('@utils/media/assetProbe', () => ({ probeImageSize }));
// The chooser opens in the tray; here it only hands a file over or clears,
// and shows what it is told about a file that was not taken.
vi.mock('@components/main/Modal/content/pickers/ImagePicker', () => ({
  default: ({
    onIdleImageChange,
    onIdleImageReset,
    notice,
  }: {
    onIdleImageChange: (path: string) => void;
    onIdleImageReset: () => void;
    notice?: string;
  }) => (
    <>
      <button onClick={() => onIdleImageChange('/images/cat.png')}>
        pick file
      </button>
      <button onClick={onIdleImageReset}>clear file</button>
      <p data-notice>{notice}</p>
    </>
  ),
}));

describe('the pages of an image in the sidebar', () => {
  let root: Root;
  let container: HTMLDivElement;
  const originalApi = window.api;
  const commitChanges = vi.fn(async (_changes: EditorChanges) => undefined);
  const onLeavePage = vi.fn();
  const t = (key: string, options?: Record<string, unknown>) =>
    options?.number ? `${key} ${options.number}` : key;

  const stored = () => useSpriteStore.getState().positions.hand[0];
  const saved = () =>
    commitChanges.mock.calls.map(
      ([changes]) => changes.spritePositions!.hand[0],
    );
  const button = (text: string) => {
    const found = Array.from(container.querySelectorAll('button')).find(
      (element) =>
        element.textContent === text ||
        element.getAttribute('aria-label') === text,
    );
    if (!found) throw new Error(`Missing button: ${text}`);
    return found;
  };
  const hints = () =>
    Array.from(container.querySelectorAll('.en-hint')).map(
      (hint) => hint.textContent,
    );
  const rowLabels = () =>
    Array.from(container.querySelectorAll('.dmn-property-row__label')).map(
      (label) => label.textContent,
    );
  const type = (label: string, text: string) => {
    const field = container.querySelector<HTMLInputElement>(
      `input[aria-label="${label}"]`,
    )!;
    act(() => {
      field.focus();
      Object.getOwnPropertyDescriptor(
        HTMLInputElement.prototype,
        'value',
      )!.set!.call(field, text);
      field.dispatchEvent(new Event('input', { bubbles: true }));
    });
  };
  const pointer = (target: Element, kind: string, clientX = 0, clientY = 0) =>
    act(() => {
      target.dispatchEvent(
        new MouseEvent(kind, { bubbles: true, clientX, clientY, button: 0 }),
      );
    });

  /** Shows a page of the sprite that is in the store. */
  const open = (page: string, useCustomCSS = false) =>
    act(() =>
      root.render(
        <InspectorPageContext.Provider
          value={{ activePage: page, groups: null }}
        >
          <SpritePanel
            mode="hand"
            sprite={stored()}
            useCustomCSS={useCustomCSS}
            onLeavePage={onLeavePage}
            t={t}
          />
        </InspectorPageContext.Provider>,
      ),
    );
  const setSprite = (overrides: Partial<ReactiveSpritePosition>) =>
    useSpriteStore.setState({
      positions: {
        hand: [makeSpritePosition({ id: 'cat', zIndex: 0, ...overrides })],
      },
      isLocalUpdateInProgress: false,
    });

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    commitChanges.mockReset();
    commitChanges.mockResolvedValue(undefined);
    onLeavePage.mockReset();
    probeImageSize.mockReset();
    window.api = {
      editor: { commitChanges },
    } as unknown as typeof window.api;
    useKeyStore.setState({
      selectedKeyType: 'hand',
      keyMappings: { hand: ['A', 'S'] },
      positions: {
        hand: [
          { id: 'key-a', dx: 0, dy: 0, width: 60, height: 60 },
          { id: 'key-s', dx: 70, dy: 0, width: 60, height: 60 },
        ] as KeyPosition[],
      },
    });
    setSprite({});
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    window.api = originalApi;
    vi.restoreAllMocks();
  });

  it('shows the resting look on a stage, with its numbers and its turning centre', () => {
    open(SPRITE_PARTS.rest);
    expect(container.querySelector('.en-stage')).not.toBeNull();
    expect(rowLabels()).toEqual([
      'sprite.move',
      'sprite.rotation',
      'sprite.size',
    ]);
    // The resting look is not tried out: it is what is shown anyway.
    expect(container.querySelector('.en-try')).toBeNull();
    expect(container.querySelector('.en-pivot-pad')).not.toBeNull();

    type('sprite.rotation', '30');
    expect(stored().idleTransform.rotation).toBe(30);
    expect(saved()[0].idleTransform.rotation).toBe(30);
  });

  it('saves a look once it has a key', () => {
    setSprite({ poses: [makeSpritePose({ poseId: 'p1', triggers: [] })] });
    open(spriteLookPart('p1'));
    expect(hints()).toContain('sprite.needsKeyHint');

    act(() => button('S').click());
    expect(stored().poses[0].triggers).toEqual(['key-s']);
    expect(saved()[0].poses).toEqual([
      expect.objectContaining({ poseId: 'p1', triggers: ['key-s'] }),
    ]);
    open(spriteLookPart('p1'));
    expect(button('S').getAttribute('aria-pressed')).toBe('true');
    // Nothing is wrong: the line says how keys are read.
    expect(hints()).toEqual(['sprite.lookKeysHint']);
  });

  it('keeps keys another look already has picked on the page, unsaved, and says so', () => {
    setSprite({
      poses: [
        makeSpritePose({ poseId: 'p1', triggers: ['key-a'] }),
        makeSpritePose({ poseId: 'p2', triggers: ['key-a', 'key-s'] }),
      ],
    });
    open(spriteLookPart('p2'));
    act(() => button('S').click());

    expect(button('S').getAttribute('aria-pressed')).toBe('false');
    expect(button('A').getAttribute('aria-pressed')).toBe('true');
    expect(hints()).toContain('sprite.sameKeysHint');
    // Both looks are saved as they were.
    expect(stored().poses[1].triggers).toEqual(['key-a', 'key-s']);
    expect(commitChanges).not.toHaveBeenCalled();

    // Picking a set of its own again saves it.
    act(() => button('A').click());
    expect(stored().poses[1].triggers).toEqual([]);
    open(spriteLookPart('p2'));
    expect(hints()).toContain('sprite.needsKeyHint');
    act(() => button('S').click());
    expect(stored().poses[1].triggers).toEqual(['key-s']);
    open(spriteLookPart('p2'));
    expect(hints()).not.toContain('sprite.sameKeysHint');
    expect(hints()).not.toContain('sprite.needsKeyHint');
  });

  it('shows a look that still needs a key, and edits it without saving', () => {
    setSprite({ poses: [makeSpritePose({ poseId: 'p1', triggers: [] })] });
    open(spriteLookPart('p1'));
    expect(container.querySelector('.en-stage')).not.toBeNull();
    expect(container.querySelector('.en-try')).not.toBeNull();

    type('sprite.rotation', '45');
    expect(stored().poses[0].transform.rotation).toBe(45);
    // It is on screen and waits: nothing that is saved has changed.
    expect(commitChanges).not.toHaveBeenCalled();
  });

  it('removes a look and returns to the page of the image', () => {
    setSprite({
      poses: [makeSpritePose({ poseId: 'p1', triggers: ['key-a'] })],
    });
    open(spriteLookPart('p1'));
    act(() => button('sprite.removeLook').click());
    expect(stored().poses).toEqual([]);
    expect(saved()[0].poses).toEqual([]);
    expect(onLeavePage).toHaveBeenCalledTimes(1);
  });

  it('shifts a look by dragging its picture, and saves the drag once', async () => {
    setSprite({
      poses: [makeSpritePose({ poseId: 'p1', triggers: ['key-a'] })],
    });
    open(spriteLookPart('p1'));
    const stage = container.querySelector('.en-stage')!;

    pointer(stage, 'pointerdown', 100, 100);
    pointer(stage, 'pointermove', 120, 100);
    open(spriteLookPart('p1'));
    pointer(stage, 'pointermove', 140, 90);
    open(spriteLookPart('p1'));
    const during = stored().poses[0].transform;
    expect(during.x).toBeGreaterThan(40);
    expect(during.y).toBeLessThan(-10);
    expect(Number.isInteger(during.x) && Number.isInteger(during.y)).toBe(true);
    // The numbers follow the picture while it is held.
    expect(
      container.querySelector<HTMLInputElement>(
        'input[aria-label="sprite.move X"]',
      )!.value,
    ).toBe(String(during.x));
    expect(commitChanges).not.toHaveBeenCalled();

    pointer(stage, 'pointerup', 140, 90);
    await act(async () => {});
    expect(commitChanges).toHaveBeenCalledTimes(1);
    expect(saved()[0].poses[0].transform).toEqual(during);

    // A press that does not move the picture saves nothing.
    pointer(stage, 'pointerdown', 50, 50);
    pointer(stage, 'pointerup', 50, 50);
    await act(async () => {});
    expect(commitChanges).toHaveBeenCalledTimes(1);
  });

  it('tries a look out while its button is held: set, cleared, and nothing written', () => {
    setSprite({
      poses: [makeSpritePose({ poseId: 'p1', triggers: ['key-a'] })],
    });
    const before = useSpriteStore.getState().positions;
    const tryOut = () => useSpriteTryOutStore.getState();
    open(spriteLookPart('p1'));
    expect(tryOut()).toEqual({
      target: { spriteId: 'cat', poseId: 'p1' },
      held: false,
    });
    const hold = container.querySelector<HTMLButtonElement>('.en-try')!;
    expect(hold.textContent).toBe('sprite.tryIt');

    pointer(hold, 'pointerdown');
    expect(tryOut().held).toBe(true);
    expect(hold.getAttribute('aria-pressed')).toBe('true');
    pointer(hold, 'pointerup');
    expect(tryOut().held).toBe(false);
    expect(hold.getAttribute('aria-pressed')).toBe('false');

    // Leaving the button, or the window, lets go as well.
    pointer(hold, 'pointerdown');
    act(() => {
      hold.focus();
      hold.blur();
    });
    expect(tryOut().held).toBe(false);
    pointer(hold, 'pointerdown');
    act(() => {
      window.dispatchEvent(new Event('blur'));
    });
    expect(tryOut().held).toBe(false);

    // A key on the button holds for as long as the key is down.
    act(() => {
      hold.dispatchEvent(
        new KeyboardEvent('keydown', { bubbles: true, key: ' ' }),
      );
    });
    expect(tryOut().held).toBe(true);
    act(() => {
      hold.dispatchEvent(
        new KeyboardEvent('keyup', { bubbles: true, key: ' ' }),
      );
    });
    expect(tryOut().held).toBe(false);

    // Leaving the page while it is held ends it.
    pointer(hold, 'pointerdown');
    open(SPRITE_PARTS.rest);
    expect(tryOut()).toEqual({ target: null, held: false });

    expect(useSpriteStore.getState().positions).toBe(before);
    expect(commitChanges).not.toHaveBeenCalled();
  });

  it('places the turning centre on the picture and keeps the picture where it is', async () => {
    setSprite({ width: 200, height: 100 });
    open(SPRITE_PARTS.rest);
    const name = () => container.querySelector('.en-pivot-name')?.textContent;
    expect(name()).toBe('sprite.centerMiddle');
    expect(button('sprite.centerMiddle').getAttribute('aria-pressed')).toBe(
      'true',
    );

    // One of the nine usual places, chosen with a key on its button.
    act(() => button('sprite.centerTopLeft').click());
    await act(async () => {});
    expect(stored().pivot).toEqual({ x: 0, y: 0 });
    // Unturned and unsized, the picture needs no shift to stay in place.
    expect(stored().idleTransform).toMatchObject({ x: 0, y: 0 });
    expect(commitChanges).toHaveBeenCalledTimes(1);
    open(SPRITE_PARTS.rest);
    expect(name()).toBe('sprite.centerTopLeft');

    // Anywhere else, by pressing the picture: shown while held, saved once.
    const pad = container.querySelector<HTMLElement>('.en-pivot-pad')!;
    vi.spyOn(pad, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      top: 0,
      right: 200,
      bottom: 100,
      width: 200,
      height: 100,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    pointer(pad, 'pointerdown', 60, 80);
    expect(stored().pivot).toEqual({ x: 0.3, y: 0.8 });
    pointer(pad, 'pointermove', 50, 70);
    expect(stored().pivot).toEqual({ x: 0.25, y: 0.7 });
    expect(commitChanges).toHaveBeenCalledTimes(1);
    pointer(pad, 'pointerup', 50, 70);
    await act(async () => {});
    expect(commitChanges).toHaveBeenCalledTimes(2);
    expect(saved()[1].pivot).toEqual({ x: 0.25, y: 0.7 });
    open(SPRITE_PARTS.rest);
    expect(name()).toBe('sprite.centerCustom');

    // A press near a marked place lands on it exactly.
    pointer(pad, 'pointerdown', 197, 4);
    pointer(pad, 'pointerup', 197, 4);
    expect(stored().pivot).toEqual({ x: 1, y: 0 });
  });

  it('turns a look around the resting centre until it is given its own', () => {
    setSprite({
      pivot: { x: 0.5, y: 1 },
      poses: [makeSpritePose({ poseId: 'p1', triggers: ['key-a'] })],
    });
    open(spriteLookPart('p1'));
    const same = () =>
      container.querySelector<HTMLButtonElement>(
        '[role="switch"][aria-label="sprite.sameCenter"]',
      )!;
    const padOff = () =>
      container.querySelector('.en-pivot')!.closest('.en-dimmed')!;
    expect(same().getAttribute('aria-checked')).toBe('true');
    expect(padOff().hasAttribute('inert')).toBe(true);
    // The pad is in place and shows the centre that is in use.
    expect(container.querySelector('.en-pivot-name')?.textContent).toBe(
      'sprite.centerBottom',
    );

    act(() => same().click());
    expect(stored().poses[0].pivot).toEqual({ x: 0.5, y: 1 });
    open(spriteLookPart('p1'));
    expect(same().getAttribute('aria-checked')).toBe('false');
    expect(padOff().hasAttribute('inert')).toBe(false);

    act(() => button('sprite.centerMiddle').click());
    expect(stored().poses[0].pivot).toEqual({ x: 0.5, y: 0.5 });
    // The image's own centre is not the look's to change.
    expect(stored().pivot).toEqual({ x: 0.5, y: 1 });

    open(spriteLookPart('p1'));
    act(() => same().click());
    expect(stored().poses[0].pivot).toBeNull();
  });

  it('fits the box to a chosen image, and keeps the old image when a file cannot be read', async () => {
    setSprite({ width: 200, height: 200 });
    /** The chooser the image's row opens, for the sprite in the store. */
    const openChooser = () =>
      act(() =>
        root.render(
          <SpriteImageChooser
            mode="hand"
            sprite={stored()}
            trigger={{ current: null }}
            onClose={vi.fn()}
            t={t}
          />,
        ),
      );
    const notice = () => container.querySelector('[data-notice]')?.textContent;
    openChooser();
    expect(notice()).toBe('');

    probeImageSize.mockResolvedValueOnce(null);
    await act(async () => button('pick file').click());
    expect(stored().baseImage).toBeNull();
    expect(notice()).toBe('sprite.imageError');
    expect(commitChanges).not.toHaveBeenCalled();

    probeImageSize.mockResolvedValueOnce({ width: 400, height: 100 });
    await act(async () => button('pick file').click());
    expect(stored()).toMatchObject({
      baseImage: '/images/cat.png',
      width: 200,
      height: 50,
      referenceNaturalSize: {
        source: '/images/cat.png',
        width: 400,
        height: 100,
      },
    });
    expect(notice()).toBe('');
    expect(commitChanges).toHaveBeenCalledTimes(1);

    openChooser();
    act(() => button('clear file').click());
    expect(stored().baseImage).toBeNull();
    expect(stored().referenceNaturalSize).toMatchObject({ source: null });
  });

  it('gives a look a picture of its own from its row, and takes it away again', async () => {
    setSprite({
      baseImage: '/images/base.png',
      poses: [makeSpritePose({ poseId: 'p1', triggers: ['key-a'] })],
    });
    open(spriteLookPart('p1'));
    const pick = () => button('sidebar.addSprite');
    // Until it has one, it is drawn with the image's own picture.
    expect(pick().textContent).toBe('sprite.sameImage');
    expect(pick().getAttribute('aria-haspopup')).toBe('dialog');
    expect(container.textContent).not.toContain('pick file');

    act(() => pick().click());
    expect(pick().getAttribute('aria-expanded')).toBe('true');
    probeImageSize.mockResolvedValueOnce({ width: 64, height: 64 });
    await act(async () => button('pick file').click());
    expect(stored().poses[0]).toMatchObject({
      imageOverride: '/images/cat.png',
      imageOverrideMetrics: { source: '/images/cat.png', width: 64 },
    });
    expect(stored().baseImage).toBe('/images/base.png');
    open(spriteLookPart('p1'));
    expect(pick().textContent).toBe('cat.png');

    act(() => button('clear file').click());
    expect(stored().poses[0]).toMatchObject({
      imageOverride: null,
      imageOverrideMetrics: null,
    });
    open(spriteLookPart('p1'));
    expect(pick().textContent).toBe('sprite.sameImage');
  });

  it('has no page for what is chosen or set in a row of the image', () => {
    for (const part of [
      SPRITE_PARTS.image,
      SPRITE_PARTS.reaction,
      SPRITE_PARTS.feel,
      SPRITE_PARTS.changeTime,
      SPRITE_PARTS.returnTime,
      SPRITE_PARTS.addLook,
    ]) {
      open(part);
      expect(container.textContent).toBe('');
    }
  });

  it('shows the CSS page only while custom CSS is in use', () => {
    open(SPRITE_PARTS.css);
    expect(container.textContent).toBe('');
    open(SPRITE_PARTS.css, true);
    expect(container.textContent).toContain('propertiesPanel.className');
  });
});
