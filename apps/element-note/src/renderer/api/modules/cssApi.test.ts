import { beforeEach, describe, expect, it, vi } from 'vitest';

const invoke = vi.hoisted(() => vi.fn());
vi.mock('../invoke', () => ({ invoke }));
vi.mock('./shared', () => ({
  getCachedLocale: () => 'ko',
  subscribe: () => () => {},
}));

import { cssApi } from './cssApi';

beforeEach(() => {
  invoke.mockReset();
});

describe('v2 CSS compatibility', () => {
  it('adapts native activation content into the existing UI result', async () => {
    invoke.mockResolvedValue({
      success: true,
      content: '',
      path: '/custom.css',
    });
    await expect(cssApi.history.activate('/custom.css')).resolves.toEqual({
      success: true,
      css: { content: '', path: '/custom.css' },
    });
  });

  it('explains a missing history file without reporting success', async () => {
    invoke.mockResolvedValue({ success: false, code: 'NOT_FOUND' });
    const result = await cssApi.history.activate('/missing.css');
    expect(result.success).toBe(false);
    expect(result.error).toContain('다시 불러와');
    expect(result.css).toBeUndefined();
  });

  it('uses the v2 tab history command and preserves the CSS payload', async () => {
    const result = {
      success: true,
      tabId: 'hand',
      css: { path: '/custom.css', content: 'key {}', enabled: true },
    };
    invoke.mockResolvedValue(result);
    await expect(
      cssApi.tab.applyHistory('hand', '/custom.css'),
    ).resolves.toEqual(result);
    expect(invoke).toHaveBeenCalledWith('css_tab_activate_history', {
      tabId: 'hand',
      path: '/custom.css',
    });
  });

  it('maps the v2 history-list removal response to a successful removal', async () => {
    invoke.mockResolvedValue([]);
    await expect(cssApi.history.remove('/custom.css')).resolves.toEqual({
      success: true,
    });
  });
});
