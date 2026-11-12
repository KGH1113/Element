import React, {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react';
import FloatingPopup from '../../FloatingPopup';
import './CommonListPickerPopup.css';

type FilterOption = {
  value: string;
  label: string;
};

interface CommonListPickerPopupProps<T> {
  open: boolean;
  referenceRef: React.RefObject<HTMLElement>;
  panelElement?: HTMLElement | null;
  onClose: () => void;
  interactiveRefs?: Array<React.RefObject<HTMLElement>>;
  estimatedWidth?: number;
  estimatedHeight?: number;
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  searchPlaceholder: string;
  filterOptions?: FilterOption[];
  filterValue?: string;
  onFilterChange?: (value: string) => void;
  items: T[];
  getItemKey: (item: T) => React.Key;
  /** What the row says. */
  getItemLabel: (item: T) => string;
  /** How the name is drawn: a font's name in that font. */
  getItemLabelStyle?: (item: T) => React.CSSProperties | undefined;
  isItemSelected: (item: T) => boolean;
  onItemChoose: (item: T) => void;
  /** Things done to the item, at the end of its row (a ⋯ menu). */
  renderItemTrailing?: (item: T) => React.ReactNode;
  emptyText: string;
  isLoading?: boolean;
  loadingText?: string;
  errorText?: string;
  onAdd: () => void;
  /** Says what the row under the list adds. */
  addLabel: string;
  /** Names the chooser when the picker opens from the sidebar. */
  chooserTitle?: string;
}

const SearchIcon = () => (
  <svg
    width="14"
    height="14"
    viewBox="0 0 14 14"
    fill="none"
    aria-hidden="true"
  >
    <circle cx="6.2" cy="6.2" r="4.2" stroke="currentColor" strokeWidth="1.5" />
    <path
      d="m9.4 9.4 3 3"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
    />
  </svg>
);

const CheckIcon = () => (
  <svg
    width="14"
    height="14"
    viewBox="0 0 14 14"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="m3 7.5 2.5 2.5L11 4.5"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

const PlusIcon = () => (
  <svg
    width="14"
    height="14"
    viewBox="0 0 14 14"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="M7 2.5v9M2.5 7h9"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
    />
  </svg>
);

/**
 * One of a list to choose (a font, a sound, a motion), with a way to add
 * more. It is a chooser like the others of the sidebar: beside it, pointing
 * at its row. Inside, one search field, the kinds as a segmented control,
 * the list as rows that say which one is chosen, and the row that adds one
 * under them. Every list is drawn by this one, so all three look alike.
 */
export default function CommonListPickerPopup<T>({
  open,
  referenceRef,
  panelElement = null,
  onClose,
  interactiveRefs = [],
  estimatedWidth = 256,
  estimatedHeight = 380,
  searchQuery,
  onSearchQueryChange,
  searchPlaceholder,
  filterOptions,
  filterValue,
  onFilterChange,
  items,
  getItemKey,
  getItemLabel,
  getItemLabelStyle,
  isItemSelected,
  onItemChoose,
  renderItemTrailing,
  emptyText,
  isLoading = false,
  loadingText = '',
  errorText = '',
  onAdd,
  addLabel,
  chooserTitle,
}: CommonListPickerPopupProps<T>) {
  const containerRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const [fixedPosition, setFixedPosition] = useState<{
    x: number;
    y: number;
  } | null>(null);
  // Whether rows are hidden below the end of the list; it then fades out.
  const [moreBelow, setMoreBelow] = useState(false);

  // Outside the sidebar (an older settings panel) the chooser stands at the
  // panel's side, its foot near the panel's foot.
  const place = useCallback(() => {
    if (!panelElement) return;
    const panelRect = panelElement.getBoundingClientRect();
    const popupWidth = containerRef.current?.offsetWidth || estimatedWidth;
    const popupHeight = containerRef.current?.offsetHeight || estimatedHeight;
    const gap = 5;
    const padding = 5;
    const x = Math.max(padding, panelRect.left - popupWidth - gap);
    const y = Math.max(padding, panelRect.bottom - 20 - popupHeight);
    setFixedPosition((prev) =>
      prev && prev.x === x && prev.y === y ? prev : { x, y },
    );
  }, [panelElement, estimatedWidth, estimatedHeight]);

  useEffect(() => {
    if (!open) {
      setFixedPosition(null);
      return;
    }
    if (!panelElement) return;
    const frame = requestAnimationFrame(place);
    const container = containerRef.current;
    const observer = new ResizeObserver(place);
    if (container) observer.observe(container);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    };
  }, [open, panelElement, place]);

  const measureMore = useCallback(() => {
    const list = listRef.current;
    if (!list) return;
    setMoreBelow(list.scrollTop + list.clientHeight < list.scrollHeight - 2);
  }, []);

  useLayoutEffect(() => {
    if (open) measureMore();
  }, [open, items.length, filterValue, searchQuery, measureMore]);

  return (
    <FloatingPopup
      open={open}
      referenceRef={referenceRef}
      fixedX={fixedPosition?.x}
      fixedY={fixedPosition?.y}
      placement="right-start"
      offset={32}
      offsetY={fixedPosition ? 0 : -93}
      className="z-50"
      interactiveRefs={interactiveRefs}
      onClose={onClose}
      autoClose={false}
      chooserTitle={chooserTitle}
    >
      <div ref={containerRef} className="en-lc">
        <label className="en-lc-search">
          <SearchIcon />
          <input
            type="text"
            value={searchQuery}
            onChange={(event) => onSearchQueryChange(event.target.value)}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
          />
        </label>

        {filterOptions && filterValue !== undefined && onFilterChange && (
          <div className="en-lc-kinds" role="radiogroup">
            {filterOptions.map((option) => (
              <button
                key={option.value}
                type="button"
                role="radio"
                aria-checked={option.value === filterValue}
                className="en-lc-kind"
                onClick={() => onFilterChange(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>
        )}

        <div
          ref={listRef}
          className="en-lc-list"
          role="listbox"
          aria-label={chooserTitle}
          data-more={moreBelow || undefined}
          onScroll={measureMore}
        >
          {isLoading && items.length === 0 ? (
            <p className="en-lc-note">{loadingText}</p>
          ) : items.length === 0 ? (
            <p className="en-lc-note">{emptyText}</p>
          ) : (
            items.map((item) => {
              const selected = isItemSelected(item);
              const label = getItemLabel(item);
              const trailing = renderItemTrailing?.(item);
              return (
                <div
                  key={getItemKey(item)}
                  className="en-lc-row"
                  data-selected={selected || undefined}
                >
                  <button
                    type="button"
                    role="option"
                    aria-selected={selected}
                    className="en-lc-row-hit"
                    title={label}
                    onClick={() => onItemChoose(item)}
                  >
                    <span
                      className="en-lc-row-label"
                      style={getItemLabelStyle?.(item)}
                    >
                      {label}
                    </span>
                    {selected && (
                      <span className="en-lc-row-check">
                        <CheckIcon />
                      </span>
                    )}
                  </button>
                  {trailing && (
                    <span className="en-lc-row-trailing">{trailing}</span>
                  )}
                </div>
              );
            })
          )}
        </div>

        {errorText && (
          <p className="en-lc-note" role="alert" data-tone="error">
            {errorText}
          </p>
        )}

        <button type="button" className="en-lc-add" onClick={onAdd}>
          <span className="en-lc-add-icon">
            <PlusIcon />
          </span>
          <span className="en-lc-add-label">{addLabel}</span>
        </button>
      </div>
    </FloatingPopup>
  );
}
