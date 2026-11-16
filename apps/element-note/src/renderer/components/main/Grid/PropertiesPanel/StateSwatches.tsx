import { useEffect, type Ref } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { usePressedPreviewStore } from '@src/renderer/editor/runtime/pressedPreview';

/** A key looks one way at rest and another while it is pressed. */
export type LookState = 'idle' | 'active';

const STATES: readonly LookState[] = ['idle', 'active'];

const cssColor = (color: string): string =>
  color && /^(#|rgb)/.test(color) ? color : '#ffffff';

interface StateSwatchesProps {
  /**
   * The pair itself. The chooser is placed against it, and a press inside it
   * is not a press outside the chooser.
   */
  ref?: Ref<HTMLDivElement>;
  /** What is coloured ("Background"). */
  label: string;
  colors: Record<LookState, string>;
  /** Several elements are selected and do not agree on that color. */
  mixed?: Partial<Record<LookState, boolean>>;
  /**
   * What the second state is called. A key is pressed; a knob is turned, and
   * says so.
   */
  activeName?: string;
  /** The state whose chooser is open, when it is this pair's. */
  open: LookState | null;
  onPress: (state: LookState) => void;
}

/**
 * A color a key has twice: at rest and while pressed. Both are shown side by
 * side, each under its name, and pressing one opens its chooser. Nothing is
 * switched inside the chooser, so which of the two is being changed is always
 * in sight. While the pressed one is being chosen, the canvas draws the
 * selected keys as pressed.
 */
export default function StateSwatches({
  ref,
  label,
  colors,
  mixed,
  activeName,
  open,
  onPress,
}: StateSwatchesProps) {
  const { t } = useTranslation();
  const begin = usePressedPreviewStore((state) => state.begin);
  useEffect(() => (open === 'active' ? begin() : undefined), [open, begin]);

  return (
    <div ref={ref} className="en-states" role="group" aria-label={label}>
      {STATES.map((state) => {
        const name =
          state === 'idle'
            ? t('colorPicker.idle')
            : (activeName ?? t('colorPicker.active'));
        const isMixed = mixed?.[state] === true;
        return (
          <button
            key={state}
            type="button"
            className="en-state"
            data-look={state}
            aria-label={`${label}: ${name}`}
            aria-haspopup="dialog"
            aria-expanded={open === state}
            title={isMixed ? t('sidebar.stateMixed') : undefined}
            onClick={() => onPress(state)}
          >
            <span
              className="en-state-swatch"
              data-mixed={isMixed || undefined}
              style={
                isMixed ? undefined : { background: cssColor(colors[state]) }
              }
              aria-hidden="true"
            />
            <span className="en-state-name">{name}</span>
          </button>
        );
      })}
    </div>
  );
}
