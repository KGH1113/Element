import type { CSSProperties, ReactNode, RefObject } from 'react';
import FloatingPopup from '@components/main/Modal/FloatingPopup';
import './ChoiceTiles.css';

export interface Choice<Id extends string = string> {
  id: Id;
  label: string;
  /** Shows what the choice does; the label only names it. */
  picture: ReactNode;
  /**
   * Shown as what is chosen and cannot be chosen: a stored value that is
   * none of the other choices. Choosing another one replaces it.
   */
  readOnly?: boolean;
}

interface ChoiceTilesProps<Id extends string> {
  /** Names what is chosen. */
  title: string;
  /** The row that opened the chooser. */
  trigger: RefObject<HTMLElement | null>;
  choices: readonly Choice<Id>[];
  value: Id;
  /** How many choices sit side by side. */
  columns?: number;
  onChoose: (id: Id) => void;
  onClose: () => void;
}

/**
 * One of a few things that differ in how they look or move: each choice is a
 * tile that shows it. Opened from a row of the sidebar it floats beside the
 * sidebar and points at that row, like every chooser.
 */
export default function ChoiceTiles<Id extends string>({
  title,
  trigger,
  choices,
  value,
  columns = 2,
  onChoose,
  onClose,
}: ChoiceTilesProps<Id>) {
  return (
    <FloatingPopup
      open
      referenceRef={trigger as RefObject<HTMLElement>}
      placement="left-start"
      offset={12}
      autoClose={false}
      className="en-tiles-popup"
      chooserTitle={title}
      onClose={onClose}
    >
      <div
        className="en-tiles"
        role="group"
        aria-label={title}
        style={{ '--en-tile-columns': columns } as CSSProperties}
      >
        {choices.map((choice) => {
          const chosen = choice.id === value;
          return (
            <button
              key={choice.id}
              type="button"
              className="en-tile"
              aria-pressed={chosen}
              aria-disabled={choice.readOnly || undefined}
              onClick={() => {
                if (!choice.readOnly && !chosen) onChoose(choice.id);
              }}
            >
              <span className="en-tile-picture" aria-hidden="true">
                {choice.picture}
              </span>
              <span className="en-tile-label">{choice.label}</span>
            </button>
          );
        })}
      </div>
    </FloatingPopup>
  );
}
