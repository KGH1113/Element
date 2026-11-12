import { useRef, useState } from 'react';
import './StepSlider.css';

interface StepSliderProps {
  /** The values the thumb rests on, from the lowest up. */
  stops: readonly number[];
  /** May lie between two stops: it is shown there until the thumb is moved. */
  value: number;
  ariaLabel: string;
  /** The value in words, shown beside the track. */
  format: (value: number) => string;
  /** The thumb is being dragged over this value. Nothing is saved yet. */
  onPreview?: (value: number) => void;
  /** The thumb was let go on a new value, or a key moved it. Called once. */
  onCommit: (value: number) => void;
}

const clamp01 = (value: number) => Math.min(1, Math.max(0, value));

/** Where a value sits along the track, from 0 to 1. */
const positionOf = (stops: readonly number[], value: number): number => {
  const last = stops.length - 1;
  if (last <= 0 || value <= stops[0]) return 0;
  if (value >= stops[last]) return 1;
  const above = stops.findIndex((stop) => stop >= value);
  const from = stops[above - 1];
  return (above - 1 + (value - from) / (stops[above] - from)) / last;
};

/** Room a text needs, in digit widths: wide letters count for more. */
const textRoom = (text: string): number =>
  [...text].reduce(
    (room, letter) => room + (letter.codePointAt(0)! > 0x2e7f ? 1.8 : 1),
    0.5,
  );

const stopAt = (stops: readonly number[], position: number): number =>
  stops[Math.round(clamp01(position) * (stops.length - 1))];

/** The stop a key moves to from a value, or the value itself at an end. */
const stopBeside = (
  stops: readonly number[],
  value: number,
  step: number,
): number => {
  if (step > 0) return stops.find((stop) => stop > value) ?? value;
  return [...stops].reverse().find((stop) => stop < value) ?? value;
};

/**
 * A slider over a few values that reads its value in words. While the thumb
 * is held it follows the pointer exactly and nothing is saved; the value is
 * committed once, when the thumb is let go.
 */
export default function StepSlider({
  stops,
  value,
  ariaLabel,
  format,
  onPreview,
  onCommit,
}: StepSliderProps) {
  const railRef = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState<{ position: number; value: number } | null>(
    null,
  );
  // What the pointer handlers read: two events can arrive before a render.
  const live = useRef(drag);

  const shown = drag?.value ?? value;
  const position = drag?.position ?? positionOf(stops, value);
  // As wide as the longest value, so the track never changes its length.
  const valueRoom = Math.max(
    textRoom(format(value)),
    ...stops.map((stop) => textRoom(format(stop))),
  );

  const track = (clientX: number) => {
    const rect = railRef.current?.getBoundingClientRect();
    const at =
      rect && rect.width > 0 ? clamp01((clientX - rect.left) / rect.width) : 0;
    const next = { position: at, value: stopAt(stops, at) };
    const before = live.current?.value ?? value;
    live.current = next;
    setDrag(next);
    if (next.value !== before) onPreview?.(next.value);
  };

  const end = (commit: boolean) => {
    const last = live.current;
    if (!last) return;
    live.current = null;
    setDrag(null);
    if (last.value === value) return;
    if (commit) onCommit(last.value);
    else onPreview?.(value);
  };

  const moveByKey = (next: number) => {
    if (next !== value) onCommit(next);
  };

  return (
    <div className="en-slider" data-dragging={drag ? 'true' : undefined}>
      <div
        className="en-slider-track"
        role="slider"
        tabIndex={0}
        aria-label={ariaLabel}
        aria-valuemin={stops[0]}
        aria-valuemax={stops[stops.length - 1]}
        aria-valuenow={shown}
        aria-valuetext={format(shown)}
        onPointerDown={(event) => {
          if (event.button !== 0) return;
          event.currentTarget.setPointerCapture?.(event.pointerId);
          track(event.clientX);
        }}
        onPointerMove={(event) => {
          if (live.current) track(event.clientX);
        }}
        onPointerUp={() => end(true)}
        onPointerCancel={() => end(false)}
        onLostPointerCapture={() => end(false)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowRight' || event.key === 'ArrowUp') {
            moveByKey(stopBeside(stops, value, 1));
          } else if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') {
            moveByKey(stopBeside(stops, value, -1));
          } else if (event.key === 'Home') {
            moveByKey(stops[0]);
          } else if (event.key === 'End') {
            moveByKey(stops[stops.length - 1]);
          } else {
            return;
          }
          event.preventDefault();
        }}
      >
        <div ref={railRef} className="en-slider-rail">
          <span className="en-slider-groove">
            <span
              className="en-slider-fill"
              style={{ transform: `scaleX(${position})` }}
            />
          </span>
          <span
            className="en-slider-lane"
            style={{ transform: `translate3d(${position * 100}%, 0, 0)` }}
          >
            <span className="en-slider-thumb" />
          </span>
        </div>
      </div>
      <span
        className="en-slider-value"
        aria-hidden="true"
        style={{ minWidth: `${valueRoom}ch` }}
      >
        {format(shown)}
      </span>
    </div>
  );
}
