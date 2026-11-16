import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ElementPages from './ElementPages';
import {
  resolvePartPath,
  type ElementPart,
  type PagePart,
} from './elementParts';
import PropertySection from '@components/main/Grid/PropertiesPanel/PropertySection';
import { FeatureSettings } from '@components/main/Grid/PropertiesPanel/FeatureSettings';

describe('element pages', () => {
  let root: Root;
  let container: HTMLDivElement;
  const t = (key: string) => key;
  const onNavigate = vi.fn();
  const onToggle = vi.fn();
  const onListen = vi.fn();

  const input: ElementPart = {
    kind: 'inline',
    id: 'input',
    label: 'Input',
    control: <button onClick={onListen}>Space</button>,
  };
  const note = (on = true): PagePart => ({
    kind: 'page',
    id: 'note',
    label: 'Raining',
    groups: null,
    toggle: { checked: on, onChange: onToggle },
  });
  const sound: PagePart = {
    kind: 'page',
    id: 'sound',
    label: 'Sound',
    groups: ['input-sound'],
  };
  /** A key: its input in a row, two pages, and its ghost face at the end. */
  const keyParts = (ghostOn = false, noteOn = true): ElementPart[][] => [
    [input],
    [note(noteOn), sound],
    [
      {
        kind: 'page',
        id: 'ghost',
        label: 'Ghost key',
        groups: null,
        toggle: {
          checked: ghostOn,
          onLabel: 'Enter',
          onChange: onToggle,
        },
        parts: [[{ ...input, label: 'Ghost input' }], [note(), sound]],
        hint: 'A second key in the same place.',
      },
    ],
  ];

  const settings = (noteOn = true) => (
    <FeatureSettings enabled={noteOn}>
      <PropertySection id="input-binding" title="Keyboard key">
        <span>binding</span>
      </PropertySection>
      <PropertySection id="input-sound" title="Sound">
        <span>sound</span>
      </PropertySection>
    </FeatureSettings>
  );

  const render = (
    props: Partial<React.ComponentProps<typeof ElementPages>> = {},
    noteOn = true,
  ) =>
    act(() =>
      root.render(
        <ElementPages
          direction="none"
          listLabel="Elements"
          onBackToList={vi.fn()}
          title="Space"
          parts={keyParts()}
          path={[]}
          onNavigate={onNavigate}
          t={t}
          {...props}
        >
          {settings(noteOn)}
        </ElementPages>,
      ),
    );

  // Every part is a row: edited or chosen where it is, or entered.
  const rows = () => Array.from(container.querySelectorAll('.en-row'));
  const row = (label: string) =>
    rows().find(
      (item) => item.querySelector('.en-row-label')?.textContent === label,
    )!;
  const hit = (label: string) => {
    const part = row(label);
    return part.matches('button')
      ? (part as HTMLButtonElement)
      : part.querySelector<HTMLButtonElement>('.en-row-hit')!;
  };
  // What the open page is about: its title, or, on the page of the element
  // itself, which has none, the name the page goes by.
  const title = () =>
    container.querySelector('.en-page-title')?.textContent ??
    container.querySelector('.en-page')?.getAttribute('aria-label');
  const back = () =>
    container.querySelector<HTMLButtonElement>('.en-page-back')!;

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    onNavigate.mockReset();
    onToggle.mockReset();
    onListen.mockReset();
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('lists the parts in groups and opens the page of the row that is pressed', () => {
    render();
    expect(
      Array.from(container.querySelectorAll('.en-group')).map((group) =>
        Array.from(group.querySelectorAll('.en-row-label')).map(
          (label) => label.textContent,
        ),
      ),
    ).toEqual([['Input'], ['Raining', 'Sound'], ['Ghost key']]);
    // Entered or not, every part is one row of the same kind.
    expect(container.querySelectorAll('.en-row')).toHaveLength(4);
    expect(container.querySelector('.en-part-content')).toBeNull();
    act(() => hit('Raining').click());
    expect(onNavigate).toHaveBeenCalledWith(['note'], false);
  });

  it('edits a part with one value in its row: the row holds the control and opens no page', () => {
    render();
    const inputRow = row('Input');
    // Nothing to press but the control itself, and no sign of a page.
    expect(inputRow.querySelector('.en-row-hit')).toBeNull();
    expect(inputRow.querySelector('.en-row-chevron')).toBeNull();
    const control = inputRow.querySelector<HTMLButtonElement>(
      '.en-row-control button',
    )!;
    act(() => control.click());
    expect(onListen).toHaveBeenCalledOnce();
    expect(onNavigate).not.toHaveBeenCalled();

    // A path that names it leads nowhere: it has no page.
    render({ path: ['input'] });
    expect(title()).toBe('Space');
    expect(resolvePartPath(keyParts(), ['input'])).toEqual([]);
  });

  it("says a part's state on its row and keeps the switch on its page", () => {
    render();
    // A row is something to open, or it holds its value: never a switch too.
    expect(container.querySelector('.en-row [role="switch"]')).toBeNull();
    expect(row('Raining').textContent).toContain('sidebar.stateOn');

    render({ path: ['note'] });
    act(() =>
      container
        .querySelector<HTMLButtonElement>('.en-page-trailing [role="switch"]')!
        .click(),
    );
    expect(onToggle).toHaveBeenCalledWith(false);
  });

  it('shows only the groups that belong to the open page', () => {
    render({ path: ['sound'] });
    expect(title()).toBe('Sound');
    expect(back().textContent).toBe('Space');
    expect(container.textContent).toContain('sound');
    expect(container.textContent).not.toContain('binding');
    act(() => back().click());
    expect(onNavigate).toHaveBeenCalledWith([], true);
  });

  it('keeps the settings of a switched-off part in place, greyed out', () => {
    render({ parts: keyParts(false, false), path: ['note'] }, false);
    expect(container.querySelector('.en-page-state')?.textContent).toBe(
      'sidebar.stateOff',
    );
    const dimmed = container.querySelector('.en-part-content .en-dimmed')!;
    expect(dimmed.hasAttribute('inert')).toBe(true);
    expect(dimmed.textContent).toContain('binding');
    expect(dimmed.textContent).toContain('sound');
  });

  it('has no title on the page of the element: the canvas shows what is selected', () => {
    const head = () =>
      Array.from(container.querySelector('.en-page-head')!.children).map(
        (child) => child.className,
      );
    const onDelete = vi.fn();
    render({ onDelete });
    // The way back and Delete, and nothing that names or renames.
    expect(head()).toEqual(['en-page-nav']);
    expect(container.querySelector('.en-page-head .dmn-segmented')).toBeNull();
    expect(
      Array.from(
        container.querySelectorAll('.en-page-actions button'),
        (button) => button.textContent,
      ),
    ).toEqual(['tooltip.delete']);
    expect(container.querySelector('.en-page-head input')).toBeNull();
    // A page below it says what it is about.
    render({ path: ['note'], onDelete });
    expect(head()).toEqual(['en-page-nav', 'en-page-title-row']);
    expect(back().textContent).toBe('Space');
  });

  it('shows a part that is off like any other row: only its value says so', () => {
    render({}, false);
    const off = rows().filter((item) =>
      item.textContent?.includes('sidebar.stateOff'),
    );
    expect(off.length).toBeGreaterThan(0);
    off.forEach((item) => {
      expect(item.hasAttribute('data-dim')).toBe(false);
      expect(item.hasAttribute('data-disabled')).toBe(false);
    });
  });

  it('lists the ghost face as the last row, saying off or its key', () => {
    render();
    expect(rows().at(-1)).toBe(row('Ghost key'));
    expect(row('Ghost key').textContent).toContain('sidebar.stateOff');
    render({ parts: keyParts(true) });
    expect(row('Ghost key').querySelector('.en-row-value')?.textContent).toBe(
      'Enter',
    );
    act(() => hit('Ghost key').click());
    expect(onNavigate).toHaveBeenCalledWith(['ghost'], false);
  });

  it('gives the ghost a page of its own: its switch in the head, its parts under it', () => {
    render({ parts: keyParts(true), path: ['ghost'] });
    expect(title()).toBe('Ghost key');
    expect(back().textContent).toBe('Space');
    expect(
      container.querySelector('.en-page-trailing [role="switch"]'),
    ).not.toBeNull();
    expect(
      rows().map((item) => item.querySelector('.en-row-label')?.textContent),
    ).toEqual(['Ghost input', 'Raining', 'Sound']);
    expect(container.textContent).toContain('A second key in the same place.');

    // A page opened from it returns to it, not to the element.
    act(() => hit('Sound').click());
    expect(onNavigate).toHaveBeenCalledWith(['ghost', 'sound'], false);
    render({ parts: keyParts(true), path: ['ghost', 'sound'] });
    expect(title()).toBe('Sound');
    expect(back().textContent).toBe('Ghost key');
    act(() => back().click());
    expect(onNavigate).toHaveBeenLastCalledWith(['ghost'], true);
  });

  it('greys the ghost out while it is not in use, rows and pages alike', () => {
    render({ path: ['ghost'] });
    expect(hit('Raining').disabled).toBe(true);
    expect(row('Ghost input').hasAttribute('inert')).toBe(true);

    render({ path: ['ghost', 'note'] });
    expect(
      container.querySelector<HTMLButtonElement>(
        '.en-page-trailing [role="switch"]',
      )!.disabled,
    ).toBe(true);
    expect(
      container.querySelector('.en-part-content')!.closest('.en-dimmed[inert]'),
    ).not.toBeNull();
  });

  it('opens a page from another page and returns to that page', () => {
    const withPose: ElementPart[][] = [
      [
        { kind: 'page', id: 'poses', label: 'Poses', groups: null },
        {
          kind: 'page',
          id: 'pose:1',
          label: 'Pose 1',
          groups: null,
          listed: false,
        },
      ],
    ];
    // A page opened from another page has no row of its own.
    render({ parts: withPose });
    expect(
      rows().map((item) => item.querySelector('.en-row-label')?.textContent),
    ).toEqual(['Poses']);

    render({ parts: withPose, path: ['poses', 'pose:1'] });
    expect(title()).toBe('Pose 1');
    expect(back().textContent).toBe('Poses');
    act(() => back().click());
    expect(onNavigate).toHaveBeenCalledWith(['poses'], true);
  });

  it('opens a chooser from its row without leaving the page', () => {
    const chooser = vi.fn(({ close }: { close: () => void }) => (
      <button data-chooser onClick={close}>
        pick
      </button>
    ));
    const parts: ElementPart[][] = [
      [{ kind: 'chooser', id: 'image', label: 'Image', chooser }],
      [sound],
    ];
    render({ parts });
    expect(container.querySelector('[data-chooser]')).toBeNull();
    expect(hit('Image').getAttribute('aria-haspopup')).toBe('dialog');
    expect(hit('Image').getAttribute('aria-expanded')).toBe('false');

    act(() => hit('Image').click());
    expect(container.querySelector('[data-chooser]')).not.toBeNull();
    expect(hit('Image').getAttribute('aria-expanded')).toBe('true');
    // The chooser is told which row it belongs to.
    expect(
      (
        chooser.mock.calls[0][0] as unknown as {
          trigger: { current: HTMLElement };
        }
      ).trigger.current,
    ).toBe(hit('Image'));
    expect(onNavigate).not.toHaveBeenCalled();
    expect(title()).toBe('Space');

    act(() =>
      container.querySelector<HTMLButtonElement>('[data-chooser]')!.click(),
    );
    expect(container.querySelector('[data-chooser]')).toBeNull();

    // A chooser left open closes when another element is selected.
    act(() => hit('Image').click());
    render({ parts, identity: 'other' });
    expect(container.querySelector('[data-chooser]')).toBeNull();
  });

  it("shows settings that have no page beside them on the element's page", () => {
    const details: PagePart = {
      kind: 'page',
      id: 'details',
      label: 'Settings',
      groups: ['input-sound'],
      inPlace: true,
    };
    render({ parts: [[input], [details]] });
    expect(title()).toBe('Space');
    expect(back().textContent).toBe('Elements');
    // The value in its row, the settings under it, and no row leading to them.
    expect(
      rows().map((item) => item.querySelector('.en-row-label')?.textContent),
    ).toEqual(['Input']);
    expect(container.textContent).toContain('sound');
    expect(container.textContent).not.toContain('binding');
  });

  it('runs an action from its row: no page and no chooser is opened for it', () => {
    const run = vi.fn();
    const add = (disabled = false): ElementPart => ({
      kind: 'action',
      id: 'add',
      label: 'Add a look',
      value: 'For a key press',
      disabled,
      run,
    });
    render({ parts: [[input], [add()]] });
    const row = () => rows()[1];
    expect(row().querySelector('.en-row-value')?.textContent).toBe(
      'For a key press',
    );
    // It says neither that it leads to a page nor that it opens a chooser.
    expect(row().querySelector('.en-row-chevron')).toBeNull();
    expect(hit('Add a look').getAttribute('aria-haspopup')).toBeNull();

    act(() => hit('Add a look').click());
    expect(run).toHaveBeenCalledTimes(1);
    expect(onNavigate).not.toHaveBeenCalled();
    expect(title()).toBe('Space');

    // An action that does not apply stays in place and cannot be run.
    render({ parts: [[input], [add(true)]] });
    expect(row().getAttribute('data-disabled')).toBe('true');
    expect(hit('Add a look').disabled).toBe(true);
  });

  it('keeps a row that does not apply right now in place, greyed out', () => {
    render({ parts: [[{ ...input, disabled: true }], [note(), sound]] });
    const first = rows()[0];
    expect(first.querySelector('.en-row-label')?.textContent).toBe('Input');
    expect(first.getAttribute('data-disabled')).toBe('true');
    expect(first.hasAttribute('inert')).toBe(true);
    // The rows beside it are as they were.
    expect(rows()[1].getAttribute('data-disabled')).toBeNull();
  });
});
