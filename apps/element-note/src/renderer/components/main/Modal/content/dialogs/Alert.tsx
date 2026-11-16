import React, { useState, useEffect, useRef } from 'react';
import { useLenis } from '@hooks/useLenis';
import { useTranslation } from '@contexts/useTranslation';
import Modal from '../../Modal';
import { getScrollShadowState } from '@utils/grid/scrollShadow';
import Button from '@components/main/common/Button';

interface AlertProps {
  isOpen: boolean;
  message: string;
  type?: 'alert' | 'confirm' | 'custom';
  confirmText?: string;
  cancelText?: string;
  showCancel?: boolean;
  /**
   * Going ahead cannot be taken back (deleting, resetting). The button that
   * goes ahead is drawn as such, and the dialog opens on Cancel.
   */
  danger?: boolean;
  onConfirm?: () => void;
  onCancel?: () => void;
}

/**
 * A message with one answer, or a question with two. The answers are one
 * size and in one order: backing out on the left, going ahead on the right.
 * The dialog opens on the answer Enter should give: going ahead, or backing
 * out when going ahead cannot be taken back. Escape and a press outside back
 * out.
 */
const Alert = ({
  isOpen,
  message,
  type = 'alert', // "alert", "confirm", or "custom"
  confirmText,
  cancelText,
  showCancel,
  danger = false,
  onConfirm,
  onCancel,
}: AlertProps) => {
  const { t } = useTranslation();
  const confirmRef = useRef<HTMLButtonElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);

  const [scrollState, setScrollState] = useState<{
    hasTopShadow: boolean;
    hasBottomShadow: boolean;
  }>({
    hasTopShadow: false,
    hasBottomShadow: false,
  });

  // 스크롤 상태 업데이트 함수
  const updateScrollState = (el: HTMLElement | null) => {
    if (!el) return;
    const nextState = getScrollShadowState(el);
    setScrollState((prev) =>
      prev.hasTopShadow === nextState.hasTopShadow &&
      prev.hasBottomShadow === nextState.hasBottomShadow
        ? prev
        : nextState,
    );
  };

  // Lenis smooth scroll 적용 (onScroll 콜백으로 그림자 업데이트)
  const {
    scrollContainerRef: scrollRef,
    wrapperElement,
    scrollbarWidth,
  } = useLenis({
    onScroll: () => updateScrollState(wrapperElement),
  });

  const isConfirm = type === 'confirm';
  const isCustom = type === 'custom';

  useEffect(() => {
    if (isCustom && wrapperElement) {
      // DOM이 렌더링된 후 스크롤 상태 확인
      const timer = setTimeout(() => updateScrollState(wrapperElement), 0);
      return () => clearTimeout(timer);
    }
  }, [isCustom, message, wrapperElement]);

  if (!isOpen) return null;

  const confirmLabel = confirmText || t('common.confirm');
  const cancelLabel = cancelText || t('common.cancel');
  const shouldShowCancel = isConfirm || (isCustom && showCancel);

  const hasOverflow =
    !!wrapperElement &&
    wrapperElement.scrollHeight > wrapperElement.clientHeight + 1;

  return (
    <Modal
      // A message has one answer; backing out of it is that answer.
      onClick={shouldShowCancel ? onCancel : (onCancel ?? onConfirm)}
      initialFocusRef={
        (danger && shouldShowCancel
          ? cancelRef
          : confirmRef) as React.RefObject<HTMLElement>
      }
    >
      <div
        className="dmn-modal-card dmn-alert flex flex-col p-[20px] pr-[6px]"
        role="alertdialog"
        aria-modal="true"
        onClick={(e) => e.stopPropagation()}
      >
        {/* 메시지 텍스트 or Custom HTML */}
        {isCustom ? (
          <div className="relative">
            {/* 상단 그림자 */}
            <div
              className={`absolute top-0 left-0 right-[14px] h-[10px] bg-gradient-to-b from-[#1A191E] to-transparent pointer-events-none z-10 transition-opacity duration-150 ${
                scrollState.hasTopShadow ? 'opacity-100' : 'opacity-0'
              }`}
            />

            <div
              ref={scrollRef}
              className="max-h-[244px] overflow-y-auto modal-content-scroll pr-[14px] text-center text-[#FFFFFF]"
              style={{
                width:
                  hasOverflow && scrollbarWidth > 0
                    ? `calc(100% + ${scrollbarWidth}px)`
                    : undefined,
                transform:
                  hasOverflow && scrollbarWidth > 0
                    ? `translateX(-${scrollbarWidth}px)`
                    : undefined,
                paddingLeft:
                  hasOverflow && scrollbarWidth > 0
                    ? `${scrollbarWidth}px`
                    : undefined,
              }}
              dangerouslySetInnerHTML={{ __html: message }}
            />

            {/* 하단 그림자 */}
            <div
              className={`absolute bottom-0 left-0 right-[14px] h-[10px] bg-gradient-to-t from-[#1A191E] to-transparent pointer-events-none z-10 transition-opacity duration-150 ${
                scrollState.hasBottomShadow ? 'opacity-100' : 'opacity-0'
              }`}
            />
          </div>
        ) : (
          <div className="dmn-alert-message pr-[14px]">{message}</div>
        )}

        <div className="dmn-alert-answers pr-[14px]">
          {shouldShowCancel && (
            <Button ref={cancelRef} onClick={onCancel} variant="secondary">
              {cancelLabel}
            </Button>
          )}
          <Button
            ref={confirmRef}
            onClick={onConfirm}
            variant={danger ? 'danger' : 'primary'}
          >
            {confirmLabel}
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default Alert;
