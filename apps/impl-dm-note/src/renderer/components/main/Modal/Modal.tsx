import React, { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import type { PopupMotionState } from '@hooks/ui/usePopupPresence';

interface ModalProps {
  onClick?: () => void;
  children: React.ReactNode;
  animate?: boolean;
  ariaLabel?: string;
  initialFocusRef?: React.RefObject<HTMLElement>;
  closeOnEscape?: boolean;
  restoreFocus?: boolean;
  motionState?: PopupMotionState;
}

const Modal = ({
  onClick,
  children,
  animate = true,
  ariaLabel,
  initialFocusRef,
  closeOnEscape = true,
  restoreFocus = true,
  motionState,
}: ModalProps) => {
  const usesPresenceMotion = animate && motionState !== undefined;
  const backdropAnimClass =
    animate && !usesPresenceMotion
      ? 'opacity-0 animate-modal-fade'
      : 'opacity-100';
  const contentAnimClass = animate
    ? usesPresenceMotion
      ? 'dmn-modal-motion'
      : 'animate-modal-scale'
    : '';
  const closeFromBackdropRef = useRef(false);
  const contentRef = useRef<HTMLDivElement>(null);
  const openerRef = useRef<HTMLElement | null>(
    document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null,
  );

  const getFocusableElements = () => {
    const content = contentRef.current;
    if (!content) return [];
    return Array.from(
      content.querySelectorAll<HTMLElement>(
        'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])',
      ),
    ).filter((element) => !element.hasAttribute('hidden'));
  };

  const isTopmostModal = () => {
    const modals = document.querySelectorAll(
      '[data-dmn-modal-backdrop="true"]',
    );
    return modals[modals.length - 1] === contentRef.current?.parentElement;
  };

  useEffect(() => {
    const reset = () => {
      closeFromBackdropRef.current = false;
    };
    document.addEventListener('pointercancel', reset, true);
    window.addEventListener('blur', reset);
    return () => {
      document.removeEventListener('pointercancel', reset, true);
      window.removeEventListener('blur', reset);
    };
  }, []);

  useEffect(() => {
    const opener = openerRef.current;
    const frame = requestAnimationFrame(() => {
      const target = initialFocusRef?.current ?? getFocusableElements()[0];
      (target ?? contentRef.current)?.focus();
    });

    return () => {
      cancelAnimationFrame(frame);
      if (restoreFocus && opener?.isConnected) opener.focus();
    };
  }, [initialFocusRef, restoreFocus]);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (!isTopmostModal()) return;
      if (event.key === 'Escape') {
        if (
          closeOnEscape &&
          !window.__dmn_isKeyListening &&
          !event.defaultPrevented
        ) {
          event.preventDefault();
          onClick?.();
        }
        return;
      }
      if (event.key !== 'Tab') return;

      const focusable = getFocusableElements();
      if (focusable.length === 0) {
        event.preventDefault();
        contentRef.current?.focus();
        return;
      }
      const active = document.activeElement;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (!contentRef.current?.contains(active)) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
      } else if (event.shiftKey && active === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [closeOnEscape, onClick]);

  const handleBackdropPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    // only mark if pointer started directly on the backdrop
    closeFromBackdropRef.current = e.target === e.currentTarget;
  };

  const handleBackdropClick = (e: React.MouseEvent<HTMLDivElement>) => {
    // ignore clicks that bubbled up from content
    if (e.target !== e.currentTarget) return;
    // ignore clicks without a matching pointerDown on backdrop (e.g. window drag)
    if (!closeFromBackdropRef.current) return;
    closeFromBackdropRef.current = false;
    onClick?.();
  };

  const handleWheel = (e: React.WheelEvent) => {
    e.stopPropagation();
  };

  return createPortal(
    <div
      data-dmn-modal-backdrop="true"
      data-dmn-app-portal
      data-dmn-motion-state={usesPresenceMotion ? motionState : undefined}
      className={`dmn-modal-backdrop fixed bottom-[60px] left-0 right-0 top-[30px] flex items-center justify-center z-50 ${backdropAnimClass}`}
      onPointerDown={handleBackdropPointerDown}
      onClick={handleBackdropClick}
      onWheel={handleWheel}
    >
      <div
        ref={contentRef}
        data-dmn-motion-state={usesPresenceMotion ? motionState : undefined}
        className={`dmn-modal-surface ${contentAnimClass}`}
        role="dialog"
        aria-modal="true"
        aria-label={ariaLabel}
        tabIndex={-1}
      >
        {children}
      </div>
    </div>,
    document.body,
  );
};

export default Modal;
