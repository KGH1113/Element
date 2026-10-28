import { invoke } from '@tauri-apps/api/core';

import type { BootstrapPayload, LaunchContext } from '@src/types/app';

export const appApi = {
  bootstrap: () => invoke<BootstrapPayload>('app_bootstrap'),
  openExternal: (url: string) => invoke<void>('app_open_external', { url }),
  restart: () => invoke<void>('app_restart'),
  quit: () => invoke<void>('app_quit'),
  launchContext: () => invoke<LaunchContext>('app_launch_context'),
};

export const windowApi = {
  type: window.__dmn_window_type as 'main' | 'overlay',
  minimize: () => invoke<void>('window_minimize'),
  close: () => invoke<void>('window_close'),
  showMain: () => invoke<void>('window_show_main'),
  openDevtoolsAll: () => invoke<void>('window_open_devtools_all'),
};
