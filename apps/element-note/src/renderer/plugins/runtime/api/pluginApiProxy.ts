/**
 * 플러그인 API 프록시 생성
 * 각 플러그인에 대해 네임스페이스가 적용된 API 프록시를 생성합니다.
 */

import {
  createNamespacedStorage,
  wrapApiValue,
  wrapFunctionWithContext,
} from '../context';
import { createDefineElement } from './defineElement';
import { createDefineSettings } from './defineSettings';
import { createPluginWriteGateway } from './pluginWriteGateway';
import { editorApi } from '@api/modules/editorApi';
import type {
  NativeKeyMappings,
  NativeElementPositions,
} from '@src/types/editor';
import type { PluginScopedAPI, ReadyUnsubscribe } from '@src/types/plugin/api';
import type { EditorCommittedV1 } from '@src/types/editor';

interface CreatePluginApiProxyOptions {
  pluginId: string;
  registerCleanup: (cleanup: () => void) => void;
  isReloading: () => boolean;
  isRevoked?: () => boolean;
}

/**
 * 플러그인용 API 프록시를 생성합니다.
 */
export const createPluginApiProxy = (
  options: CreatePluginApiProxyOptions,
): PluginScopedAPI => {
  const {
    pluginId,
    registerCleanup,
    isReloading,
    isRevoked = () => false,
  } = options;
  const writes = createPluginWriteGateway(() => !isRevoked());

  const originalStorage = window.api.plugin.storage;
  const namespacedStorage = createNamespacedStorage(pluginId, originalStorage);

  const wrappedApi = wrapApiValue(window.api, pluginId) as Record<
    string,
    unknown
  > & {
    window?: Record<string, unknown>;
    plugin?: Record<string, unknown>;
  };

  const wrapWithContext = (fn: (...args: unknown[]) => unknown) =>
    wrapFunctionWithContext(fn, pluginId) as (...args: unknown[]) => unknown;
  const onCommitted = (
    listener: (event: EditorCommittedV1) => void,
  ): ReadyUnsubscribe => {
    if (isRevoked()) {
      const ready = Promise.reject(new Error('Plugin is no longer active'));
      void ready.catch(() => undefined);
      return Object.assign(() => undefined, { ready });
    }
    const contextualListener = wrapFunctionWithContext(
      listener,
      pluginId,
    ) as typeof listener;
    const unsubscribe = editorApi.onCommitted((event) => {
      if (!isRevoked()) contextualListener(event);
    });
    registerCleanup(unsubscribe);
    return unsubscribe;
  };

  const defineElement = createDefineElement({
    pluginId,
    namespacedStorage,
    registerCleanup,
    wrapFunctionWithContext: wrapWithContext,
    isReloading,
  });

  const defineSettings = createDefineSettings({
    pluginId,
    namespacedStorage,
    registerCleanup,
  });

  const proxiedApi = {
    ...wrappedApi,
    keys: {
      ...(wrappedApi.keys as object),
      get: () => editorApi.get().then(({ document }) => document.keys),
      update: writes.update,
      updateWithPositions: writes.updateWithPositions,
      updatePositions: (positions: NativeElementPositions) =>
        writes.positions('keyPositions', positions),
      onChanged: (
        listener: (keys: NativeKeyMappings) => void,
      ): ReadyUnsubscribe => {
        const unsubscribe = onCommitted((event) => {
          if (!isRevoked() && event.patch.keys) listener(event.patch.keys);
        });
        return unsubscribe;
      },
    },
    statItems: {
      ...(wrappedApi.statItems as object),
      updatePositions: (positions: NativeElementPositions) =>
        writes.positions('statPositions', positions),
    },
    graphItems: {
      ...(wrappedApi.graphItems as object),
      updatePositions: (positions: NativeElementPositions) =>
        writes.positions('graphPositions', positions),
    },
    knobItems: {
      ...(wrappedApi.knobItems as object),
      updatePositions: (positions: NativeElementPositions) =>
        writes.positions('knobPositions', positions),
    },
    editor: {
      get: editorApi.get,
      commit: writes.commit,
      onCommitted,
    },
    window: {
      ...(wrappedApi.window ?? {}),
      type: window.__dmn_window_type as 'main' | 'overlay',
    },
    plugin: {
      ...(wrappedApi.plugin ?? {}),
      storage: namespacedStorage,
      registerCleanup: (cleanup: () => void) => registerCleanup(cleanup),
      defineElement: wrapWithContext(
        defineElement as (...args: unknown[]) => unknown,
      ),
      defineSettings: wrapWithContext(
        defineSettings as (...args: unknown[]) => unknown,
      ),
    },
  } as unknown as PluginScopedAPI;

  return proxiedApi;
};

/**
 * 플러그인용 Window 프록시를 생성합니다.
 */
export const createPluginWindowProxy = (
  proxiedApi: PluginScopedAPI,
): Window => {
  return new Proxy(window, {
    get(target, prop: string | symbol, receiver) {
      if (prop === 'api') return proxiedApi;
      if (prop === 'dmn') return proxiedApi; // dmn 별칭도 프록시된 API 반환
      return Reflect.get(target, prop, receiver);
    },
    set(target, prop: string | symbol, value, receiver) {
      return Reflect.set(target, prop, value, receiver);
    },
  });
};
