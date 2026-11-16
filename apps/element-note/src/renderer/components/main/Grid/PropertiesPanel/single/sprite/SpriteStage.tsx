import { useEffect, useRef, useState } from 'react';
import { spriteTransformToCss } from '@utils/sprite/spriteImageStyles';
import {
  frameSpriteLooks,
  isSameSpriteLook,
  spriteFrameScale,
  type SpriteFrame,
  type SpriteLook,
} from '@utils/sprite/spriteLooks';
import { resolveSpriteRenderEasing } from '@utils/sprite/spriteReach';
import { clamp } from '@utils/number/clamp';
import {
  SPRITE_CONSTRAINTS,
  type ReactiveSpritePosition,
  type SpriteActivation,
  type SpriteTransform,
} from '@src/types/key/sprites';
import SpriteLookView from './SpriteLookView';
import './sprite.css';

/** The room the picture is fitted into; the stage around it can be wider. */
const VIEW = { width: 232, height: 112 };
/** How long the picture takes to come back after a replay that left it. */
const REPLAY_RETURN_MS = 180;

const prefersReducedMotion = () =>
  window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

export interface StageMotion {
  /** The look is being tried out right now. */
  playing: boolean;
  /** Where the picture comes from, or goes back to. */
  rest: SpriteTransform;
  activation: SpriteActivation;
  changeMs: number;
  returnMs: number;
  easing: string;
}

interface SpriteStageProps {
  sprite: ReactiveSpritePosition;
  /** The look that is edited. */
  look: SpriteLook;
  /** The resting look, drawn faintly behind a look that differs from it. */
  rest?: SpriteLook;
  label: string;
  /** Says that the picture can be dragged. */
  hint: string;
  /** The picture is dragged to this shift. Nothing is saved yet. */
  onMove: (shift: { x: number; y: number }) => void;
  /** The picture was let go: the drag is saved once. */
  onMoveEnd: () => void;
  /** Plays how the look is reached while it is tried out. */
  motion?: StageMotion;
}

interface Drag {
  /** The frame the drag began in; it stays until the picture is let go. */
  frame: SpriteFrame;
  scale: number;
  x: number;
  y: number;
  from: SpriteTransform;
}

/**
 * The look of an image as it is being edited, large enough to judge. The
 * picture follows a change of any number at once, and can itself be dragged
 * to shift it. While the look is tried out, the picture replays how it is
 * reached, in step with the canvas.
 */
export default function SpriteStage({
  sprite,
  look,
  rest,
  label,
  hint,
  onMove,
  onMoveEnd,
  motion,
}: SpriteStageProps) {
  const ghost = rest && !isSameSpriteLook(rest, look) ? rest : undefined;
  const fitted = frameSpriteLooks(sprite, ghost ? [ghost, look] : [look]);
  const [drag, setDrag] = useState<Drag | null>(null);
  // Read by the pointer handlers: a move can arrive before the next render.
  const live = useRef<Drag | null>(null);
  const frame = drag?.frame ?? fitted;

  const pictureRef = useRef<HTMLElement>(null);
  const replay = useRef<Animation | null>(null);
  const latest = useRef({ look, motion });
  useEffect(() => {
    latest.current = { look, motion };
  });

  const playing = motion?.playing ?? false;
  useEffect(() => {
    if (!playing) return;
    const picture = pictureRef.current;
    const { look: shown, motion: how } = latest.current;
    if (
      !picture ||
      !how ||
      typeof picture.animate !== 'function' ||
      prefersReducedMotion()
    )
      return;
    const lookCss = spriteTransformToCss(shown.transform);
    const restCss = spriteTransformToCss(how.rest);
    const easing = resolveSpriteRenderEasing(how.easing);
    replay.current?.cancel();
    try {
      if (how.activation === 'onPress') {
        // One press: the look at once, then back by itself.
        const back = picture.animate(
          [{ transform: lookCss }, { transform: restCss }],
          { duration: how.returnMs, easing, fill: 'forwards' },
        );
        replay.current = back;
        back.onfinish = () => {
          back.cancel();
          // The stage shows the look again; it fades in instead of jumping.
          replay.current = picture.animate([{ opacity: 0 }, { opacity: 1 }], {
            duration: REPLAY_RETURN_MS,
            easing: 'ease-out',
          });
        };
      } else {
        replay.current = picture.animate(
          [{ transform: restCss }, { transform: lookCss }],
          { duration: how.changeMs, easing },
        );
      }
    } catch {
      // The engine does not know this curve: there is nothing to replay.
      replay.current = null;
    }
  }, [playing]);
  useEffect(() => () => replay.current?.cancel(), []);

  const end = () => {
    if (!live.current) return;
    live.current = null;
    setDrag(null);
    onMoveEnd();
  };

  return (
    <div
      className="en-stage"
      role="group"
      aria-label={label}
      data-dragging={drag ? 'true' : undefined}
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.currentTarget.setPointerCapture?.(event.pointerId);
        const started: Drag = {
          frame: fitted,
          scale: spriteFrameScale(fitted, VIEW),
          x: event.clientX,
          y: event.clientY,
          from: look.transform,
        };
        live.current = started;
        setDrag(started);
      }}
      onPointerMove={(event) => {
        const held = live.current;
        if (!held) return;
        const { min, max } = SPRITE_CONSTRAINTS.offset;
        // Whole pixels of travel, so an axis that is not moved keeps its value.
        onMove({
          x: clamp(
            held.from.x + Math.round((event.clientX - held.x) / held.scale),
            min,
            max,
          ),
          y: clamp(
            held.from.y + Math.round((event.clientY - held.y) / held.scale),
            min,
            max,
          ),
        });
      }}
      onPointerUp={end}
      onPointerCancel={end}
      onLostPointerCapture={end}
    >
      <SpriteLookView
        sprite={sprite}
        look={look}
        ghost={ghost}
        frame={frame}
        width={VIEW.width}
        height={VIEW.height}
        pictureRef={pictureRef}
      />
      <p className="en-stage-hint">{hint}</p>
    </div>
  );
}
