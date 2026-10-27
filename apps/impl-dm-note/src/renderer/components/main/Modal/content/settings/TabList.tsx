import { useEffect, useRef, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import PlusIcon from '@assets/svgs/plus2.svg';
import MinusIcon from '@assets/svgs/minus.svg';
import { useKeyStore } from '@stores/data/useKeyStore';
import { useLenis } from '@hooks/useLenis';
import Alert from '../dialogs/Alert.jsx';
import TabNameModal from '../editors/TabNameModal';
import type { KeyViewerKind } from '@src/types/key/keys';
import ListPopup from '../../ListPopup';
import { moveTabId, moveTabIdByOffset } from './tabOrder';
import {
  captureEditorHistorySnapshot,
  pushEditorHistorySnapshot,
} from '@stores/data/editorHistorySnapshot';

interface TabListProps {
  viewerKind: KeyViewerKind;
}

const MAX_TABS_PER_VIEWER = 30;
const VISIBLE_TAB_COUNT = 5;
const TAB_ITEM_HEIGHT = 24;
const TAB_ITEM_GAP = 6;
const SCROLL_CONTENT_GUTTER = 4;
const TAB_LIST_MAX_HEIGHT =
  VISIBLE_TAB_COUNT * TAB_ITEM_HEIGHT + (VISIBLE_TAB_COUNT - 1) * TAB_ITEM_GAP;

const TabList = ({ viewerKind }: TabListProps) => {
  const allTabs = useKeyStore((state) => state.tabs);
  const tabs = allTabs.filter((tab) => tab.viewerKind === viewerKind);
  const selectedTabId = useKeyStore(
    (state) => state.selectedViewerTabs[viewerKind],
  );
  const { t } = useTranslation();

  const [askDelete, setAskDelete] = useState(false);
  const [showNameModal, setShowNameModal] = useState(false);
  const [renameTabId, setRenameTabId] = useState<string | null>(null);
  const [deleteTabId, setDeleteTabId] = useState<string | null>(null);
  const [contextMenu, setContextMenu] = useState<{
    id: string;
    x: number;
    y: number;
  } | null>(null);
  const [draftOrder, setDraftOrder] = useState<string[] | null>(null);
  const draggingId = useRef<string | null>(null);
  const dropCommitted = useRef(false);
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

  return (
    <div className="relative z-[1] flex max-w-[154px] flex-col items-center justify-center">
      <div className="min-h-[39px] w-full border-b-[1px] border-ui-line-subtle flex flex-col items-center justify-center p-[8px] gap-[8px]">
        <span className="w-full truncate text-left text-style-2 font-semibold text-[#F2F3F7]">
          {t(`tabs.${viewerKind}Viewer`)}
        </span>
        {tabs.length === 0 ? (
          <span className="text-style-2 text-[#DBDEE8]">{t('tabs.empty')}</span>
        ) : (
          <div
            ref={scrollRef}
            className="flex flex-col w-full gap-[6px] overflow-y-auto modal-content-scroll"
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
              className="flex flex-col gap-[6px]"
              style={
                hasOverflow
                  ? { width: `calc(100% - ${SCROLL_CONTENT_GUTTER}px)` }
                  : undefined
              }
            >
              {orderedTabs.map((tab) => (
                <div
                  key={tab.id}
                  className="flex w-full items-center gap-[3px]"
                  onDragOver={(event) => {
                    if (!draggingId.current) return;
                    event.preventDefault();
                    setDraftOrder((current) =>
                      moveTabId(
                        current ?? displayedIds,
                        draggingId.current as string,
                        tab.id,
                      ),
                    );
                  }}
                  onDrop={(event) => {
                    event.preventDefault();
                    dropCommitted.current = true;
                    void commitOrder(draftOrder ?? displayedIds);
                  }}
                >
                  <button
                    ref={
                      selectedTabId === tab.id ? selectedButtonRef : undefined
                    }
                    type="button"
                    aria-current={selectedTabId === tab.id ? 'true' : undefined}
                    data-selected={selectedTabId === tab.id ? 'true' : 'false'}
                    className={`relative min-w-0 flex-1 min-h-[24px] h-[24px] flex-shrink-0 flex items-center justify-center rounded-[7px] px-[24px] text-style-2 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-focus ${
                      selectedTabId === tab.id
                        ? 'bg-button-active text-[#FFFFFF] hover:bg-surfaceHover'
                        : 'text-[#DBDEE8] hover:bg-ui-fill active:bg-ui-fill-active'
                    }`}
                    onClick={() => handleSelect(tab.id)}
                    onContextMenu={(event) => {
                      event.preventDefault();
                      setContextMenu({
                        id: tab.id,
                        x: event.clientX,
                        y: event.clientY,
                      });
                    }}
                  >
                    {selectedTabId === tab.id && (
                      <svg
                        className="absolute left-[8px] h-[11px] w-[11px] text-focus"
                        viewBox="0 0 12 12"
                        fill="none"
                        aria-hidden="true"
                      >
                        <path
                          d="M2 6.2 4.55 8.6 10 3"
                          stroke="currentColor"
                          strokeWidth="1.8"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                        />
                      </svg>
                    )}
                    {tab.name}
                  </button>
                  <button
                    type="button"
                    draggable
                    aria-label={t('tabs.reorder', { name: tab.name })}
                    className="h-[24px] w-[16px] cursor-grab rounded text-[#8B8E99] hover:bg-ui-fill hover:text-white active:cursor-grabbing"
                    onDragStart={(event) => {
                      draggingId.current = tab.id;
                      dropCommitted.current = false;
                      setDraftOrder(displayedIds);
                      event.dataTransfer.effectAllowed = 'move';
                      event.dataTransfer.setData('text/plain', tab.id);
                    }}
                    onDragEnd={() => {
                      draggingId.current = null;
                      if (!dropCommitted.current) setDraftOrder(null);
                    }}
                    onKeyDown={(event) => {
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
                  >
                    ⋮
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
      <div className="flex flex-row p-[8px] w-[154px] gap-[8px]">
        {!maxReached && (
          <button
            type="button"
            aria-label={t('tabs.create')}
            className="flex flex-1 items-center justify-center max-w-[138px] h-[22px] rounded-[7px] bg-ui-fill hover:bg-ui-fill-hover active:bg-ui-fill-active"
            onClick={() => setShowNameModal(true)}
          >
            <PlusIcon />
          </button>
        )}
        {tabs.length > 0 && (
          <button
            type="button"
            aria-label={t('tabs.delete')}
            className={`flex flex-1 items-center justify-center max-w-[138px] h-[22px] rounded-[7px] ${
              tabs.length > 1
                ? 'bg-[#3C1E1E] hover:bg-[#442222] active:bg-[#522929]'
                : 'bg-ui-fill opacity-50 cursor-not-allowed'
            }`}
            disabled={tabs.length <= 1}
            onClick={() => {
              setDeleteTabId(selectedTabId);
              setAskDelete(true);
            }}
          >
            <MinusIcon />
          </button>
        )}
      </div>

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

      <ListPopup
        open={contextMenu !== null}
        position={contextMenu ?? undefined}
        onClose={() => setContextMenu(null)}
        items={[
          { id: 'rename', label: t('tabs.rename') },
          {
            id: 'delete',
            label: t('tabs.delete'),
            disabled: tabs.length <= 1,
          },
        ]}
        onSelect={(action) => {
          const id = contextMenu?.id;
          setContextMenu(null);
          if (!id) return;
          if (action === 'rename') setRenameTabId(id);
          if (action === 'delete') {
            setDeleteTabId(id);
            setAskDelete(true);
          }
        }}
      />

      <Alert
        isOpen={askDelete}
        type="confirm"
        message={t('tabs.deleteConfirm', {
          name: tabs.find((tab) => tab.id === deleteTabId)?.name || '',
        })}
        confirmText={t('tabs.delete')}
        cancelText={t('common.cancel')}
        showCancel
        onConfirm={async () => {
          setAskDelete(false);
          if (deleteTabId) await handleDelete(deleteTabId);
          setDeleteTabId(null);
        }}
        onCancel={() => {
          setAskDelete(false);
          setDeleteTabId(null);
        }}
      />
    </div>
  );
};

export default TabList;
