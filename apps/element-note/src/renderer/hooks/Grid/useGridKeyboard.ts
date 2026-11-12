/**
 * Grid 키보드 이벤트 핸들링 훅
 * - 방향키로 선택된 요소 이동
 * - Delete 키로 선택된 요소 삭제
 * - Escape 키로 선택 해제
 * - Ctrl+C/V 복사/붙여넣기
 * - Ctrl+Z/Shift+Z Undo/Redo
 */

import { useEffect, useRef } from 'react';
import { useSettingsStore } from '@stores/useSettingsStore';
import {
  useGridSelectionStore,
  type SelectedElement,
} from '@stores/grid/useGridSelectionStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import { ARROW_KEY_HISTORY_DELAY } from './constants';
import { isMac } from '@utils/core/platform';
import { isTextEntry, shortcutLetter } from '@utils/core/keyboardInput';
import {
  groupSelectedElements,
  ungroupSelectedElements,
} from '@utils/grid/groupActions';

interface UseGridKeyboardParams {
  selectedElements: SelectedElement[];
  moveSelectedElements: (
    deltaX: number,
    deltaY: number,
    saveHistory?: boolean,
  ) => void;
  deleteSelectedElements: () => void;
  clearSelection: () => void;
  copySelectedElements: () => void;
  pasteElements: () => void;
  canUndo: boolean;
  canRedo: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onMoveForward?: () => void;
  onMoveBackward?: () => void;
  newGroupLabel?: string;
}

/**
 * 키보드 핸들러 훅
 */
export function useGridKeyboard({
  selectedElements,
  moveSelectedElements,
  deleteSelectedElements,
  clearSelection,
  copySelectedElements,
  pasteElements,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  onMoveForward,
  onMoveBackward,
  newGroupLabel = 'New Group',
}: UseGridKeyboardParams): void {
  const lastArrowKeyTime = useRef(0);
  const macOS = isMac();

  // 선택 요소 키보드 조작
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (typeof window !== 'undefined' && window.__dmn_isKeyListening) {
        return;
      }
      // 입력 요소에서는 무시
      if (isTextEntry(e.target)) return;

      const isPrimaryModifierPressed = macOS ? e.metaKey : e.ctrlKey;
      const letter = shortcutLetter(e);

      // Ctrl/Cmd + G: 선택 요소 그룹화
      if (isPrimaryModifierPressed && !e.shiftKey && e.code === 'KeyG') {
        e.preventDefault();
        if (selectedElements.length < 2) return;

        const selectedKeyType = useKeyStore.getState().selectedKeyType;
        groupSelectedElements(selectedKeyType, selectedElements, newGroupLabel);
        return;
      }

      // Ctrl/Cmd + Shift + G: 선택 요소 그룹 해제
      if (isPrimaryModifierPressed && e.shiftKey && e.code === 'KeyG') {
        e.preventDefault();
        if (selectedElements.length === 0) return;

        const selectedKeyType = useKeyStore.getState().selectedKeyType;
        ungroupSelectedElements(selectedKeyType, selectedElements);
        return;
      }

      // Ctrl+C: 복사 (선택된 요소가 있을 때)
      if (isPrimaryModifierPressed && letter === 'c') {
        if (selectedElements.length > 0) {
          e.preventDefault();
          copySelectedElements();
        }
        return;
      }

      // Ctrl+V: 붙여넣기 (스토어에서 직접 클립보드 확인)
      if (isPrimaryModifierPressed && letter === 'v') {
        const currentClipboard = useGridSelectionStore.getState().clipboard;
        if (currentClipboard.length > 0) {
          e.preventDefault();
          pasteElements();
        }
        return;
      }

      // 선택된 요소가 없으면 무시
      if (selectedElements.length === 0) return;

      // 방향키 처리
      const arrowKeys = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'];
      if (arrowKeys.includes(e.key)) {
        e.preventDefault();

        // One unit at a time; with Shift, one step of the grid.
        const step = e.shiftKey
          ? Math.max(
              1,
              Math.round(
                useSettingsStore.getState().gridSettings?.gridSnapSize ?? 5,
              ),
            )
          : 1;
        let deltaX = 0;
        let deltaY = 0;

        switch (e.key) {
          case 'ArrowUp':
            deltaY = -step;
            break;
          case 'ArrowDown':
            deltaY = step;
            break;
          case 'ArrowLeft':
            deltaX = -step;
            break;
          case 'ArrowRight':
            deltaX = step;
            break;
        }

        // 일정 시간 내 연속 입력이면 히스토리 저장 안함
        const now = Date.now();
        const saveHistory =
          now - lastArrowKeyTime.current > ARROW_KEY_HISTORY_DELAY;
        lastArrowKeyTime.current = now;

        moveSelectedElements(deltaX, deltaY, saveHistory);
        return;
      }

      // Delete 키로 선택 요소 삭제
      if (e.key === 'Delete' || e.key === 'Backspace') {
        e.preventDefault();
        deleteSelectedElements();
        return;
      }

      // Escape 키로 선택 해제
      if (e.key === 'Escape') {
        clearSelection();
        return;
      }

      // ] 키로 앞으로 이동
      if (e.key === ']' && typeof onMoveForward === 'function') {
        e.preventDefault();
        onMoveForward();
        return;
      }

      // [ 키로 뒤로 이동
      if (e.key === '[' && typeof onMoveBackward === 'function') {
        e.preventDefault();
        onMoveBackward();
        return;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    macOS,
    selectedElements,
    moveSelectedElements,
    deleteSelectedElements,
    clearSelection,
    copySelectedElements,
    pasteElements,
    onMoveForward,
    onMoveBackward,
    newGroupLabel,
  ]);

  // Undo/Redo 단축키 처리
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (typeof window !== 'undefined' && window.__dmn_isKeyListening) {
        return;
      }
      // 입력 요소에서는 단축키 무시
      if (isTextEntry(e.target)) return;

      const isPrimaryModifierPressed = macOS ? e.metaKey : e.ctrlKey;
      if (!isPrimaryModifierPressed) return;
      const letter = shortcutLetter(e);

      // Ctrl+Z: 실행 취소
      if (!e.shiftKey && letter === 'z') {
        e.preventDefault();
        if (canUndo && typeof onUndo === 'function') {
          onUndo();
        }
      }
      // Ctrl+Shift+Z, Windows에서는 Ctrl+Y도: 다시 실행
      else if (
        (e.shiftKey && letter === 'z') ||
        (!macOS && !e.shiftKey && letter === 'y')
      ) {
        e.preventDefault();
        if (canRedo && typeof onRedo === 'function') {
          onRedo();
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [macOS, canUndo, canRedo, onUndo, onRedo]);

  // 캔버스를 누르면 다른 곳의 입력 칸에서 나온다. 요소를 누를 때는 포커스가 옮겨 가지 않아
  // 실행 취소·삭제·방향키가 계속 그 입력 칸으로 가기 때문이다. 입력 칸은 떠나며 값을 확정한다.
  useEffect(() => {
    const handlePress = (e: PointerEvent) => {
      const focused = document.activeElement;
      if (!isTextEntry(focused)) return;
      const canvas =
        e.target instanceof Element
          ? e.target.closest('[data-grid-container]')
          : null;
      if (canvas && !canvas.contains(focused)) focused.blur();
    };
    window.addEventListener('pointerdown', handlePress, true);
    return () => window.removeEventListener('pointerdown', handlePress, true);
  }, []);
}
