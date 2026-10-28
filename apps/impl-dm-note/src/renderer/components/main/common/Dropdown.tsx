import React, {
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
} from 'react';
import { createPortal } from 'react-dom';
import { usePopupPresence } from '@hooks/ui/usePopupPresence';

export interface DropdownOption {
  label: string;
  value: string;
}

export interface DropdownProps {
  options: DropdownOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  fullWidth?: boolean;
  iconTrigger?: React.ReactNode;
  align?: 'left' | 'center' | 'right';
  widthClass?: string;
  ariaLabel?: string;
}

interface MenuPosition {
  left: number;
  top: number;
  width?: number;
  placement: 'top-start' | 'bottom-start';
}

const VIEWPORT_PADDING = 5;
const MENU_GAP = 4;
const BOTTOM_CHROME_PADDING = 60;

const Dropdown: React.FC<DropdownProps> = ({
  options,
  value,
  onChange,
  placeholder = '선택',
  disabled = false,
  fullWidth = false,
  iconTrigger,
  align = 'left',
  widthClass = '',
  ariaLabel,
}) => {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  // The active row is only painted while navigating by keyboard; pointer
  // hover is styled by :hover, so no row stays lit after the pointer leaves.
  const [keyboardActive, setKeyboardActive] = useState(false);
  const [position, setPosition] = useState<MenuPosition | null>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const menuId = useId();
  const selectedIndex = options.findIndex((option) => option.value === value);
  const selected = selectedIndex >= 0 ? options[selectedIndex] : undefined;
  const popupPresence = usePopupPresence(open, {
    ready: position !== null,
    motionRef: menuRef,
  });

  const close = useCallback((restoreFocus = false) => {
    setOpen(false);
    if (restoreFocus) buttonRef.current?.focus();
  }, []);

  const openMenu = useCallback(
    (preferredIndex?: number) => {
      if (disabled) return;
      setActiveIndex(
        options.length === 0
          ? -1
          : (preferredIndex ?? (selectedIndex >= 0 ? selectedIndex : 0)),
      );
      setOpen(true);
    },
    [disabled, options.length, selectedIndex],
  );

  const selectOption = useCallback(
    (index: number) => {
      const option = options[index];
      if (!option) return;
      onChange(option.value);
      close(true);
    },
    [close, onChange, options],
  );

  const placeMenu = useCallback(() => {
    const button = buttonRef.current;
    const menu = menuRef.current;
    if (!button || !menu) return;

    const anchor = button.getBoundingClientRect();
    const menuRect = menu.getBoundingClientRect();
    const menuWidth = fullWidth ? anchor.width : menuRect.width;
    const menuHeight = menuRect.height;
    let left = anchor.left;
    if (align === 'right') left = anchor.right - menuWidth;
    if (align === 'center') left = anchor.left + (anchor.width - menuWidth) / 2;
    left = Math.min(
      Math.max(left, VIEWPORT_PADDING),
      Math.max(
        VIEWPORT_PADDING,
        window.innerWidth - menuWidth - VIEWPORT_PADDING,
      ),
    );

    const availableBelow =
      window.innerHeight - anchor.bottom - BOTTOM_CHROME_PADDING;
    const availableAbove = anchor.top - VIEWPORT_PADDING;
    const openUpward =
      availableBelow < menuHeight + MENU_GAP && availableAbove > availableBelow;
    const desiredTop = openUpward
      ? anchor.top - menuHeight - MENU_GAP
      : anchor.bottom + MENU_GAP;
    const top = Math.min(
      Math.max(desiredTop, VIEWPORT_PADDING),
      Math.max(
        VIEWPORT_PADDING,
        window.innerHeight - menuHeight - VIEWPORT_PADDING,
      ),
    );

    setPosition({
      left,
      top,
      width: fullWidth ? anchor.width : undefined,
      placement: openUpward ? 'top-start' : 'bottom-start',
    });
  }, [align, fullWidth]);

  useLayoutEffect(() => {
    if (!open || !popupPresence.mounted) return;
    placeMenu();
  }, [open, options.length, placeMenu, popupPresence.mounted]);

  useEffect(() => {
    if (!open) return;
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (
        rootRef.current?.contains(target) ||
        menuRef.current?.contains(target)
      ) {
        return;
      }
      close();
    };
    const handleResize = () => placeMenu();
    const handleScroll = () => close();
    document.addEventListener('pointerdown', handlePointerDown, true);
    window.addEventListener('resize', handleResize);
    document.addEventListener('scroll', handleScroll, true);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown, true);
      window.removeEventListener('resize', handleResize);
      document.removeEventListener('scroll', handleScroll, true);
    };
  }, [close, open, placeMenu]);

  const moveActive = (direction: 1 | -1) => {
    if (options.length === 0) return;
    const current = activeIndex >= 0 ? activeIndex : selectedIndex;
    const next =
      (Math.max(current, 0) + direction + options.length) % options.length;
    setActiveIndex(next);
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key !== 'Tab' && event.key !== 'Escape') {
      setKeyboardActive(true);
    }
    if (event.key === 'Escape' && open) {
      event.preventDefault();
      close(true);
      return;
    }
    if (event.key === 'Tab' && open) {
      close();
      return;
    }
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      if (options.length === 0) {
        openMenu();
        return;
      }
      if (!open) openMenu(event.key === 'Home' ? 0 : options.length - 1);
      else setActiveIndex(event.key === 'Home' ? 0 : options.length - 1);
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) {
        const fallback = event.key === 'ArrowDown' ? 0 : options.length - 1;
        openMenu(selectedIndex >= 0 ? selectedIndex : fallback);
      } else {
        moveActive(event.key === 'ArrowDown' ? 1 : -1);
      }
      return;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!open) openMenu();
      else selectOption(activeIndex >= 0 ? activeIndex : selectedIndex);
    }
  };

  useEffect(() => {
    if (!popupPresence.mounted) setPosition(null);
  }, [popupPresence.mounted]);

  const floatingMenu = popupPresence.mounted
    ? createPortal(
        <div
          ref={menuRef}
          id={menuId}
          data-dmn-app-portal
          role="listbox"
          aria-label={ariaLabel}
          data-dmn-motion-state={popupPresence.state}
          data-dmn-placement={position?.placement ?? 'bottom-start'}
          className={`dmn-dropdown-menu dmn-motion ${widthClass}`}
          style={{
            position: 'fixed',
            left: position?.left ?? 0,
            top: position?.top ?? 0,
            width: position?.width,
            visibility: position ? 'visible' : 'hidden',
          }}
        >
          {options.length === 0 ? (
            <div className="dmn-dropdown-empty" role="status">
              옵션 없음
            </div>
          ) : (
            options.map((option, index) => (
              <button
                key={option.value}
                id={`${menuId}-option-${index}`}
                type="button"
                role="option"
                aria-selected={option.value === value}
                data-active={
                  (keyboardActive && index === activeIndex) || undefined
                }
                className="dmn-dropdown-option"
                tabIndex={-1}
                onPointerMove={() => {
                  setKeyboardActive(false);
                  setActiveIndex(index);
                }}
                onClick={() => selectOption(index)}
              >
                <span>{option.label}</span>
              </button>
            ))
          )}
        </div>,
        document.body,
      )
    : null;

  return (
    <div ref={rootRef} className={`relative ${fullWidth ? 'w-full' : ''}`}>
      <button
        ref={buttonRef}
        type="button"
        className={`dmn-dropdown-trigger ${iconTrigger ? 'dmn-dropdown-trigger--icon' : ''} ${
          fullWidth ? 'w-full' : ''
        } ${widthClass}`}
        aria-label={ariaLabel}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-activedescendant={
          open && activeIndex >= 0
            ? `${menuId}-option-${activeIndex}`
            : undefined
        }
        disabled={disabled}
        onClick={() => {
          setKeyboardActive(false);
          if (open) close();
          else openMenu();
        }}
        onKeyDown={handleKeyDown}
      >
        {iconTrigger ?? (
          <>
            <span className="truncate">{selected?.label ?? placeholder}</span>
            <svg
              width="8"
              height="5"
              viewBox="0 0 14 8"
              fill="none"
              className="dmn-dropdown-chevron"
              aria-hidden="true"
            >
              <path
                d="M1 1L7 7L13 1"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </>
        )}
      </button>
      {floatingMenu}
    </div>
  );
};

export default Dropdown;
