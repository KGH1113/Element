import { useEffect, useRef } from 'react';
import type { Choice } from './ChoiceTiles';
import './PictureSegments.css';

interface PictureSegmentsProps<Id extends string> {
  /** Names the setting: the line above the choices. */
  label: string;
  choices: readonly Choice<Id>[];
  value: Id;
  disabled?: boolean;
  onChoose: (id: Id) => void;
  /**
   * The pointer rests on a choice (its id), or has left them all (null): the
   * canvas can show that look before it is chosen.
   */
  onPreview?: (id: Id | null) => void;
}

/**
 * A setting that is one of a few looks, all of them shown at once: one
 * control, a picture of what each choice does with its name under it. A few
 * choices are chosen here, in the row; a longer set opens ChoiceTiles beside
 * the sidebar instead.
 */
export default function PictureSegments<Id extends string>({
  label,
  choices,
  value,
  disabled = false,
  onChoose,
  onPreview,
}: PictureSegmentsProps<Id>) {
  const preview = useRef(onPreview);
  useEffect(() => {
    preview.current = onPreview;
  });
  // Leaving the page, or the setting no longer applying, ends the preview.
  useEffect(() => {
    if (disabled) preview.current?.(null);
  }, [disabled]);
  useEffect(() => () => preview.current?.(null), []);

  return (
    <div className="en-segments-row">
      <span className="en-segments-label">{label}</span>
      <div
        className="en-segments"
        role="radiogroup"
        aria-label={label}
        aria-disabled={disabled || undefined}
        onPointerLeave={() => onPreview?.(null)}
      >
        {choices.map((choice) => {
          const chosen = choice.id === value;
          return (
            <button
              key={choice.id}
              type="button"
              role="radio"
              aria-checked={chosen}
              disabled={disabled}
              className="en-segment"
              onPointerEnter={() => {
                if (!disabled) onPreview?.(choice.id);
              }}
              onClick={() => {
                if (!chosen) onChoose(choice.id);
              }}
            >
              <span className="en-segment-picture" aria-hidden="true">
                {choice.picture}
              </span>
              <span className="en-segment-label">{choice.label}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
