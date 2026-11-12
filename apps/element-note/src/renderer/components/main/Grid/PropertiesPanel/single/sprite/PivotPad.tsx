import { useRef, useState } from 'react';
import {
  SPRITE_ANCHOR_PRESETS,
  isSameSpriteAnchor,
} from '@utils/sprite/spriteGeometry';
import type { SpriteAnchor } from '@src/types/key/sprites';
import './sprite.css';

/** The room the picture is fitted into. */
const ROOM = { width: 208, height: 100 };
/** No picture is drawn smaller than this on a side. */
const MIN_SIDE = 28;
/** A press this close to one of the nine places lands on it. */
const SNAP_PX = 8;

const clamp01 = (value: number) => Math.min(1, Math.max(0, value));
const tidy = (value: number) => Math.round(value * 1000) / 1000;

interface PivotPadProps {
  /** The picture the point is placed on; null while there is none. */
  src: string | null;
  /** Width over height of that picture as it is drawn. */
  aspect: number;
  value: SpriteAnchor;
  label: string;
  /** Names a place: one of the nine usual ones, or any other. */
  nameOf: (anchor: SpriteAnchor) => string;
  /** The point is picked up. */
  onStart: () => void;
  /** The point is over this place. Nothing is saved yet. */
  onPlace: (anchor: SpriteAnchor) => void;
  /** The point was put down: saved once. */
  onEnd: () => void;
}

/**
 * Places the point an image turns and grows around, on a picture of the
 * image. The point goes where the picture is pressed and follows a drag; the
 * nine usual places (corners, edges, middle) are marked, and a press near
 * one lands on it exactly.
 */
export default function PivotPad({
  src,
  aspect,
  value,
  label,
  nameOf,
  onStart,
  onPlace,
  onEnd,
}: PivotPadProps) {
  const padRef = useRef<HTMLDivElement>(null);
  const [dragging, setDragging] = useState(false);
  const live = useRef(false);

  const ratio = Number.isFinite(aspect) && aspect > 0 ? aspect : 1;
  const width = Math.max(
    MIN_SIDE,
    Math.min(ROOM.width, Math.round(ROOM.height * ratio)),
  );
  const height = Math.max(
    MIN_SIDE,
    Math.min(ROOM.height, Math.round(width / ratio)),
  );

  const placeAt = (clientX: number, clientY: number) => {
    const rect = padRef.current?.getBoundingClientRect();
    if (!rect || rect.width <= 0 || rect.height <= 0) return;
    const x = clamp01((clientX - rect.left) / rect.width);
    const y = clamp01((clientY - rect.top) / rect.height);
    const near = SPRITE_ANCHOR_PRESETS.find(
      (preset) =>
        Math.hypot((preset.x - x) * rect.width, (preset.y - y) * rect.height) <=
        SNAP_PX,
    );
    onPlace(near ? { x: near.x, y: near.y } : { x: tidy(x), y: tidy(y) });
  };

  const end = () => {
    if (!live.current) return;
    live.current = false;
    setDragging(false);
    onEnd();
  };

  return (
    <div
      className="en-pivot"
      data-dragging={dragging ? 'true' : undefined}
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.currentTarget.setPointerCapture?.(event.pointerId);
        live.current = true;
        setDragging(true);
        onStart();
        placeAt(event.clientX, event.clientY);
      }}
      onPointerMove={(event) => {
        if (live.current) placeAt(event.clientX, event.clientY);
      }}
      onPointerUp={end}
      onPointerCancel={end}
      onLostPointerCapture={end}
    >
      <div
        ref={padRef}
        className="en-pivot-pad"
        role="group"
        aria-label={label}
        style={{ width, height }}
      >
        {src ? (
          <img
            className="en-pivot-picture"
            src={src}
            alt=""
            draggable={false}
          />
        ) : (
          <span className="en-pivot-picture" data-blank />
        )}
        {SPRITE_ANCHOR_PRESETS.map((preset) => (
          <button
            key={`${preset.x}:${preset.y}`}
            type="button"
            className="en-pivot-point"
            aria-label={nameOf(preset)}
            aria-pressed={isSameSpriteAnchor(preset, value)}
            style={{ left: `${preset.x * 100}%`, top: `${preset.y * 100}%` }}
            onClick={(event) => {
              // A press is placed by the pad; this is a key on the button.
              if (event.detail !== 0) return;
              onStart();
              onPlace({ x: preset.x, y: preset.y });
              onEnd();
            }}
          />
        ))}
        <span
          className="en-pivot-dot"
          aria-hidden="true"
          style={{ left: `${value.x * 100}%`, top: `${value.y * 100}%` }}
        />
      </div>
      <p className="en-pivot-name">{nameOf(value)}</p>
    </div>
  );
}
