import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import { useGridKeyboard } from './useGridKeyboard';

const platform = vi.hoisted(() => ({ mac: true }));
vi.mock('@utils/core/platform', () => ({ isMac: () => platform.mac }));

describe('the canvas shortcuts for undo and redo', () => {
  let container: HTMLDivElement;
  let root: Root;
  const onUndo = vi.fn();
  const onRedo = vi.fn();

  function Probe() {
    useGridKeyboard({
      selectedElements: [],
      moveSelectedElements: () => {},
      deleteSelectedElements: () => {},
      clearSelection: () => {},
      copySelectedElements: () => {},
      pasteElements: () => {},
      canUndo: true,
      canRedo: true,
      onUndo,
      onRedo,
    });
    return null;
  }
  const mount = async (mac: boolean) => {
    platform.mac = mac;
    root = createRoot(container);
    await act(async () => root.render(<Probe />));
  };
  /** A key press with the platform's command key held. */
  const press = (
    init: KeyboardEventInit,
    target: EventTarget = document.body,
  ) => {
    const event = new KeyboardEvent('keydown', {
      bubbles: true,
      cancelable: true,
      metaKey: platform.mac,
      ctrlKey: !platform.mac,
      ...init,
    });
    target.dispatchEvent(event);
    return event;
  };

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  });
  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
  });
  beforeEach(() => {
    onUndo.mockReset();
    onRedo.mockReset();
    container = document.createElement('div');
    document.body.append(container);
  });
  afterEach(async () => {
    await act(async () => root.unmount());
    container.remove();
  });

  it('undoes with the command key and Z, redoes with Shift held', async () => {
    await mount(true);

    expect(press({ key: 'z', code: 'KeyZ' }).defaultPrevented).toBe(true);
    expect(onUndo).toHaveBeenCalledOnce();
    expect(onRedo).not.toHaveBeenCalled();

    press({ key: 'Z', code: 'KeyZ', shiftKey: true });
    expect(onRedo).toHaveBeenCalledOnce();
    expect(onUndo).toHaveBeenCalledOnce();
  });

  it('undoes while a Korean input source is on', async () => {
    await mount(true);

    press({ key: 'ㅋ', code: 'KeyZ' });
    press({ key: 'ㅋ', code: 'KeyZ', shiftKey: true });

    expect(onUndo).toHaveBeenCalledOnce();
    expect(onRedo).toHaveBeenCalledOnce();
  });

  it('redoes with Ctrl+Y on Windows only', async () => {
    await mount(false);
    press({ key: 'y', code: 'KeyY' });
    press({ key: 'ㅛ', code: 'KeyY' });
    expect(onRedo).toHaveBeenCalledTimes(2);

    await act(async () => root.unmount());
    onRedo.mockReset();
    await mount(true);
    press({ key: 'y', code: 'KeyY' });
    expect(onRedo).not.toHaveBeenCalled();
  });

  it('leaves the keys to a text field that is being typed in', async () => {
    await mount(true);
    const field = document.createElement('input');
    document.body.append(field);

    expect(press({ key: 'z', code: 'KeyZ' }, field).defaultPrevented).toBe(
      false,
    );

    expect(onUndo).not.toHaveBeenCalled();
    field.remove();
  });
});

describe('pressing the canvas while a text field has the focus', () => {
  let container: HTMLDivElement;
  let root: Root;
  let canvas: HTMLDivElement;
  let element: HTMLDivElement;
  let sidebarField: HTMLInputElement;

  function Probe() {
    useGridKeyboard({
      selectedElements: [],
      moveSelectedElements: () => {},
      deleteSelectedElements: () => {},
      clearSelection: () => {},
      copySelectedElements: () => {},
      pasteElements: () => {},
      canUndo: false,
      canRedo: false,
    });
    return null;
  }
  const press = (target: EventTarget) =>
    target.dispatchEvent(new Event('pointerdown', { bubbles: true }));

  beforeAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  });
  afterAll(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = false;
  });
  beforeEach(async () => {
    container = document.createElement('div');
    canvas = document.createElement('div');
    canvas.setAttribute('data-grid-container', '');
    element = document.createElement('div');
    canvas.append(element);
    sidebarField = document.createElement('input');
    document.body.append(container, canvas, sidebarField);
    root = createRoot(container);
    await act(async () => root.render(<Probe />));
    sidebarField.focus();
  });
  afterEach(async () => {
    await act(async () => root.unmount());
    container.remove();
    canvas.remove();
    sidebarField.remove();
  });

  it('takes the focus out of the field, which settles its value', () => {
    const settled = vi.fn();
    sidebarField.addEventListener('blur', settled);

    press(element);

    expect(document.activeElement).not.toBe(sidebarField);
    expect(settled).toHaveBeenCalledOnce();
  });

  it('keeps the focus for a press outside the canvas', () => {
    const button = document.createElement('button');
    document.body.append(button);

    press(button);

    expect(document.activeElement).toBe(sidebarField);
    button.remove();
  });

  it('keeps the focus of a text field that is on the canvas itself', () => {
    const canvasField = document.createElement('input');
    canvas.append(canvasField);
    canvasField.focus();

    press(element);

    expect(document.activeElement).toBe(canvasField);
  });
});
