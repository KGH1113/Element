import { useEffect, useRef, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useLenis } from '@hooks/useLenis';
import Alert from '../dialogs/Alert.jsx';
import TabNameModal from '../editors/TabNameModal';
import type { KeyViewerKind } from '@src/types/key/keys';
import { moveTabId, moveTabIdByOffset } from './tabOrder';
import {
  captureEditorHistorySnapshot,
  pushEditorHistorySnapshot,
} from '@stores/data/editorHistorySnapshot';

interface TabListProps {
  viewerKind: KeyViewerKind;
}

const MAX_TABS_PER_VIEWER = 30;
const VISIBLE_TAB_COUNT = 7;
const TAB_ROW_HEIGHT = 26;
const TAB_ROW_GAP = 4;
const TAB_ROW_PITCH = TAB_ROW_HEIGHT + TAB_ROW_GAP;
const SCROLL_CONTENT_GUTTER = 4;
const DRAG_THRESHOLD_PX = 4;
const TAB_LIST_MAX_HEIGHT =
  VISIBLE_TAB_COUNT * TAB_ROW_HEIGHT + (VISIBLE_TAB_COUNT - 1) * TAB_ROW_GAP;

interface TabDrag {
  id: string;
  /** 잡은 지점을 손가락 아래에 붙여 두기 위한 행 이동량 */
  offsetY: number;
}

