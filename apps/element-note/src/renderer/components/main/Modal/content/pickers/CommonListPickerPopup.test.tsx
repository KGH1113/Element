import { act, createRef } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import CommonListPickerPopup from './CommonListPickerPopup';

type Font = { id: string; name: string };
const FONTS: Font[] = [
  { id: 'a', name: 'Pretendard' },
  { id: 'b', name: 'SUIT' },
];

describe('list chooser', () => {
  let root: Root;
  let container: HTMLDivElement;
  const onItemChoose = vi.fn();
  const onFilterChange = vi.fn();
  const onAdd = vi.fn();
  const trigger = createRef<HTMLButtonElement>();

  const draw = (items: Font[]) =>
    act(() =>
      root.render(
        <>
          <button ref={trigger} type="button">
            Font
          </button>
          <CommonListPickerPopup<Font>
            open
            referenceRef={trigger}
            onClose={() => undefined}
            searchQuery=""
            onSearchQueryChange={() => undefined}
            searchPlaceholder="Search"
            filterOptions={[
              { value: 'all', label: 'All' },
              { value: 'web', label: 'Web' },
            ]}
            filterValue="all"
            onFilterChange={onFilterChange}
            items={items}
            getItemKey={(font) => font.id}
            getItemLabel={(font) => font.name}
            getItemLabelStyle={(font) => ({ fontFamily: font.name })}
            isItemSelected={(font) => font.name === 'SUIT'}
            onItemChoose={onItemChoose}
            emptyText="No fonts"
            onAdd={onAdd}
            addLabel="Add a font"
          />
        </>,
      ),
    );
  const rows = () =>
    Array.from(document.body.querySelectorAll<HTMLElement>('.en-lc-row'));

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
    [onItemChoose, onFilterChange, onAdd].forEach((fn) => fn.mockReset());
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('draws every item as a row of one kind, the chosen one marked', () => {
    draw(FONTS);
    expect(
      rows().map((row) => [
        row.querySelector('.en-lc-row-label')?.textContent,
        row.querySelector('[role=option]')?.getAttribute('aria-selected'),
        !!row.querySelector('.en-lc-row-check'),
      ]),
    ).toEqual([
      ['Pretendard', 'false', false],
      ['SUIT', 'true', true],
    ]);
    // A font's name is written in that font.
    expect(
      rows()[0].querySelector<HTMLElement>('.en-lc-row-label')!.style
        .fontFamily,
    ).toBe('Pretendard');
  });

  it('chooses a row, a kind, or adds one', () => {
    draw(FONTS);
    act(() =>
      rows()[0].querySelector<HTMLButtonElement>('[role=option]')!.click(),
    );
    expect(onItemChoose).toHaveBeenCalledWith(FONTS[0]);
    const kinds = Array.from(
      document.body.querySelectorAll<HTMLButtonElement>('.en-lc-kind'),
    );
    expect(kinds.map((kind) => kind.getAttribute('aria-checked'))).toEqual([
      'true',
      'false',
    ]);
    act(() => kinds[1].click());
    expect(onFilterChange).toHaveBeenCalledWith('web');
    act(() =>
      document.body.querySelector<HTMLButtonElement>('.en-lc-add')!.click(),
    );
    expect(onAdd).toHaveBeenCalled();
  });

  it('says so when nothing is in the list', () => {
    draw([]);
    expect(document.body.querySelector('.en-lc-note')?.textContent).toBe(
      'No fonts',
    );
  });
});
