import { useTranslation } from '@contexts/useTranslation';
import { useKeyStore } from '@stores/data/useKeyStore';
import type { KeyViewerKind } from '@src/types/key/keys';
import ViewerPreview from './ViewerPreview';
import { VIEWS, viewerShownIn } from './views';
import './viewers.css';

interface ViewerPickerProps {
  /** Opens the screen for choosing the key viewer a view shows. */
  onChoose: (view: KeyViewerKind) => void;
}

/**
 * Which key viewer the open view shows, as a value with an arrow: pressing it
 * chooses another one. It floats in the top corner of the canvas, so it takes
 * no row of its own and is read as part of what the canvas shows.
 */
export default function ViewerPicker({ onChoose }: ViewerPickerProps) {
  const { t } = useTranslation();
  const viewers = useKeyStore((state) => state.tabs);
  const editingId = useKeyStore((state) => state.selectedKeyType);
  const shownIds = useKeyStore((state) => state.selectedViewerTabs);
  const isReady = useKeyStore((state) => state.isBootstrapped);

  const view: KeyViewerKind =
    VIEWS.find(
      (kind) => viewerShownIn(kind, viewers, shownIds)?.id === editingId,
    ) ?? 'hand';
  const shown = viewerShownIn(view, viewers, shownIds);

  return (
    <div className="en-viewer-picker" data-canvas-cover>
      <span className="en-viewer-picker-label" id="en-viewer-picker-label">
        {t('viewers.shownLabel')}
      </span>
      <button
        type="button"
        className="en-picker"
        aria-labelledby="en-viewer-picker-label en-picker-value"
        aria-haspopup="dialog"
        title={t('viewers.changeLabel', { slot: t(`viewers.${view}`) })}
        disabled={!isReady}
        onClick={() => onChoose(view)}
      >
        <span className="en-picker-picture" aria-hidden="true">
          {shown && <ViewerPreview viewerId={shown.id} />}
        </span>
        <span className="en-picker-value" id="en-picker-value">
          {shown?.name ?? t('viewers.none')}
        </span>
        <svg
          className="en-picker-arrow"
          width="12"
          height="12"
          viewBox="0 0 12 12"
          fill="none"
          aria-hidden="true"
        >
          <path
            d="m2.5 4.5 3.5 3.5 3.5-3.5"
            stroke="currentColor"
            strokeWidth="1.5"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </button>
    </div>
  );
}
