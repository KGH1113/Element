import { useEffect, useRef, type CSSProperties } from 'react';

/** What a number stands for, drawn so that the drawing follows the number. */
export type RangeVisual = 'stroke' | 'corner' | 'text' | 'glow' | 'volume';

interface RangeTrackProps {
  value: number;
  min: number;
  max: number;
  step: number;
  /** Names the slider; the same words as its number field. */
  label?: string;
  visual?: RangeVisual;
  disabled?: boolean;
  onChange: (value: number) => void;
}

const share = (value: number, min: number, max: number) =>
  max > min ? Math.min(1, Math.max(0, (value - min) / (max - min))) : 0;

/**
 * A small picture of the value: a line as thick as the border, a corner as
 * round as the corners. It changes while the slider is dragged, so the number
 * is seen as well as read.
 */
function RangeGlyph({
  visual,
  amount,
}: {
  visual: RangeVisual;
  amount: number;
}) {
  const common = {
    width: 22,
    height: 22,
    viewBox: '0 0 22 22',
    fill: 'none',
    stroke: 'currentColor',
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true,
  };
  if (visual === 'stroke') {
    return (
      <svg {...common}>
        <path d="M4 11h14" strokeWidth={1 + amount * 7} />
      </svg>
    );
  }
  if (visual === 'corner') {
    const radius = amount * 13;
    return (
      <svg {...common} strokeWidth={1.6}>
        <path d={`M4 19V${4 + radius}Q4 4 ${4 + radius} 4H19`} />
      </svg>
    );
  }
  if (visual === 'text') {
    return (
      <svg {...common} stroke="none" fill="currentColor">
        <text
          x="11"
          y="18"
          textAnchor="middle"
          fontSize={9 + amount * 13}
          fontWeight="700"
        >
          A
        </text>
      </svg>
    );
  }
  if (visual === 'glow') {
    return (
      <svg {...common} stroke="none" fill="currentColor">
        <circle cx="11" cy="11" r={4 + amount * 7} opacity="0.28" />
        <circle cx="11" cy="11" r="3.5" />
      </svg>
    );
  }
  return (
    <svg {...common} strokeWidth={1.6}>
      <path d="M3.5 9v4h3l4 3.2V5.8L6.5 9z" />
      {amount > 0 && <path d="M13.5 9a3 3 0 0 1 0 4" />}
      {amount > 0.4 && <path d="M15.8 6.800a6 6 0 0 1 0 8.400" />}
      {amount > 0.75 && <path d="M18 4.800a9 9 0 0 1 0 12.400" />}
    </svg>
  );
}

/**
 * A number with a small range, as something to drag. It sits under its row's
 * number field and says the same value. Changes are handed on once a frame
 * at most, however fast the pointer moves.
 */
export default function RangeTrack({
  value,
  min,
  max,
  step,
  label,
  visual,
  disabled = false,
  onChange,
}: RangeTrackProps) {
  const pending = useRef<number | null>(null);
  const frame = useRef(0);
  const latest = useRef(onChange);
  useEffect(() => {
    latest.current = onChange;
  });
  useEffect(() => () => cancelAnimationFrame(frame.current), []);

  const amount = share(value, min, max);
  return (
    <span
      className="en-range"
      data-disabled={disabled || undefined}
      style={{ '--en-range-fill': `${amount * 100}%` } as CSSProperties}
    >
      {visual && (
        <span className="en-range-glyph">
          <RangeGlyph visual={visual} amount={amount} />
        </span>
      )}
      <input
        type="range"
        className="en-range-input"
        aria-label={label}
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={disabled}
        onChange={(event) => {
          pending.current = Number(event.currentTarget.value);
          if (frame.current) return;
          frame.current = requestAnimationFrame(() => {
            frame.current = 0;
            const next = pending.current;
            pending.current = null;
            if (next !== null) latest.current(next);
          });
        }}
      />
    </span>
  );
}
