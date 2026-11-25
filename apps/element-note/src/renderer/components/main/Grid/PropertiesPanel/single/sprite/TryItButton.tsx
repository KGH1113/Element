import { useEffect } from 'react';
import {
  armSpriteTryOut,
  pressSpriteTryOut,
  releaseSpriteTryOut,
  useSpriteTryOutStore,
} from '@src/renderer/editor/runtime/spriteTryOut';
import './sprite.css';

interface TryItButtonProps {
  spriteId: string;
  /** The look that is tried out. */
  poseId: string;
  label: string;
}

/**
 * Held down, it shows a look on the canvas as if the look's keys were held:
 * the user does not have to find and press them, and a look that has no key
 * yet can be tried too. Letting go, leaving the button or leaving the page
 * ends it. Nothing is saved (see spriteTryOut.ts).
 */
export default function TryItButton({
  spriteId,
  poseId,
  label,
}: TryItButtonProps) {
  const held = useSpriteTryOutStore((state) => state.held);

  // The look answers to the try-out key for as long as its button is there.
  useEffect(() => armSpriteTryOut(spriteId, poseId), [spriteId, poseId]);
  // A window that loses focus never sees the button being let go.
  useEffect(() => {
    window.addEventListener('blur', releaseSpriteTryOut);
    return () => window.removeEventListener('blur', releaseSpriteTryOut);
  }, []);

  return (
    <button
      type="button"
      className="en-try"
      aria-pressed={held}
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.currentTarget.setPointerCapture?.(event.pointerId);
        pressSpriteTryOut();
      }}
      onPointerUp={releaseSpriteTryOut}
      onPointerCancel={releaseSpriteTryOut}
      onLostPointerCapture={releaseSpriteTryOut}
      onBlur={releaseSpriteTryOut}
      onKeyDown={(event) => {
        if (event.key !== ' ' && event.key !== 'Enter') return;
        event.preventDefault();
        if (!event.repeat) pressSpriteTryOut();
      }}
      onKeyUp={(event) => {
        if (event.key === ' ' || event.key === 'Enter') releaseSpriteTryOut();
      }}
      onContextMenu={(event) => event.preventDefault()}
    >
      {label}
    </button>
  );
}