const TabList = ({ viewerKind }: TabListProps) => {
  const allTabs = useKeyStore((state) => state.tabs);
  const tabs = allTabs.filter((tab) => tab.viewerKind === viewerKind);
  const selectedTabId = useKeyStore(
    (state) => state.selectedViewerTabs[viewerKind],
  );
  const { t } = useTranslation();

  const [showNameModal, setShowNameModal] = useState(false);
  const [renameTabId, setRenameTabId] = useState<string | null>(null);
  const [deleteTabId, setDeleteTabId] = useState<string | null>(null);
  const [draftOrder, setDraftOrder] = useState<string[] | null>(null);
  const [drag, setDrag] = useState<TabDrag | null>(null);
  const suppressClick = useRef(false);
  const rowsRef = useRef<HTMLDivElement>(null);
  const [hasOverflow, setHasOverflow] = useState(false);
  const selectedButtonRef = useRef<HTMLButtonElement>(null);

  const {
    scrollContainerRef: scrollRef,
    wrapperElement,
    lenisInstance,
    scrollbarWidth,
  } = useLenis();

  useEffect(() => {
    const rafId = requestAnimationFrame(() => {
      lenisInstance.current?.resize?.();
      selectedButtonRef.current?.focus();
    });
    return () => cancelAnimationFrame(rafId);
  }, [tabs.length, lenisInstance, selectedTabId]);

  useEffect(() => {
    const wrapper = wrapperElement;
    if (!wrapper) {
      setHasOverflow(false);
      return;
    }

    const updateOverflow = () => {
      const nextHasOverflow = wrapper.scrollHeight > wrapper.clientHeight;
      setHasOverflow((prev) =>
        prev === nextHasOverflow ? prev : nextHasOverflow,
      );
    };

    const rafId = requestAnimationFrame(updateOverflow);
    const resizeObserver = new ResizeObserver(updateOverflow);
    resizeObserver.observe(wrapper);

    const contentEl = wrapper.firstElementChild;
    if (contentEl instanceof HTMLElement) {
      resizeObserver.observe(contentEl);
    }

    return () => {
      cancelAnimationFrame(rafId);
      resizeObserver.disconnect();
    };
  }, [wrapperElement, tabs.length]);

  const maxReached = tabs.length >= MAX_TABS_PER_VIEWER;
  const canDelete = tabs.length > 1;
  const scrollbarCompensation = hasOverflow
    ? scrollbarWidth + SCROLL_CONTENT_GUTTER
    : 0;

  const handleCreate = async (name: string) => {
    const before = captureEditorHistorySnapshot();
    const result = await window.api.keys.tabs.create(viewerKind, name);
    if (!result.error) pushEditorHistorySnapshot(before);
    return result;
  };

  const handleRename = async (name: string) => {
    if (!renameTabId) return { error: 'not-found' };
    const before = captureEditorHistorySnapshot();
    const result = await window.api.keys.tabs.rename(
      viewerKind,
      renameTabId,
      name,
    );
    if (!result.error) pushEditorHistorySnapshot(before);
    return result;
  };

  const handleSelect = async (id: string) => {
    try {
      const result = await window.api.keys.tabs.select(viewerKind, id);
      return result;
    } catch (error) {
      console.error('Failed to select custom tab', error);
      return { success: false };
    }
  };

  const handleDelete = async (id: string) => {
    try {
      const before = captureEditorHistorySnapshot();
      const result = await window.api.keys.tabs.delete(id);
      if (!result?.success) {
        console.warn('Failed to delete custom tab', result?.error);
      } else {
        pushEditorHistorySnapshot(before);
      }
    } catch (error) {
      console.error('Failed to delete custom tab', error);
    }
  };

  const displayTabs = [...tabs].reverse();
  const displayedIds = displayTabs.map((tab) => tab.id);
  const visibleOrder = draftOrder ?? displayedIds;
  const orderedTabs = visibleOrder
    .map((id) => displayTabs.find((tab) => tab.id === id))
    .filter((tab): tab is (typeof displayTabs)[number] => Boolean(tab));

  const commitOrder = async (orderedIds: string[]) => {
    if (orderedIds.join('\0') === displayedIds.join('\0')) {
      setDraftOrder(null);
      return;
    }
    const before = captureEditorHistorySnapshot();
    try {
      const result = await window.api.keys.tabs.reorder(viewerKind, orderedIds);
      if (result.error) throw new Error(result.error);
      pushEditorHistorySnapshot(before);
    } catch (error) {
      console.error('Failed to reorder custom tabs', error);
    } finally {
      setDraftOrder(null);
    }
  };

  // 행 자체를 끌어 순서를 바꾼다. HTML5 드래그는 Tauri 웹뷰가 파일 드롭용으로
  // 가로채므로 포인터 이벤트로 직접 추적한다. 짧은 움직임은 클릭으로 남긴다.
  const beginDrag = (id: string, event: React.PointerEvent<HTMLElement>) => {
    suppressClick.current = false;
    if (event.button !== 0 || displayedIds.length < 2) return;
    if ((event.target as Element).closest('[data-tab-action="true"]')) return;

    const pointerId = event.pointerId;
    const startY = event.clientY;
    const rowTop = event.currentTarget.getBoundingClientRect().top;
    const grabOffset = startY - rowTop;
    let order = displayedIds;
    let active = false;

    const move = (moveEvent: PointerEvent) => {
      if (moveEvent.pointerId !== pointerId) return;
      if (!active && Math.abs(moveEvent.clientY - startY) < DRAG_THRESHOLD_PX)
        return;
      active = true;
      const listTop = rowsRef.current?.getBoundingClientRect().top ?? rowTop;
      // 행 높이가 고정이라 칸 번호를 좌표로 바로 구한다. 렌더 전의 DOM 순서를
      // 읽지 않으니 두 칸 사이에서 순서가 깜빡이지 않는다.
      const slot = Math.min(
        order.length - 1,
        Math.max(
          0,
          Math.round(
            (moveEvent.clientY - grabOffset - listTop) / TAB_ROW_PITCH,
          ),
        ),
      );
      const next = moveTabId(order, id, order[slot]);
      if (next !== order) {
        order = next;
        setDraftOrder(next);
      }
      setDrag({
        id,
        offsetY:
          moveEvent.clientY - grabOffset - (listTop + slot * TAB_ROW_PITCH),
      });
    };

    const end = (endEvent: PointerEvent) => {
      if (endEvent.pointerId !== pointerId) return;
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', end);
      window.removeEventListener('pointercancel', end);
      if (!active) return;
      setDrag(null);
      if (endEvent.type === 'pointerup') {
        suppressClick.current = true;
        void commitOrder(order);
      } else {
        setDraftOrder(null);
      }
    };

    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', end);
    window.addEventListener('pointercancel', end);
  };

  return (
    <div className="relative z-[1] flex w-[176px] flex-col gap-[4px] p-[6px]">
      {orderedTabs.length > 0 && (
        <div
          ref={scrollRef}
          className="flex w-full flex-col overflow-y-auto modal-content-scroll"
          style={{
            maxHeight: `${TAB_LIST_MAX_HEIGHT}px`,
            width:
              scrollbarCompensation > 0
                ? `calc(100% + ${scrollbarCompensation}px)`
                : undefined,
            marginRight:
              scrollbarCompensation > 0
                ? `-${scrollbarCompensation}px`
                : undefined,
          }}
        >
          <div
            ref={rowsRef}
            className="flex flex-col"
            style={{
              gap: `${TAB_ROW_GAP}px`,
              ...(hasOverflow
                ? { width: `calc(100% - ${SCROLL_CONTENT_GUTTER}px)` }
                : {}),
            }}
          >
            {orderedTabs.map((tab) => {
              const isSelected = selectedTabId === tab.id;
              const isDragging = drag?.id === tab.id;
              return (
                // 행 래퍼는 비인터랙티브로 두고, 선택은 행 전체를 덮는 버튼,
                // 이름 변경·삭제는 형제 버튼이 맡는다
                <div
                  key={tab.id}
                  data-tab-row={tab.id}
                  data-selected={isSelected ? 'true' : 'false'}
                  data-dragging={isDragging ? 'true' : undefined}
                  className={`group relative flex w-full shrink-0 select-none items-center rounded-[7px] px-[8px] text-style-2 ${
                    isDragging
                      ? 'z-[1] cursor-grabbing bg-ui-fill-active text-ui-fg-primary shadow-ui-popup'
                      : isSelected
                        ? 'bg-ui-fill-hover text-ui-fg-primary'
                        : 'text-[#DBDEE8] hover:bg-ui-fill'
                  }`}
                  style={{
                    height: `${TAB_ROW_HEIGHT}px`,
                    transform: isDragging
                      ? `translate3d(0, ${drag?.offsetY ?? 0}px, 0)`
                      : undefined,
                  }}
                  onPointerDown={(event) => beginDrag(tab.id, event)}
                >
                  <button
                    ref={isSelected ? selectedButtonRef : undefined}
                    type="button"
                    aria-label={tab.name}
                    aria-current={isSelected ? 'true' : undefined}
                    aria-keyshortcuts="Alt+ArrowUp Alt+ArrowDown"
                    className="absolute inset-0 rounded-[7px] focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-focus"
                    onClick={() => {
                      if (suppressClick.current) {
                        suppressClick.current = false;
                        return;
                      }
                      void handleSelect(tab.id);
                    }}
                    onKeyDown={(event) => {
                      if (!event.altKey) return;
                      const offset =
                        event.key === 'ArrowUp'
                          ? -1
                          : event.key === 'ArrowDown'
                            ? 1
                            : null;
                      if (!offset) return;
                      event.preventDefault();
                      void commitOrder(
                        moveTabIdByOffset(displayedIds, tab.id, offset),
                      );
                    }}
                  />
                  <span className="pointer-events-none relative min-w-0 flex-1 truncate text-left">
                    {tab.name}
                  </span>
                  <button
                    type="button"
                    aria-label={t('tabs.renameTitle')}
                    data-tab-action="true"
                    className="relative flex h-full w-[20px] shrink-0 items-center justify-center text-ui-fg-muted opacity-0 hover:text-ui-fg-primary focus-visible:opacity-100 focus-visible:outline-none group-hover:opacity-100 group-focus-within:opacity-100"
                    onClick={() => setRenameTabId(tab.id)}
                  >
                    <svg
                      width="10"
                      height="10"
                      viewBox="0 0 10 10"
                      fill="none"
                      aria-hidden="true"
                    >
                      <path
                        d="M6.8 1.5 8.5 3.2M1.5 8.5l.5-2 4.6-4.6a.7.7 0 0 1 1 0l.5.5a.7.7 0 0 1 0 1L3.5 8l-2 .5Z"
                        stroke="currentColor"
                        strokeWidth="1.2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                    </svg>
                  </button>
                  {canDelete && (
                    <button
                      type="button"
                      aria-label={t('tabs.delete')}
                      data-tab-action="true"
                      className="relative -mr-[6px] flex h-full w-[20px] shrink-0 items-center justify-center text-ui-fg-muted opacity-0 hover:text-ui-fg-primary focus-visible:opacity-100 focus-visible:outline-none group-hover:opacity-100 group-focus-within:opacity-100"
                      onClick={() => setDeleteTabId(tab.id)}
                    >
                      <svg
                        width="8"
                        height="8"
                        viewBox="0 0 8 8"
                        fill="none"
                        aria-hidden="true"
                      >
                        <path
                          d="M1 1l6 6M7 1 1 7"
                          stroke="currentColor"
                          strokeWidth="1.2"
                          strokeLinecap="round"
                        />
                      </svg>
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      {!maxReached && (
        <button
          type="button"
          className="flex w-full shrink-0 items-center gap-[6px] rounded-[7px] px-[8px] text-style-2 text-ui-fg-muted hover:bg-ui-fill hover:text-ui-fg-primary focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-focus"
          style={{ height: `${TAB_ROW_HEIGHT}px` }}
          onClick={() => setShowNameModal(true)}
        >
          <svg
            width="10"
            height="10"
            viewBox="0 0 10 10"
            fill="none"
            className="shrink-0"
            aria-hidden="true"
          >
            <path
              d="M5 1.5V8.5M1.5 5H8.5"
              stroke="currentColor"
              strokeWidth="1.2"
              strokeLinecap="round"
            />
          </svg>
          <span className="min-w-0 truncate">{t('tabs.createTitle')}</span>
        </button>
      )}

      <TabNameModal
        isOpen={showNameModal}
        onClose={() => setShowNameModal(false)}
        onSubmit={handleCreate}
        existingNames={tabs.map((tab) => tab.name)}
      />

      <TabNameModal
        isOpen={renameTabId !== null}
        mode="rename"
        initialName={tabs.find((tab) => tab.id === renameTabId)?.name ?? ''}
        onClose={() => setRenameTabId(null)}
        onSubmit={handleRename}
        existingNames={tabs
          .filter((tab) => tab.id !== renameTabId)
          .map((tab) => tab.name)}
      />

      <Alert
        isOpen={deleteTabId !== null}
        type="confirm"
        message={t('tabs.deleteConfirm', {
          name: tabs.find((tab) => tab.id === deleteTabId)?.name || '',
        })}
        confirmText={t('tabs.delete')}
        cancelText={t('common.cancel')}
        showCancel
        onConfirm={async () => {
          const id = deleteTabId;
          setDeleteTabId(null);
          if (id) await handleDelete(id);
        }}
        onCancel={() => setDeleteTabId(null)}
      />
    </div>
  );
};

export default TabList;
