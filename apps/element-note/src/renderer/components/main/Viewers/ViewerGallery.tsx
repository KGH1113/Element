import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import type { KeyViewerKind } from '@src/types/key/keys';
import {
  captureEditorHistorySnapshot,
  pushEditorHistorySnapshot,
} from '@stores/data/editorHistorySnapshot';
import ListPopup from '@components/main/Modal/ListPopup';
import Alert from '@components/main/Modal/content/dialogs/Alert';
import type { ShowConfirm } from '@components/main/Modal/content/dialogs/confirm';
import { usePresetActions } from '@hooks/app/usePresetActions';
import ViewerThumbnail from './ViewerThumbnail';
import { useThumbnailScale } from './thumbnailScale';
import { moveTabId, moveTabIdByOffset } from './viewerOrder';
import './viewers.css';

const MAX_VIEWERS_PER_SLOT = 30;
const MAX_NAME_LENGTH = 10;
const DRAG_THRESHOLD_PX = 5;

const menuIcon = (path: React.ReactNode) => (
  <svg
    width="14"
    height="14"
    viewBox="0 0 14 14"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.3"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    {path}
  </svg>
);

const MENU_ICONS = {
  rename: menuIcon(<path d="M9.4 2.4 11.6 4.6 5 11.2l-2.7.5.5-2.7 6.6-6.6Z" />),
  resetCounters: menuIcon(<ellipse cx="7" cy="7" rx="2.9" ry="4.4" />),
  reset: menuIcon(
    <>
      <path d="M2.6 7a4.4 4.4 0 1 0 1.4-3.2" />
      <path d="M2.4 2.2v2.6H5" />
    </>,
  ),
  delete: menuIcon(
    <>
      <path d="M2.6 4h8.8M5.5 4V2.6h3V4" />
      <path d="m3.7 4 .5 7.4h5.6l.5-7.4" />
    </>,
  ),
  // Into a file: an arrow going down into a tray. Out of one: coming up.
  saveFile: menuIcon(
    <>
      <path d="M7 2.2v6.2M4.6 6.2 7 8.6l2.4-2.4" />
      <path d="M2.6 9.4v1.4c0 .5.4.9.9.9h7c.5 0 .9-.4.9-.9V9.4" />
    </>,
  ),
  loadFile: menuIcon(
    <>
      <path d="M7 8.6V2.4M4.6 4.6 7 2.2l2.4 2.4" />
      <path d="M2.6 9.4v1.4c0 .5.4.9.9.9h7c.5 0 .9-.4.9-.9V9.4" />
    </>,
  ),
};

interface ViewerGalleryProps {
  /** The slot (hands or feet) whose key viewer is being chosen. */
  slot: KeyViewerKind;
  onClose: () => void;
  showConfirm: ShowConfirm;
  showAlert: (message: string) => void;
}

interface CardDrag {
  id: string;
  order: string[];
  offsetX: number;
  offsetY: number;
}

/** The card whose name is being typed: a new one, or one being renamed. */
type Naming = { mode: 'create' } | { mode: 'rename'; id: string };

/**
 * The key viewers made for one slot (hands or feet), as pictures. The slot
 * has one of them up; pressing another puts that one up instead and opens it
 * on the canvas. Only the slot that is being changed is shown. A name is
 * typed on the card itself, for a new key viewer and for renaming one.
 *
 * Key viewers are also kept as files from here: one of them from its card's
 * menu, all of them (hands and feet, with their settings) from the head.
 */
