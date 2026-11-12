import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { InspectorPageContext } from './inspectorPageContext';
import { NumberInput } from './PropertyInputs';

describe('a number that can be dragged', () => {
  let root: Root;
  let container: HTMLDivElement;
  const onChange = vi.fn();

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    vi.useFakeTimers({
      toFake: ['requestAnimationFrame', 'cancelAnimationFrame'],
    });
    onChange.mockReset();
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.useRealTimers();
  });

  const onPage = (field: React.ReactNode) =>
    act(() =>
      root.render(
        <InspectorPageContext.Provider
          value={{ activePage: 'style', groups: null }}
        >
          {field}
        </InspectorPageContext.Provider>,
      ),
    );
  const slider = () =>
    container.querySelector<HTMLInputElement>('input[type="range"]');
  const drag = (value: string) =>
    act(() => {
      const input = slider()!;
      Object.getOwnPropertyDescriptor(
        HTMLInputElement.prototype,
        'value',
      )!.set!.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });

  it('is a slider as well when its range is small, on a sidebar page', () => {
    onPage(
      <NumberInput
        value={3}
        onChange={onChange}
        min={0}
        max={20}
        suffix="px"
        ariaLabel="Border width"
        visual="stroke"
      />,
    );
    expect(slider()).not.toBeNull();
    expect(slider()!.getAttribute('aria-label')).toBe('Border width');
    expect([slider()!.min, slider()!.max, slider()!.value]).toEqual([
      '0',
      '20',
      '3',
    ]);
    // The picture of the value is drawn beside it.
    expect(container.querySelector('.en-range-glyph svg')).not.toBeNull();
    // The field that says the number stays.
    expect(container.querySelector('.dmn-number-field')).not.toBeNull();
  });

  it('stays a field alone when the range is wide, when it names its number, or off a sidebar page', () => {
    onPage(<NumberInput value={10} onChange={onChange} min={0} max={9999} />);
    expect(slider()).toBeNull();
    onPage(
      <NumberInput
        value={10}
        onChange={onChange}
        min={10}
        max={100}
        prefix="W"
      />,
    );
    expect(slider()).toBeNull();
    act(() =>
      root.render(
        <NumberInput value={10} onChange={onChange} min={0} max={30} />,
      ),
    );
    expect(slider()).toBeNull();
  });

  it('hands a drag on once a frame, with the newest value', () => {
    onPage(
      <NumberInput
        value={3}
        onChange={onChange}
        min={0}
        max={20}
        suffix="px"
      />,
    );
    drag('5');
    drag('9');
    drag('14');
    expect(onChange).not.toHaveBeenCalled();
    act(() => vi.advanceTimersToNextFrame());
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenLastCalledWith(14);
  });
});
