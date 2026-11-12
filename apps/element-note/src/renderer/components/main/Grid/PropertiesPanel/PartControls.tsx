import { useEffect, useRef, useState } from 'react';
import { axisEventBus } from '@utils/core/axisEventBus';
import { NumberInput } from './PropertyInputs';
import type { NumberInputProps } from './types';

/**
 * Controls that sit in a row of the element's page: the one value they set
 * is changed right there, with no page of its own.
 */

type Translate = (key: string) => string | undefined;

interface KeyCaptureButtonProps {
  /** The name of the key that is bound now. */
  keyName?: string | null;
  /** The next key pressed on the keyboard becomes the bound key. */
  listening: boolean;
  onListen: () => void;
  t: Translate;
}

/**
 * Binds a keyboard key: pressing the button listens, and the next key that is
 * pressed is the one. The button itself names the bound key.
 */
export function KeyCaptureButton({
  keyName,
  listening,
  onListen,
  t,
}: KeyCaptureButtonProps) {
  const text = listening
    ? t('propertiesPanel.pressAnyKey') || 'Press a key now'
    : keyName || t('propertiesPanel.clickToSet') || 'Choose a key';
  return (
    <button
      type="button"
      className="en-capture"
      aria-pressed={listening}
      title={text}
      onClick={onListen}
    >
      <span role={listening ? 'status' : undefined}>{text}</span>
    </button>
  );
}

/** How many moves of one axis make it the knob that is meant. */
const AXIS_MOVES_TO_BIND = 3;
/** Listening stops by itself when no knob is turned. */
const AXIS_LISTEN_MS = 6000;

interface KnobAxisCaptureProps {
  axisId?: string | null;
  onBind: (axisId: string) => void;
  t: Translate;
}

/**
 * Binds a knob: pressing the button listens, and the knob that is turned next
 * is the one.
 */
export function KnobAxisCapture({ axisId, onBind, t }: KnobAxisCaptureProps) {
  const [listening, setListening] = useState(false);
  // The knob is bound through the newest handler; listening is not restarted
  // each time the page renders.
  const bind = useRef(onBind);
  useEffect(() => {
    bind.current = onBind;
  });

  useEffect(() => {
    if (!listening) return;
    axisEventBus.initialize();
    const moves = new Map<string, number>();
    let bound = false;
    const unsubscribe = axisEventBus.subscribe(({ axisId: moved }) => {
      if (bound) return;
      const count = (moves.get(moved) ?? 0) + 1;
      moves.set(moved, count);
      if (count >= AXIS_MOVES_TO_BIND) {
        bound = true;
        bind.current(moved);
        setListening(false);
      }
    });
    const timer = window.setTimeout(() => setListening(false), AXIS_LISTEN_MS);
    return () => {
      unsubscribe();
      window.clearTimeout(timer);
    };
  }, [listening]);

  const text = listening
    ? t('propertiesPanel.knobCapturing') || 'Turn the knob'
    : axisId
      ? axisId.replace(/^HIDA:/, '')
      : t('propertiesPanel.knobCapture') || 'Click to set';
  return (
    <button
      type="button"
      className="en-capture"
      aria-pressed={listening}
      title={axisId || text}
      onClick={() => setListening((on) => !on)}
    >
      <span role={listening ? 'status' : undefined}>{text}</span>
    </button>
  );
}

const roundTo = (value: number, digits: number) => {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
};

/**
 * A number shown rounded. Leaving the field without typing gives the rounded
 * number back; that is not a change and is not saved.
 */
export function NumberField({
  value,
  digits = 0,
  onChange,
  ...rest
}: Omit<
  NumberInputProps,
  'value' | 'onChange' | 'allowDecimal' | 'decimalScale'
> & {
  value: number;
  digits?: number;
  onChange: (value: number) => void;
}) {
  const shown = roundTo(value, digits);
  return (
    <NumberInput
      {...rest}
      value={shown}
      allowDecimal={digits > 0}
      decimalScale={digits}
      onChange={(next) => {
        if (next !== shown) onChange(next);
      }}
    />
  );
}
