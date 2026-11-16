import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { historyStepId, startHistorySteps } from './historyStep';

describe('where one undo step ends and the next begins', () => {
  let stop: () => void;
  let field: HTMLInputElement;
  let other: HTMLInputElement;
  let canvas: HTMLDivElement;

  const press = (target: EventTarget = canvas) =>
    target.dispatchEvent(new Event('pointerdown', { bubbles: true }));
  const key = (
    name: string,
    target: EventTarget = document.body,
    init: KeyboardEventInit = {},
  ) =>
    target.dispatchEvent(
      new KeyboardEvent('keydown', { key: name, bubbles: true, ...init }),
    );
  /** Clicks into a field: the press, then the focus it brings. */
  const enter = (target: HTMLInputElement) => {
    press(target);
    target.focus();
  };
  /** What a field saves while it loses the focus. */
  const savedOnLeaving = (target: HTMLInputElement) => {
    const saved: (string | undefined)[] = [];
    target.addEventListener('focusout', () => saved.push(historyStepId()));
    return saved;
  };

  beforeEach(() => {
    field = document.createElement('input');
    other = document.createElement('input');
    canvas = document.createElement('div');
    document.body.append(field, other, canvas);
    stop = startHistorySteps();
  });

  afterEach(() => {
    stop();
    field.remove();
    other.remove();
    canvas.remove();
  });

  it('keeps every save of one press in one step', () => {
    press();
    const dragging = historyStepId();
    // The saves while dragging, on release and after an await all follow.
    expect(historyStepId()).toBe(dragging);
    window.dispatchEvent(new Event('pointerup'));
    expect(historyStepId()).toBe(dragging);
    expect(dragging).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('starts a new step with the next press', () => {
    press();
    const first = historyStepId();
    press();
    expect(historyStepId()).not.toBe(first);
  });

  it('starts a new step with each key press on the canvas', () => {
    key('ArrowRight');
    const first = historyStepId();
    key('ArrowRight');
    const second = historyStepId();
    key('Delete');
    expect(second).not.toBe(first);
    expect(historyStepId()).not.toBe(second);
  });

  it('keeps a held key in the step of its first press', () => {
    key('ArrowRight');
    const nudging = historyStepId();
    key('ArrowRight', document.body, { repeat: true });
    key('ArrowRight', document.body, { repeat: true });
    expect(historyStepId()).toBe(nudging);
  });

  it('keeps a modifier pressed during a drag in the step of the drag', () => {
    press();
    const dragging = historyStepId();
    key('Shift');
    key('Alt');
    expect(historyStepId()).toBe(dragging);
  });

  it('keeps the letters typed into one field in one step', () => {
    enter(field);
    key('1', field);
    const typing = historyStepId();
    key('2', field);
    key('Enter', field);
    expect(historyStepId()).toBe(typing);

    // Enter settles the value: the field is left without a new press.
    const saved = savedOnLeaving(field);
    field.blur();
    expect(saved).toEqual([typing]);
  });

  it('ends the step of a button before typing goes on in a field', () => {
    enter(field);
    key('1', field);
    const typing = historyStepId();
    // A button that keeps the field focused.
    press();
    const pressed = historyStepId();
    key('2', field);
    expect(pressed).not.toBe(typing);
    expect(historyStepId()).not.toBe(pressed);
  });

  it('counts the value a field settles on leaving as the last save of its step', () => {
    enter(field);
    key('1', field);
    const typing = historyStepId();
    const saved = savedOnLeaving(field);

    // The press that takes the focus away starts the next step, but the
    // field saves its value while it is being left.
    press();
    const dragging = historyStepId();
    field.blur();

    expect(saved).toEqual([typing]);
    expect(dragging).not.toBe(typing);
    expect(historyStepId()).toBe(dragging);
  });

  it('gives a field that only saves on leaving a step of its own', () => {
    press();
    const earlier = historyStepId();
    // Typing shows a preview and saves nothing until the field is left.
    enter(field);
    key('1', field);
    key('2', field);
    const saved = savedOnLeaving(field);

    press();
    const dragging = historyStepId();
    field.blur();

    expect(saved).toHaveLength(1);
    expect(saved[0]).toMatch(/^[0-9a-f-]{36}$/);
    expect(saved[0]).not.toBe(earlier);
    expect(saved[0]).not.toBe(dragging);
  });

  it('keeps what the press itself saves before the field is left in the new step', () => {
    enter(field);
    key('1', field);
    const typing = historyStepId();
    const saved = savedOnLeaving(field);

    // A slider saves on the press, and only then does the focus move.
    press();
    const scrubbing = historyStepId();
    field.blur();

    expect(scrubbing).not.toBe(typing);
    expect(saved).toEqual([typing]);
    expect(historyStepId()).toBe(scrubbing);
  });

  it('ends the step when Tab moves on to the next field', () => {
    enter(field);
    key('1', field);
    const first = historyStepId();
    const saved = savedOnLeaving(field);

    key('Tab', field);
    other.focus();
    key('2', other);

    expect(saved).toEqual([first]);
    expect(historyStepId()).not.toBe(first);
  });

  it('does not treat a press inside the field as leaving it', () => {
    enter(field);
    key('1', field);
    press(field);
    key('2', field);
    const typing = historyStepId();

    const saved = savedOnLeaving(field);
    field.blur();

    expect(saved).toEqual([typing]);
  });

  it('stops holding back the next step when a handler stops the event', async () => {
    enter(field);
    key('1', field);
    const typing = historyStepId();
    field.addEventListener('focusout', (event) => event.stopPropagation());

    press();
    field.blur();
    expect(historyStepId()).toBe(typing);

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(historyStepId()).not.toBe(typing);
  });

  it('gives no step where the boundaries are not heard', () => {
    stop();
    expect(historyStepId()).toBeUndefined();
  });
});
