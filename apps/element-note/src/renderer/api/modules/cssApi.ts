import { invoke } from '../invoke';
import { getCachedLocale, subscribe } from './shared';

import type {
  CssLoadResult,
  CssSetContentResult,
  CssTogglePayload,
  CssHistoryMutationResult,
  CustomCssHistoryEntry,
  TabCssLoadResult,
} from '@src/types/plugin/api';
import type { CustomCss } from '@src/types/plugin/css';

interface NativeCssActivation {
  success: boolean;
  code?: string;
  content?: string;
  path?: string;
}

function historyError(code?: string): string {
  const korean = (getCachedLocale() ?? 'ko') === 'ko';
  if (code === 'NOT_FOUND') {
    return korean
      ? 'CSS 파일을 찾지 못했습니다. 파일 위치를 확인하고 다시 불러와 주세요.'
      : 'The CSS file was not found. Check its location and load it again.';
  }
  if (code === 'PATH_NOT_AUTHORIZED') {
    return korean
      ? '이 CSS 파일을 다시 불러온 뒤 적용해 주세요.'
      : 'Load this CSS file again before applying it.';
  }
  return korean
    ? 'CSS 파일을 적용하지 못했습니다. 읽을 수 있는 UTF-8 CSS 파일인지 확인하고 다시 불러와 주세요.'
    : 'The CSS file could not be applied. Check that it is a readable UTF-8 CSS file and load it again.';
}

export const cssApi = {
  get: () => invoke<CustomCss>('css_get'),
  getUse: () => invoke<boolean>('css_get_use'),
  toggle: (enabled: boolean) =>
    invoke<CssTogglePayload>('css_toggle', { enabled }),
  load: () => invoke<CssLoadResult>('css_load'),
  setContent: (content: string) =>
    invoke<CssSetContentResult>('css_set_content', { content }),
  reset: () => invoke<void>('css_reset'),
  history: {
    list: () =>
      invoke<import('@src/types/plugin/api').CustomCssHistoryEntry[]>(
        'css_history_get',
      ),
    activate: async (path: string): Promise<CssHistoryMutationResult> => {
      const result = await invoke<NativeCssActivation>('css_history_activate', {
        path,
      });
      return result.success
        ? {
            success: true,
            css: { content: result.content ?? '', path: result.path ?? path },
          }
        : { success: false, error: historyError(result.code) };
    },
    remove: async (path: string): Promise<CssHistoryMutationResult> => {
      await invoke<CustomCssHistoryEntry[]>('css_history_remove', { path });
      return { success: true };
    },
  },
  onUse: (listener: (payload: CssTogglePayload) => void) =>
    subscribe<CssTogglePayload>('css:use', listener),
  onContent: (listener: (payload: CustomCss) => void) =>
    subscribe<CustomCss>('css:content', listener),
  tab: {
    getAll: () =>
      invoke<import('@src/types/plugin/css').TabCssOverrides>(
        'css_tab_get_all',
      ),
    get: (tabId: string) =>
      invoke<import('@src/types/plugin/api').TabCssResponse>('css_tab_get', {
        tabId,
      }),
    load: (tabId: string) =>
      invoke<import('@src/types/plugin/api').TabCssLoadResult>('css_tab_load', {
        tabId,
      }),
    applyHistory: async (
      tabId: string,
      path: string,
    ): Promise<TabCssLoadResult> => {
      const result = await invoke<TabCssLoadResult & { code?: string }>(
        'css_tab_activate_history',
        { tabId, path },
      );
      return result.success
        ? result
        : { ...result, error: historyError(result.code) };
    },
    export: (tabId: string) =>
      invoke<import('@src/types/plugin/api').CssSetContentResult>(
        'css_tab_export',
        { tabId },
      ),
    clear: (tabId: string) =>
      invoke<import('@src/types/plugin/api').TabCssClearResult>(
        'css_tab_clear',
        {
          tabId,
        },
      ),
    toggle: (tabId: string, enabled: boolean) =>
      invoke<import('@src/types/plugin/api').TabCssToggleResult>(
        'css_tab_toggle',
        {
          tabId,
          enabled,
        },
      ),
    set: (tabId: string, css: import('@src/types/plugin/css').TabCss | null) =>
      invoke<import('@src/types/plugin/api').TabCssSetResult>('css_tab_set', {
        tabId,
        css,
      }),
    onChanged: (
      listener: (
        payload: import('@src/types/plugin/api').TabCssResponse,
      ) => void,
    ) =>
      subscribe<import('@src/types/plugin/api').TabCssResponse>(
        'tabCss:changed',
        listener,
      ),
    onChangedAll: (
      listener: (
        payload: import('@src/types/plugin/css').TabCssOverrides,
      ) => void,
    ) =>
      subscribe<import('@src/types/plugin/css').TabCssOverrides>(
        'tabCss:changed_all',
        listener,
      ),
  },
};
