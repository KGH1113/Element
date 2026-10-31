import React, { useEffect, useState, useRef } from 'react';
import Modal from '../../Modal';
import Checkbox from '@components/main/common/Checkbox';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyStore } from '@stores/data/useKeyStore';
import type { TabCss } from '@src/types/plugin/css';
import Button from '@components/main/common/Button';
import type { CustomCssHistoryEntry } from '@src/types/plugin/api';

interface TabCssModalProps {
  isOpen: boolean;
  onClose: () => void;
  showAlert?: (message: string, confirmText?: string) => void;
}

const TabCssModal = ({ isOpen, onClose, showAlert }: TabCssModalProps) => {
  const { t } = useTranslation();
  const selectedKeyType = useKeyStore((state) => state.selectedKeyType);

  const [tabCss, setTabCss] = useState<TabCss | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [history, setHistory] = useState<CustomCssHistoryEntry[]>([]);

  // 모달 열기 시점의 원본 상태 저장 (취소 시 복원용)
  const originalStateRef = useRef<TabCss | null>(null);

  // 모달이 열릴 때 현재 탭의 CSS 정보 로드 및 원본 상태 저장
  useEffect(() => {
    if (!isOpen) return;

    setIsLoading(true);
    window.api.css.history.list().then(setHistory).catch(console.error);
    window.api.css.tab
      .get(selectedKeyType)
      .then((tabResponse) => {
        const css = tabResponse.css || null;
        setTabCss(css);
        // 원본 상태 깊은 복사로 저장
        originalStateRef.current = css ? { ...css } : null;
      })
      .catch((error) => {
        console.error('Failed to get CSS info:', error);
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [isOpen, selectedKeyType]);

  // 탭 CSS 변경 이벤트 구독 (실시간 미리보기 반영)
  useEffect(() => {
    if (!isOpen) return;

    const unsubTabCss = window.api.css.tab.onChanged((payload) => {
      if (payload.tabId === selectedKeyType) {
        setTabCss(payload.css || null);
      }
    });

    return () => {
      unsubTabCss();
    };
  }, [isOpen, selectedKeyType]);

  const handleLoadCss = async () => {
    try {
      const result = await window.api.css.tab.load(selectedKeyType);
      if (result.success && result.css) {
        setTabCss(result.css);
        setHistory(await window.api.css.history.list());
      } else if (result.error) {
        showAlert?.(t('tabCss.loadFailed') + ': ' + result.error);
      }
    } catch (error) {
      console.error('Failed to load tab CSS:', error);
    }
  };

  const handleApplyHistory = async (path: string) => {
    const result = await window.api.css.tab.applyHistory(selectedKeyType, path);
    if (result.success && result.css) setTabCss(result.css);
    else if (result.error) showAlert?.(result.error);
    setHistory(await window.api.css.history.list());
  };

  const handleClearCss = async () => {
    try {
      const result = await window.api.css.tab.clear(selectedKeyType);
      if (result.success) {
        setTabCss(null);
      }
    } catch (error) {
      console.error('Failed to clear tab CSS:', error);
    }
  };

  const handleToggleCss = async () => {
    const newEnabled = !(tabCss?.enabled ?? true);
    try {
      const result = await window.api.css.tab.toggle(
        selectedKeyType,
        newEnabled,
      );
      if (result.success) {
        setTabCss((prev) =>
          prev
            ? { ...prev, enabled: result.enabled }
            : { path: null, content: '', enabled: result.enabled },
        );
      }
    } catch (error) {
      console.error('Failed to toggle tab CSS:', error);
    }
  };

  // 저장: 현재 상태 유지하고 모달 닫기
  const handleSave = () => {
    onClose();
  };

  // 취소: 원본 상태로 복원하고 모달 닫기
  const handleCancel = async () => {
    const original = originalStateRef.current;

    try {
      // 원본 상태와 현재 상태가 다른 경우에만 복원
      const currentState = tabCss;
      const statesAreDifferent =
        original?.path !== currentState?.path ||
        original?.content !== currentState?.content ||
        original?.enabled !== currentState?.enabled;

      if (statesAreDifferent) {
        // css.tab.set을 사용하여 원본 상태로 직접 복원
        await window.api.css.tab.set(selectedKeyType, original);
      }
    } catch (error) {
      console.error('Failed to restore original state:', error);
    }

    onClose();
  };

  if (!isOpen) return null;

  const hasTabCss = tabCss && tabCss.path;
  const cssEnabled = tabCss?.enabled ?? true;

  return (
    <Modal onClick={handleCancel}>
      <div
        className="dmn-manager-surface flex flex-col items-center justify-center gap-[14px] p-[18px]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* CSS 사용 여부 토글 */}
        <div className="flex justify-between w-full items-center">
          <p className="text-white text-style-2">{t('tabCss.enableCss')}</p>
          <Checkbox checked={cssEnabled} onChange={handleToggleCss} />
        </div>

        {history.length > 0 && (
          <div className="flex w-full flex-col gap-[5px]">
            <p className="text-white text-style-2">{t('tabCss.history')}</p>
            {history.map((entry) => (
              <button
                key={entry.path}
                type="button"
                title={entry.path}
                className="h-[26px] truncate rounded-[7px] bg-button-primary px-[8px] text-left text-style-1 text-[#DBDEE8] hover:bg-button-hover"
                onClick={() => void handleApplyHistory(entry.path)}
              >
                {entry.path.split(/[/\\]/).pop()}
              </button>
            ))}
          </div>
        )}

        <Button
          size="sm"
          variant="secondary"
          block
          disabled={!hasTabCss}
          onClick={async () => {
            const result = await window.api.css.tab.export(selectedKeyType);
            if (result.error) showAlert?.(result.error);
          }}
        >
          {t('tabCss.export')}
        </Button>

        {/* CSS 파일 */}
        <div className="flex justify-between w-full items-center">
          <p className="text-white text-style-2">{t('tabCss.cssFile')}</p>
          <div className="flex items-center gap-[8px]">
            <Button
              size="sm"
              variant="danger"
              onClick={handleClearCss}
              disabled={isLoading || !hasTabCss}
            >
              {t('tabCss.remove')}
            </Button>
            <Button size="sm" onClick={handleLoadCss} disabled={isLoading}>
              {t('tabCss.loadFile')}
            </Button>
          </div>
        </div>

        {/* 버튼 영역 */}
        <div className="flex gap-[10.5px]">
          <Button variant="primary" onClick={handleSave} className="w-[150px]">
            {t('keySetting.save')}
          </Button>
          <Button variant="danger" onClick={handleCancel} className="w-[75px]">
            {t('keySetting.cancel')}
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default TabCssModal;