export default function ViewerGallery({
  slot,
  onClose,
  showConfirm,
  showAlert,
}: ViewerGalleryProps) {
  const { t } = useTranslation();
  const files = usePresetActions(showAlert);
  const viewers = useKeyStore((state) => state.tabs);
  const editingId = useKeyStore((state) => state.selectedKeyType);
  const shownIds = useKeyStore((state) => state.selectedViewerTabs);
  const edit = useKeyStore((state) => state.setSelectedKeyType);

  const [menuFor, setMenuFor] = useState<string | null>(null);
  const [naming, setNaming] = useState<Naming | null>(null);
  const [nameError, setNameError] = useState<string | null>(null);
  const savingName = useRef(false);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [drag, setDrag] = useState<CardDrag | null>(null);
  const menuButtonRef = useRef<HTMLElement | null>(null);
  const suppressClick = useRef(false);

  // Cards that change place glide there instead of jumping: the others while
  // one is dragged past them, and the dragged one when it is let go.
  const gridRef = useRef<HTMLDivElement | null>(null);
  const cardPlaces = useRef(new Map<string, { left: number; top: number }>());
  const lastDrag = useRef<CardDrag | null>(null);
  useThumbnailScale(gridRef);
  useLayoutEffect(() => {
    const grid = gridRef.current;
    if (!grid) return;
    const still =
      typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const dropped = drag ? null : lastDrag.current;
    const places = new Map<string, { left: number; top: number }>();
    grid.querySelectorAll<HTMLElement>('[data-viewer-id]').forEach((card) => {
      const id = card.dataset.viewerId ?? '';
      const place = { left: card.offsetLeft, top: card.offsetTop };
      places.set(id, place);
      if (still || typeof card.animate !== 'function') return;
      const before = cardPlaces.current.get(id);
      const from =
        dropped?.id === id
          ? { x: dropped.offsetX, y: dropped.offsetY }
          : before && id !== drag?.id
            ? { x: before.left - place.left, y: before.top - place.top }
            : null;
      if (!from || (from.x === 0 && from.y === 0)) return;
      card.animate(
        [
          { transform: `translate3d(${from.x}px, ${from.y}px, 0)` },
          { transform: 'translate3d(0, 0, 0)' },
        ],
        { duration: 220, easing: 'cubic-bezier(0.65, 0, 0.35, 1)' },
      );
    });
    cardPlaces.current = places;
    lastDrag.current = drag;
  });

  // Escape returns to the editor, unless a menu or a dialog is open over it.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || event.defaultPrevented) return;
      if (
        document.querySelector('.dmn-popup-surface, [data-dmn-modal-backdrop]')
      )
        return;
      onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const viewersOf = (kind: KeyViewerKind) =>
    viewers.filter((viewer) => viewer.viewerKind === kind);

  const putUp = (id: string) => {
    if (id !== editingId) {
      useGridSelectionStore.getState().clearSelection();
      edit(id);
    }
    onClose();
  };

  const saveOrder = async (order: string[]) => {
    const current = viewersOf(slot).map((viewer) => viewer.id);
    if (order.join('\0') === current.join('\0')) return;
    const before = captureEditorHistorySnapshot();
    try {
      // The backend takes the order last one first.
      const result = await window.api.keys.tabs.reorder(
        slot,
        [...order].reverse(),
      );
      if (result.error) throw new Error(result.error);
      pushEditorHistorySnapshot(before);
    } catch (error) {
      console.error('Failed to reorder key viewers', error);
    }
  };

  // A card follows the pointer by the distance dragged and takes the place of
  // the card it is closest to. HTML5 drag is taken by the webview for file
  // drops, so pointer events track it directly.
  const beginDrag = (id: string, event: React.PointerEvent<HTMLElement>) => {
    suppressClick.current = false;
    const ids = viewersOf(slot).map((viewer) => viewer.id);
    if (event.button !== 0 || ids.length < 2) return;
    const grid = event.currentTarget.closest('.en-gallery-grid');
    if (!grid) return;
    const boxes = ids.map((viewerId) =>
      grid
        .querySelector(`[data-viewer-id="${viewerId}"]`)
        ?.getBoundingClientRect(),
    );
    const selfIndex = ids.indexOf(id);
    const self = boxes[selfIndex];
    if (!self || boxes.some((box) => !box)) return;

    const pointerId = event.pointerId;
    const startX = event.clientX;
    const startY = event.clientY;
    let order = ids;
    let active = false;

    const move = (moveEvent: PointerEvent) => {
      if (moveEvent.pointerId !== pointerId) return;
      const dx = moveEvent.clientX - startX;
      const dy = moveEvent.clientY - startY;
      if (!active && Math.hypot(dx, dy) < DRAG_THRESHOLD_PX) return;
      active = true;

      // Cards are one size, so place `i` of the grid is where card `i` was.
      const centerX = self.left + self.width / 2 + dx;
      const centerY = self.top + self.height / 2 + dy;
      let place = selfIndex;
      let nearest = Infinity;
      boxes.forEach((box, index) => {
        const distance = Math.hypot(
          box!.left + box!.width / 2 - centerX,
          box!.top + box!.height / 2 - centerY,
        );
        if (distance < nearest) {
          nearest = distance;
          place = index;
        }
      });
      order = moveTabId(ids, id, ids[place]);
      setDrag({
        id,
        order,
        offsetX: self.left + dx - boxes[place]!.left,
        offsetY: self.top + dy - boxes[place]!.top,
      });
    };

    const end = (endEvent: PointerEvent) => {
      if (endEvent.pointerId !== pointerId) return;
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', end);
      window.removeEventListener('pointercancel', end);
      if (!active) return;
      suppressClick.current = true;
      const finish = () => setDrag(null);
      if (endEvent.type === 'pointerup') {
        void saveOrder(order).finally(finish);
      } else {
        finish();
      }
    };

    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', end);
    window.addEventListener('pointercancel', end);
  };

  const startNaming = (next: Naming) => {
    setNameError(null);
    setNaming(next);
  };
  const stopNaming = () => {
    setNameError(null);
    setNaming(null);
  };

  // Enter keeps the field open on a problem and says what to fix. Leaving the
  // field saves a usable name and otherwise drops the edit without a word.
  const saveName = async (typed: string, by: 'enter' | 'leave') => {
    if (!naming || savingName.current) return;
    const name = typed.trim();
    const selfId = naming.mode === 'rename' ? naming.id : null;
    const others = viewersOf(slot).filter((viewer) => viewer.id !== selfId);
    const current = viewers.find((viewer) => viewer.id === selfId)?.name;
    if (name === current || (!name && by === 'leave')) {
      stopNaming();
      return;
    }
    const problem = !name
      ? t('tabs.name.required')
      : name.length > MAX_NAME_LENGTH
        ? t('tabs.name.max')
        : others.some((viewer) => viewer.name === name)
          ? t('tabs.name.duplicate')
          : null;
    if (problem) {
      if (by === 'leave') stopNaming();
      else setNameError(problem);
      return;
    }

    savingName.current = true;
    try {
      const before = captureEditorHistorySnapshot();
      const result =
        naming.mode === 'create'
          ? await window.api.keys.tabs.create(slot, name)
          : await window.api.keys.tabs.rename(slot, naming.id, name);
      if (result?.error) {
        const messages: Record<string, string> = {
          'max-reached': t('tabs.errors.max'),
          'duplicate-name': t('tabs.name.duplicate'),
          'invalid-name': t('tabs.errors.invalid'),
          'name-too-long': t('tabs.name.max'),
          'not-found': t('tabs.errors.notFound'),
        };
        setNameError(messages[result.error] ?? t('tabs.errors.createFail'));
        return;
      }
      pushEditorHistorySnapshot(before);
      stopNaming();
    } catch (error) {
      console.error('Failed to save the key viewer name', error);
      setNameError(t('tabs.errors.createFail'));
    } finally {
      savingName.current = false;
    }
  };

  const nameField = (initial: string, label: string) => (
    <span className="en-card-name-field">
      <input
        className="en-card-name-input"
        autoFocus
        defaultValue={initial}
        maxLength={MAX_NAME_LENGTH}
        placeholder={t('tabs.name.placeholder')}
        aria-label={label}
        aria-invalid={nameError ? true : undefined}
        spellCheck={false}
        onFocus={(event) => event.currentTarget.select()}
        onChange={() => nameError && setNameError(null)}
        onBlur={(event) => void saveName(event.currentTarget.value, 'leave')}
        onKeyDown={(event) => {
          // Enter that only confirms a composed character is not a save.
          if (event.nativeEvent.isComposing) return;
          if (event.key === 'Enter') {
            event.preventDefault();
            void saveName(event.currentTarget.value, 'enter');
          } else if (event.key === 'Escape') {
            // Leaves the name, not the gallery.
            event.preventDefault();
            event.stopPropagation();
            stopNaming();
          }
        }}
      />
      {/* Lies over the gap below the card, so nothing moves. */}
      {nameError && (
        <span className="en-card-name-error" role="alert">
          {nameError}
        </span>
      )}
    </span>
  );

  const menuViewer = viewers.find((viewer) => viewer.id === menuFor);
  const deleteViewer = viewers.find((viewer) => viewer.id === deleteId);

  const slotViewers = viewersOf(slot);
  const order = drag ? drag.order : slotViewers.map((viewer) => viewer.id);
  const ordered = order
    .map((id) => slotViewers.find((viewer) => viewer.id === id))
    .filter((viewer): viewer is (typeof slotViewers)[number] =>
      Boolean(viewer),
    );
  const slotName = t(`viewers.${slot}`);

  return (
    <div className="en-gallery">
      <header className="en-gallery-head">
        <button
          type="button"
          className="en-settings-leave"
          aria-label={t('viewers.back')}
          title={t('viewers.back')}
          onClick={onClose}
        >
          <svg
            width="16"
            height="16"
            viewBox="0 0 16 16"
            fill="none"
            aria-hidden="true"
          >
            <path
              d="M10 3.5 5.5 8l4.5 4.5"
              stroke="currentColor"
              strokeWidth="1.7"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
        <h1>{t('viewers.title', { slot: slotName })}</h1>
        <div className="en-gallery-files">
          <button
            type="button"
            className="en-gallery-file"
            title={t('viewers.loadAllHelp')}
            onClick={() => void files.loadAll()}
          >
            {MENU_ICONS.loadFile}
            {t('viewers.loadAll')}
          </button>
          <button
            type="button"
            className="en-gallery-file"
            title={t('viewers.saveAllHelp')}
            onClick={() => void files.saveAll()}
          >
            {MENU_ICONS.saveFile}
            {t('viewers.saveAll')}
          </button>
        </div>
      </header>

      <div className="en-gallery-body">
        <div ref={gridRef} className="en-gallery-grid">
          {ordered.map((viewer, place) => {
            const shown = shownIds[slot] === viewer.id;
            const dragging = drag?.id === viewer.id;
            const renaming =
              naming?.mode === 'rename' && naming.id === viewer.id;
            return (
              <div
                key={viewer.id}
                className="en-card"
                data-viewer-id={viewer.id}
                data-shown={shown || undefined}
                data-dragging={dragging || undefined}
                style={
                  {
                    '--card': place,
                    ...(dragging && {
                      transform: `translate3d(${drag.offsetX}px, ${drag.offsetY}px, 0)`,
                    }),
                  } as React.CSSProperties
                }
              >
                <button
                  type="button"
                  className="en-card-open"
                  aria-label={t('viewers.putUpLabel', {
                    slot: slotName,
                    name: viewer.name,
                  })}
                  aria-current={shown ? 'true' : undefined}
                  aria-keyshortcuts="Alt+ArrowLeft Alt+ArrowRight"
                  onPointerDown={(event) => beginDrag(viewer.id, event)}
                  onClick={() => {
                    if (suppressClick.current) {
                      suppressClick.current = false;
                      return;
                    }
                    putUp(viewer.id);
                  }}
                  onKeyDown={(event) => {
                    if (!event.altKey) return;
                    const step =
                      event.key === 'ArrowLeft'
                        ? -1
                        : event.key === 'ArrowRight'
                          ? 1
                          : null;
                    if (!step) return;
                    event.preventDefault();
                    void saveOrder(
                      moveTabIdByOffset(
                        slotViewers.map((item) => item.id),
                        viewer.id,
                        step,
                      ),
                    );
                  }}
                >
                  <span className="en-card-picture">
                    <ViewerThumbnail viewerId={viewer.id} />
                  </span>
                </button>
                <div className="en-card-foot">
                  {renaming ? (
                    nameField(viewer.name, t('viewers.rename'))
                  ) : (
                    <span className="en-card-name" title={viewer.name}>
                      {viewer.name}
                    </span>
                  )}
                  <button
                    type="button"
                    className="en-card-more"
                    aria-label={t('viewers.menu', { name: viewer.name })}
                    aria-haspopup="menu"
                    aria-expanded={menuFor === viewer.id}
                    onClick={(event) => {
                      menuButtonRef.current = event.currentTarget;
                      setMenuFor((current) =>
                        current === viewer.id ? null : viewer.id,
                      );
                    }}
                  >
                    <svg
                      width="14"
                      height="14"
                      viewBox="0 0 14 14"
                      fill="currentColor"
                      aria-hidden="true"
                    >
                      <circle cx="3" cy="7" r="1.1" />
                      <circle cx="7" cy="7" r="1.1" />
                      <circle cx="11" cy="7" r="1.1" />
                    </svg>
                  </button>
                </div>
              </div>
            );
          })}
          {naming?.mode === 'create' ? (
            // The new key viewer is named where its card will be.
            <div
              className="en-card en-card-draft"
              style={{ '--card': ordered.length } as React.CSSProperties}
            >
              <span className="en-card-picture" />
              <div className="en-card-foot">
                {nameField('', t('viewers.new'))}
              </div>
            </div>
          ) : (
            <button
              type="button"
              className="en-card-new"
              style={{ '--card': ordered.length } as React.CSSProperties}
              disabled={slotViewers.length >= MAX_VIEWERS_PER_SLOT}
              onClick={() => startNaming({ mode: 'create' })}
            >
              <span className="en-card-picture">
                <svg
                  width="18"
                  height="18"
                  viewBox="0 0 18 18"
                  fill="none"
                  aria-hidden="true"
                >
                  <path
                    d="M9 3.5v11M3.5 9h11"
                    stroke="currentColor"
                    strokeWidth="1.6"
                    strokeLinecap="round"
                  />
                </svg>
              </span>
              <span className="en-card-foot">
                <span className="en-card-name">{t('viewers.new')}</span>
              </span>
            </button>
          )}
        </div>
      </div>

      <ListPopup
        open={!!menuViewer}
        referenceRef={menuButtonRef as React.RefObject<HTMLElement>}
        placement="bottom-end"
        offset={6}
        textAlign="left"
        onClose={() => setMenuFor(null)}
        items={[
          {
            id: 'rename',
            label: t('viewers.rename'),
            icon: MENU_ICONS.rename,
          },
          { id: 'separator-file', label: '', type: 'separator' },
          {
            id: 'saveFile',
            label: t('viewers.saveFile'),
            icon: MENU_ICONS.saveFile,
          },
          {
            id: 'loadFile',
            label: t('viewers.loadFile'),
            icon: MENU_ICONS.loadFile,
          },
          { id: 'separator-reset', label: '', type: 'separator' },
          {
            id: 'resetCounters',
            label: t('viewers.resetCounters'),
            icon: MENU_ICONS.resetCounters,
          },
          {
            id: 'reset',
            label: t('viewers.reset'),
            icon: MENU_ICONS.reset,
          },
          { id: 'separator-delete', label: '', type: 'separator' },
          {
            id: 'delete',
            label: t('viewers.delete'),
            icon: MENU_ICONS.delete,
            tone: 'danger',
            disabled: slotViewers.length < 2,
          },
        ]}
        onSelect={(id) => {
          const viewer = menuViewer;
          setMenuFor(null);
          if (!viewer) return;
          if (id === 'rename') {
            startNaming({ mode: 'rename', id: viewer.id });
          } else if (id === 'saveFile') {
            void files.saveTab(viewer.id);
          } else if (id === 'loadFile') {
            void files.loadTab(viewer.id);
          } else if (id === 'resetCounters') {
            showConfirm(
              t('viewers.resetCountersConfirm', { name: viewer.name }),
              () => void window.api.keys.resetCountersMode(viewer.id),
              { confirmText: t('confirm.reset'), danger: true },
            );
          } else if (id === 'reset') {
            showConfirm(
              t('viewers.resetConfirm', { name: viewer.name }),
              () => void window.api.keys.resetMode(viewer.id),
              { confirmText: t('confirm.reset'), danger: true },
            );
          } else if (id === 'delete') {
            setDeleteId(viewer.id);
          }
        }}
      />

      <Alert
        isOpen={deleteId !== null}
        type="confirm"
        message={t('tabs.deleteConfirm', { name: deleteViewer?.name ?? '' })}
        confirmText={t('tabs.delete')}
        cancelText={t('common.cancel')}
        danger
        onConfirm={async () => {
          const id = deleteId;
          setDeleteId(null);
          if (!id) return;
          const before = captureEditorHistorySnapshot();
          try {
            const result = await window.api.keys.tabs.delete(id);
            if (result?.success) pushEditorHistorySnapshot(before);
            else console.warn('Failed to delete key viewer', result?.error);
          } catch (error) {
            console.error('Failed to delete key viewer', error);
          }
        }}
        onCancel={() => setDeleteId(null)}
      />
    </div>
  );
}
