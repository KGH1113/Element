import { useTranslation } from '@contexts/useTranslation';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import type { KeyViewerKind } from '@src/types/key/keys';
import ViewerPreview from './ViewerPreview';
import { VIEWS, viewerShownIn } from './views';

/**
 * The two views of the editor, as tabs: hands and feet. A tab is a view of
 * the workspace: pressing one swaps the canvas and the sidebar under it to
 * that view, and does nothing else. It also says which key viewer the view
 * shows; that is changed on the canvas, with `ViewerPicker`.
 */
export default function ViewTabs() {
  const { t } = useTranslation();
  const viewers = useKeyStore((state) => state.tabs);
  const editingId = useKeyStore((state) => state.selectedKeyType);
  const shownIds = useKeyStore((state) => state.selectedViewerTabs);
  const open = useKeyStore((state) => state.setSelectedKeyType);
  const isReady = useKeyStore((state) => state.isBootstrapped);

  const current: KeyViewerKind =
    VIEWS.find(
      (view) => viewerShownIn(view, viewers, shownIds)?.id === editingId,
    ) ?? 'hand';

  return (
    <div className="en-tabs" role="tablist" aria-label={t('viewers.views')}>
      {VIEWS.map((view) => {
        const shown = viewerShownIn(view, viewers, shownIds);
        const selected = view === current;
        return (
          <button
            key={view}
            type="button"
            role="tab"
            aria-selected={selected}
            className="en-tab"
            // A view with no key viewer yet still opens: its bar offers one.
            disabled={!isReady || !shown}
            onClick={() => {
              if (selected || !shown) return;
              useGridSelectionStore.getState().clearSelection();
              open(shown.id);
            }}
          >
            <span className="en-tab-picture" aria-hidden="true">
              {shown && <ViewerPreview viewerId={shown.id} />}
            </span>
            <span className="en-tab-name">{t(`viewers.${view}`)}</span>
            <span className="en-tab-viewer">
              {shown?.name ?? t('viewers.none')}
            </span>
          </button>
        );
      })}
    </div>
  );
}
