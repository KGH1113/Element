import React, {
  createContext,
  useContext,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react';
import { createPortal } from 'react-dom';
import FloatingPopup from './FloatingPopup';
import { useLenis } from '@hooks/useLenis';

export type ListItem = {
  id: string;
  label: string;
  disabled?: boolean;
  /** 구분선 항목 */
  type?: 'item' | 'separator';
  /** 토글 항목의 체크 상태 */
  checked?: boolean;
  /** 서브메뉴 항목 */
  children?: ListItem[];
  /** 서브메뉴 최대 표시 항목 수 (초과 시 스크롤) */
  maxVisibleChildren?: number;
};

interface ListPopupProps {
  open: boolean;
  referenceRef?: React.RefObject<HTMLElement>;
  position?: { x: number; y: number };
  onClose?: () => void;
  items: ListItem[];
  onSelect?: (id: string) => void;
  className?: string;
  offsetX?: number;
  offsetY?: number;
  /** 텍스트 정렬 방향 */
  textAlign?: 'left' | 'center';
  /** 최대 표시 항목 수 (초과 시 스크롤) */
  maxVisibleItems?: number;
}

// 부모 메뉴가 닫히기 시작하면 body로 빠져나간 서브메뉴도 함께 걷는다.
const ListPopupOpenContext = createContext(true);

const SUBMENU_GAP = 2;
const SUBMENU_EDGE_PADDING = 5;

/** 서브메뉴 컴포넌트 (호버 시 표시) */
const SubMenu = ({
  items,
  onSelect,
  onCloseAll,
  textAlign = 'left',
  maxVisibleItems,
  anchorRect,
}: {
  items: ListItem[];
  onSelect?: (id: string) => void;
  onCloseAll?: () => void;
  textAlign?: 'left' | 'center';
  maxVisibleItems?: number;
  anchorRect: DOMRect | null;
}) => {
  const subMenuRef = useRef<HTMLDivElement>(null);
  const siblingActiveRef = useRef<{
    id: string | null;
    close: (() => void) | null;
  }>({ id: null, close: null });
  const parentOpen = useContext(ListPopupOpenContext);
  // 실제 크기를 페인트 전에 재서 배치한다. 추정치로는 긴 라벨이 화면 밖으로 밀린다.
  const [pos, setPos] = useState<{
    left?: number;
    right?: number;
    top: number;
  } | null>(null);

  useLayoutEffect(() => {
    const el = subMenuRef.current;
    if (!anchorRect || !el) return;
    const { offsetWidth: width, offsetHeight: height } = el;
    const normalLeft = anchorRect.right + SUBMENU_GAP;
    const flipToLeft =
      normalLeft + width > window.innerWidth - SUBMENU_EDGE_PADDING &&
      anchorRect.left - SUBMENU_GAP - width >= SUBMENU_EDGE_PADDING;
    const top = Math.max(
      SUBMENU_EDGE_PADDING,
      Math.min(
        anchorRect.top,
        window.innerHeight - height - SUBMENU_EDGE_PADDING,
      ),
    );
    // 측정 후 페인트 전에 위치를 확정하는 패턴이라 동기 setState가 의도다.
    setPos(
      flipToLeft
        ? { right: window.innerWidth - anchorRect.left + SUBMENU_GAP, top }
        : {
            left: Math.min(
              normalLeft,
              window.innerWidth - width - SUBMENU_EDGE_PADDING,
            ),
            top,
          },
    );
  }, [anchorRect, items.length]);

  const itemHeight = 24;
  const separatorCount = items.filter((i) => i.type === 'separator').length;
  const normalItemCount = items.length - separatorCount;
  const effectiveMax = maxVisibleItems ?? normalItemCount;
  const needsScroll = normalItemCount > effectiveMax;
  const maxHeight = needsScroll
    ? effectiveMax * itemHeight + separatorCount * 9 + 10
    : undefined;

  const { scrollContainerRef: subLenisRef } = useLenis({
    duration: 0.5,
    wheelMultiplier: 0.7,
  });

  if (!anchorRect || !parentOpen) return null;

  // body 포털 필수: 부모 팝업의 transform과 backdrop-filter가 fixed 요소의
  // 기준 상자가 되어 뷰포트 좌표가 팝업 기준으로 밀려 창 밖에 그려진다.
  // React 트리상으로는 여전히 부모 행의 자식이라 행의 mouseenter/leave가
  // 서브메뉴 위에서도 끊기지 않는다.
  return createPortal(
    <div
      ref={(node) => {
        (subMenuRef as React.MutableRefObject<HTMLDivElement | null>).current =
          node;
        if (needsScroll) subLenisRef(node);
      }}
      data-dmn-app-portal
      data-dmn-popup-submenu="true"
      role="menu"
      className={`fixed z-[10001] bg-ui-glass rounded-ui-surface shadow-ui-popup p-[5px] flex flex-col gap-[1px] tooltip-fade-in${
        needsScroll ? ' listpopup-scroll' : ''
      }`}
      style={{
        left: pos?.left,
        right: pos?.right,
        top: pos?.top ?? 0,
        visibility: pos ? undefined : 'hidden',
        ...(maxHeight
          ? { maxHeight, overflowY: 'auto', overflowX: 'hidden' }
          : {}),
      }}
    >
      {items.map((it) => (
        <MenuItemRow
          key={it.id}
          item={it}
          textAlign={textAlign}
          onSelect={onSelect}
          onCloseAll={onCloseAll}
          siblingActiveRef={siblingActiveRef}
        />
      ))}
    </div>,
    document.body,
  );
};

/** 개별 메뉴 항목 행 */
const MenuItemRow = ({
  item,
  textAlign,
  onSelect,
  onCloseAll,
  siblingActiveRef,
}: {
  item: ListItem;
  textAlign: 'left' | 'center';
  onSelect?: (id: string) => void;
  onCloseAll?: () => void;
  /** 형제 항목 중 활성 서브메뉴를 추적하는 ref (즉시 전환용) */
  siblingActiveRef?: React.RefObject<{
    id: string | null;
    close: (() => void) | null;
  }>;
}) => {
  const [subMenuOpen, setSubMenuOpen] = useState(false);
  const rowRef = useRef<HTMLButtonElement>(null);
  const hoverTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [rowRect, setRowRect] = useState<DOMRect | null>(null);

  const hasChildren = item.children && item.children.length > 0;

  const handleMouseEnter = () => {
    if (!hasChildren || item.disabled) return;
    if (hoverTimerRef.current) clearTimeout(hoverTimerRef.current);
    const active = siblingActiveRef?.current;
    const hasActiveSibling = active?.id != null;
    const delay = hasActiveSibling ? 0 : 150;

    // 다른 형제의 서브메뉴가 열려있으면 즉시 닫기
    if (hasActiveSibling && active?.id !== item.id && active?.close) {
      active.close();
    }

    hoverTimerRef.current = setTimeout(() => {
      if (rowRef.current) {
        setRowRect(rowRef.current.getBoundingClientRect());
      }
      setSubMenuOpen(true);
      if (siblingActiveRef) {
        siblingActiveRef.current = {
          id: item.id,
          close: () => setSubMenuOpen(false),
        };
      }
    }, delay);
  };

  const handleMouseLeave = () => {
    if (hoverTimerRef.current) clearTimeout(hoverTimerRef.current);
    hoverTimerRef.current = setTimeout(() => {
      setSubMenuOpen(false);
      if (siblingActiveRef?.current.id === item.id) {
        siblingActiveRef.current = { id: null, close: null };
      }
    }, 200);
  };

  useEffect(() => {
    return () => {
      if (hoverTimerRef.current) clearTimeout(hoverTimerRef.current);
    };
  }, []);

  // 구분선 (부모 p-[5px] 패딩을 무시하고 전체 폭 사용)
  if (item.type === 'separator') {
    return (
      <div className="-mx-[5px] py-[3px]">
        <div className="h-[1px] bg-ui-line-subtle" />
      </div>
    );
  }

  const isLeft = textAlign === 'left';
  const hasCheck = typeof item.checked === 'boolean';
  const isBasicCenterItem = !isLeft && !hasCheck && !hasChildren;

  const handleSelect = () => {
    if (item.disabled || hasChildren) return;
    onSelect?.(item.id);
    onCloseAll?.();
  };

  if (isBasicCenterItem) {
    return (
      <button
        type="button"
        role="menuitem"
        disabled={item.disabled}
        onClick={handleSelect}
        className={`w-full min-w-[108px] h-[24px] px-[24px] rounded-[7px] flex items-center justify-center ${
          item.disabled
            ? 'opacity-70'
            : 'hover:bg-ui-fill active:bg-ui-fill-active cursor-pointer'
        }`}
      >
        <span
          className={`text-style-2 whitespace-nowrap ${
            item.disabled ? 'text-[#6B6E7B]' : 'text-[#DBDEE8]'
          }`}
        >
          {item.label}
        </span>
      </button>
    );
  }

  return (
    <div
      className="relative"
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      <button
        ref={rowRef}
        type="button"
        role="menuitem"
        disabled={item.disabled}
        onClick={handleSelect}
        className={`w-full min-w-[120px] h-[24px] px-[6px] rounded-[5px] flex items-center gap-[4px] ${
          item.disabled
            ? 'opacity-70'
            : 'hover:bg-ui-fill active:bg-ui-fill-active cursor-pointer'
        }`}
      >
        {/* 좌측 체크 영역 (고정 너비) */}
        <span className="w-[16px] flex-shrink-0 flex items-center justify-center">
          {hasCheck && item.checked && (
            <svg
              width="12"
              height="12"
              viewBox="0 0 12 12"
              fill="none"
              className="text-[#DBDEE8]"
            >
              <path
                d="M2 6.5L4.5 9L10 3"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          )}
        </span>

        {/* 라벨 텍스트 */}
        <span
          className={`flex-1 text-style-2 whitespace-nowrap ${
            isLeft ? 'text-left' : 'text-center'
          } ${item.disabled ? 'text-[#6B6E7B]' : 'text-[#DBDEE8]'}`}
        >
          {item.label}
        </span>

        {/* 우측 서브메뉴 화살표 영역 (고정 너비) */}
        <span className="w-[16px] flex-shrink-0 flex items-center justify-center">
          {hasChildren && (
            <svg
              width="7"
              height="12"
              viewBox="0 0 7 12"
              fill="none"
              className="text-[#DBDEE8]"
            >
              <path
                d="M1 1L5.5 6L1 11"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          )}
        </span>
      </button>

      {/* 서브메뉴 */}
      {hasChildren && subMenuOpen && (
        <SubMenu
          items={item.children!}
          onSelect={onSelect}
          onCloseAll={onCloseAll}
          textAlign={textAlign}
          maxVisibleItems={item.maxVisibleChildren}
          anchorRect={rowRect}
        />
      )}
    </div>
  );
};

const ListPopup = ({
  open,
  referenceRef,
  position,
  onClose,
  items,
  onSelect,
  className = '',
  offsetX = 0,
  offsetY = 0,
  textAlign = 'center',
  maxVisibleItems,
}: ListPopupProps) => {
  const defaultClassName = 'p-[5px] flex flex-col gap-[1px]';
  const effectiveClassName = `${defaultClassName} ${className}`.trim();

  // 스크롤 필요 여부 계산
  const itemHeight = 24;
  const separatorCount = items.filter((i) => i.type === 'separator').length;
  const normalItemCount = items.length - separatorCount;
  const needsScroll =
    maxVisibleItems != null && normalItemCount > maxVisibleItems;
  const maxHeight = needsScroll
    ? maxVisibleItems * itemHeight + separatorCount * 9 + 10
    : undefined;

  const siblingActiveRef = useRef<{
    id: string | null;
    close: (() => void) | null;
  }>({ id: null, close: null });

  const { scrollContainerRef: lenisRef } = useLenis({
    duration: 0.5,
    wheelMultiplier: 0.7,
  });

  return (
    <ListPopupOpenContext.Provider value={open}>
      <FloatingPopup
        open={open}
        referenceRef={referenceRef}
        placement="top"
        offset={25}
        offsetX={offsetX}
        offsetY={offsetY}
        fixedX={position?.x}
        fixedY={position?.y}
        onClose={onClose}
        role="menu"
        className={effectiveClassName}
      >
        <div
          ref={needsScroll ? lenisRef : undefined}
          style={
            maxHeight
              ? { maxHeight, overflowY: 'auto', overflowX: 'hidden' }
              : undefined
          }
          className={`flex flex-col gap-[1px]${
            needsScroll ? ' listpopup-scroll' : ''
          }`}
        >
          {items.map((it) => (
            <MenuItemRow
              key={it.id}
              item={it}
              textAlign={textAlign}
              onSelect={onSelect}
              onCloseAll={onClose}
              siblingActiveRef={siblingActiveRef}
            />
          ))}
        </div>
      </FloatingPopup>
    </ListPopupOpenContext.Provider>
  );
};

export default ListPopup;
