import { create } from 'zustand';
import type { HistoryStatus } from '@api/modules/historyApi';

interface HistoryStatusState {
  initialized: boolean;
  status: HistoryStatus | null;
  applyStatus(status: HistoryStatus): void;
}

/** 실행 취소 가능 여부는 v2 네이티브 히스토리의 단조 시퀀스를 따른다. */
export const useHistoryStatusStore = create<HistoryStatusState>((set) => ({
  initialized: false,
  status: null,
  applyStatus(status) {
    set((state) => {
      if (state.status && status.statusSeq < state.status.statusSeq)
        return state;
      return { initialized: true, status };
    });
  },
}));
