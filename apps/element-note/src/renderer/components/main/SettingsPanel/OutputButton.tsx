import type { ReactNode } from 'react';

export type OutputPlace = 'game' | 'desktop' | 'obs';

interface OutputButtonProps {
  place: OutputPlace;
  name: string;
  description: string;
  /** This is the place in use. Only one is at a time. */
  chosen: boolean;
  /** What the place is doing right now; empty while it is not in use. */
  state: string;
  tone?: string;
  onChoose: (place: OutputPlace) => void;
}

/** Four keys of a key viewer, small. They light one after another. */
const Keys = ({ x, y }: { x: number; y: number }) => (
  <g className="en-output-keys">
    {[0, 1, 2, 3].map((key) => (
      <rect
        key={key}
        x={x + key * 9}
        y={y}
        width="7"
        height="7"
        rx="1.6"
        style={{ '--key': key } as React.CSSProperties}
      />
    ))}
  </g>
);

/** A screen on its stand: where both the game and the desktop are seen. */
const Screen = () => (
  <>
    <rect
      className="en-output-frame"
      x="14"
      y="5"
      width="92"
      height="46"
      rx="5"
    />
    <path className="en-output-stand" d="M52 51h16l3 8H49z" />
  </>
);

/**
 * A small moving picture of each place. They move only while the place is
 * in use, slowly, and stand still for people who asked for less motion.
 */
const PICTURES: Record<OutputPlace, ReactNode> = {
  // Inside the game: the keys are in the game's window and go where it goes.
  game: (
    <>
      <Screen />
      <g className="en-output-window">
        <rect x="26" y="13" width="50" height="30" rx="3" />
        <path d="M26 19h50" />
        <Keys x={34} y={30} />
      </g>
    </>
  ),
  // On the desktop: a window drifts behind while the keys stay put on top.
  desktop: (
    <>
      <Screen />
      <g className="en-output-window">
        <rect x="26" y="13" width="44" height="26" rx="3" />
        <path d="M26 19h44" />
      </g>
      <Keys x={62} y={34} />
    </>
  ),
  // In OBS: its mark, a ring with three dots, beside the keys.
  obs: (
    <>
      <rect
        className="en-output-frame"
        x="14"
        y="6"
        width="92"
        height="52"
        rx="7"
      />
      <g className="en-output-mark">
        <circle className="en-output-ring" cx="42" cy="32" r="13" />
        <circle className="en-output-dot" cx="42" cy="25.4" r="3.3" />
        <circle className="en-output-dot" cx="36.3" cy="35.3" r="3.3" />
        <circle className="en-output-dot" cx="47.7" cy="35.3" r="3.3" />
      </g>
      <Keys x={64} y={28.5} />
    </>
  ),
};

/**
 * One place the key viewer can be shown, as a large choice: pressing it puts
 * the key viewer there instead of where it was. It says what it is doing in
 * words under its picture while it is the one in use.
 */
export function OutputButton({
  place,
  name,
  description,
  chosen,
  state,
  tone,
  onChoose,
}: OutputButtonProps) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={chosen}
      aria-label={name}
      aria-describedby={`en-output-${place}-help`}
      className="en-output"
      data-output-button={place}
      onClick={() => onChoose(place)}
    >
      <svg
        className="en-output-picture"
        viewBox="0 0 120 64"
        aria-hidden="true"
      >
        {PICTURES[place]}
      </svg>
      <span className="en-output-name">{name}</span>
      <span className="en-output-help" id={`en-output-${place}-help`}>
        {description}
      </span>
      {/* Keeps its line when empty, so the three buttons stay one height. */}
      <span className="en-output-state" data-tone={chosen ? tone : undefined}>
        {state}
      </span>
    </button>
  );
}
