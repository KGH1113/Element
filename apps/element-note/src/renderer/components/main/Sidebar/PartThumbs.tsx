import type { CSSProperties } from 'react';
import { computeKeyElementStyles } from '@hooks/overlay/useKeyElementStyles';
import {
  normalizeCounterSettings,
  type KeyPosition,
  type NoteColor,
} from '@src/types/key/keys';

/**
 * Small pictures at the start of a sidebar row. Each is the element as it
 * really looks, drawn with the same styles the overlay uses and fitted into
 * the row's well; a part that changes the look shows the element with that
 * part on it. Nothing here is a drawing of a key: it is the key.
 */

interface Room {
  width: number;
  height: number;
}

/** What a picture may take of its well (36px) when it is the whole picture. */
const ROOM: Room = { width: 26, height: 20 };
/** A key under something it carries: notes, a count. */
const LOW_ROOM: Room = { width: 24, height: 9 };

const NOTE_WHITE = '#FFFFFF';

/**
 * A color nothing can be seen through: `transparent`, or any color whose
 * alpha is 0. A ghost key starts out this way, so its picture would be empty;
 * it is drawn as an outline instead.
 */
const isClear = (color: string | undefined) => {
  if (!color) return false;
  const value = color.trim().toLowerCase();
  return (
    value === 'transparent' ||
    /^rgba?\(.*,\s*0(?:\.0*)?\s*\)$/.test(value) ||
    /^#(?:[0-9a-f]{6}00|[0-9a-f]{3}0)$/.test(value)
  );
};

const notePaint = (color: NoteColor | undefined) =>
  !color
    ? NOTE_WHITE
    : typeof color === 'string'
      ? color
      : `linear-gradient(${color.top}, ${color.bottom})`;

interface MiniKeyProps {
  position: KeyPosition;
  /** Written on the key; a picture of the face alone has none. */
  label?: string;
  room: Room;
}

/** A key at rest, exactly as the overlay draws it, made to fit `room`. */
function MiniKey({ position, label, room }: MiniKeyProps) {
  const width = position.width || 60;
  const height = position.height || 60;
  const scale = Math.min(room.width / width, room.height / height);
  const box: CSSProperties = { width: width * scale, height: height * scale };
  const styles = computeKeyElementStyles({
    position: { ...position, dx: 0, dy: 0, rotation: 0 },
    active: false,
    label: label ?? '',
  });
  const unseen =
    isClear(position.backgroundColor) &&
    isClear(position.borderColor) &&
    !styles.hasCurrentImage;
  if (styles.isTransparent || unseen) {
    return <span className="en-mini-key" style={box} data-empty />;
  }
  return (
    <span className="en-mini-key" style={box}>
      <span
        className="en-mini-key-face"
        style={{
          ...styles.keyStyle,
          transform: `scale(${scale})`,
          willChange: 'auto',
          zIndex: undefined,
        }}
      >
        {styles.borderRingStyle && (
          <span aria-hidden="true" style={styles.borderRingStyle} />
        )}
        {styles.hasCurrentImage && (
          <img
            src={styles.currentImageSrc || ''}
            alt=""
            style={styles.imageStyle}
            draggable={false}
          />
        )}
        {label !== undefined && !styles.imageReplaces && (
          <span
            className="flex items-center justify-center h-full font-bold text-safe-inline"
            style={styles.textStyle}
          >
            {styles.labelText}
          </span>
        )}
      </span>
    </span>
  );
}

/** The face of a key or a stat: its colors, its border, its corners. */
export function FaceThumb({ position }: { position: KeyPosition }) {
  return <MiniKey position={position} room={ROOM} />;
}

/**
 * A whole key: its face, and what is written on it in its font and color.
 * Shrunk with the face the letters could not be read, so they keep a size
 * that can; a key whose picture replaces its letters shows the picture alone.
 */
export function KeyThumb({
  position,
  label,
}: {
  position: KeyPosition;
  label: string;
}) {
  const { keyStyle, textStyle, imageReplaces, labelText } =
    computeKeyElementStyles({ position, active: false, label });
  return (
    <span className="en-key-thumb">
      <MiniKey position={position} room={ROOM} />
      {!imageReplaces && labelText && (
        <span
          className="en-key-thumb-label"
          style={{
            color: keyStyle.color,
            fontFamily: textStyle.fontFamily,
            fontWeight: textStyle.fontWeight,
          }}
        >
          {labelText}
        </span>
      )}
    </span>
  );
}

/** The letters of a key, in its font and its color. */
export function TextThumb({ position }: { position: KeyPosition }) {
  const { keyStyle, textStyle } = computeKeyElementStyles({
    position,
    active: false,
    label: '',
  });
  return (
    <span
      className="en-thumb-text"
      data-empty={isClear(position.fontColor) || undefined}
      style={{
        color: keyStyle.color,
        fontFamily: textStyle.fontFamily,
        fontWeight: textStyle.fontWeight,
        fontStyle: textStyle.fontStyle,
      }}
    >
      Aa
    </span>
  );
}

/** Notes rising from the key, in the key's note color. */
export function NoteThumb({ position }: { position: KeyPosition }) {
  const paint = notePaint(position.noteColor);
  return (
    <span className="en-thumb-stack">
      <span className="en-thumb-notes">
        <i style={{ background: paint, height: 4, opacity: 0.55 }} />
        <i style={{ background: paint, height: 7 }} />
      </span>
      <MiniKey position={position} room={LOW_ROOM} />
    </span>
  );
}

/** The count over the key, in the color it is counted in. */
export function CountThumb({ position }: { position: KeyPosition }) {
  const counter = normalizeCounterSettings(position.counter);
  return (
    <span className="en-thumb-stack">
      <span className="en-thumb-count" style={{ color: counter.fill.idle }}>
        {position.count ?? 0}
      </span>
      <MiniKey position={position} room={LOW_ROOM} />
    </span>
  );
}

/** A graph's box in its colors, and its line when the row is about it. */
export function GraphThumb({
  background,
  border,
  line,
}: {
  background: string;
  border: string;
  line?: string;
}) {
  return (
    <span
      className="en-thumb-graph"
      style={{ background, boxShadow: `inset 0 0 0 1px ${border}` }}
    >
      {line && (
        <svg width="20" height="10" viewBox="0 0 20 10" fill="none">
          <polyline
            points="1,9 5,6 8,7.5 12,2.5 15,4.5 19,1"
            stroke={line}
            strokeWidth="1.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      )}
    </span>
  );
}

/** A knob: its dial, in its colors. */
export function KnobThumb({
  background,
  border,
}: {
  background: string;
  border: string;
}) {
  return (
    <span
      className="en-thumb-knob"
      style={{ background, boxShadow: `inset 0 0 0 1.5px ${border}` }}
    >
      <i />
    </span>
  );
}
