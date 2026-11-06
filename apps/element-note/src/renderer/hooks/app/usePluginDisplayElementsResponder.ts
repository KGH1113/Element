import { useEffect } from 'react';
import { usePluginDisplayElementStore } from '@stores/plugin/usePluginDisplayElementStore';
import {
  isLocalPluginRuntimeReady,
  subscribePluginReadiness,
} from '@plugins/runtime/pluginRuntimeReadiness';
import {
  normalizeStateKeys,
  setPluginMenuRuntimeState,
  clearPluginMenuRuntimeState,
} from '@utils/plugin/menu/pluginMenuRuntimeState';

// 다중 OBS 클라이언트 동시 재연결 시 요청 버스트를 응답 1회로 코얼레싱
const RESPOND_DEBOUNCE_MS = 100;

// 오버레이(네이티브/OBS)의 플러그인 요소 상태 요청에 응답
// Grid 언마운트(설정 화면) 중에도 응답 가능하도록 main App 레벨에 상시 마운트
export function usePluginDisplayElementsResponder() {
  useEffect(() => {
    const unsubscribe = window.api.bridge.on<{
      fullId: string;
      state: Record<string, unknown>;
    }>('plugin:displayElement:syncMenuState', (data) => {
      if (
        !data?.fullId ||
        !data.state ||
        typeof data.state !== 'object' ||
        Array.isArray(data.state)
      )
        return;
      const { elements, definitions } = usePluginDisplayElementStore.getState();
      const element = elements.find(
        (candidate) => candidate.fullId === data.fullId,
      );
      if (!element) return;
      const definition = element.definitionId
        ? definitions.get(element.definitionId)
        : undefined;
      setPluginMenuRuntimeState(
        data.fullId,
        data.state,
        normalizeStateKeys(definition?.contextMenuStateKeys),
      );
    });
    let known = new Set(
      usePluginDisplayElementStore
        .getState()
        .elements.map((element) => element.fullId),
    );
    const unsubscribeStore = usePluginDisplayElementStore.subscribe(
      ({ elements }) => {
        const current = new Set(elements.map((element) => element.fullId));
        known.forEach((id) => {
          if (!current.has(id)) clearPluginMenuRuntimeState(id);
        });
        known = current;
      },
    );
    return () => {
      unsubscribe();
      unsubscribeStore();
      known.forEach(clearPluginMenuRuntimeState);
    };
  }, []);

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | null = null;
    const respond = () => {
      if (timer || !isLocalPluginRuntimeReady()) return;
      timer = setTimeout(() => {
        timer = null;
        if (!isLocalPluginRuntimeReady()) return;
        const elements = usePluginDisplayElementStore.getState().elements;
        window.api.bridge
          .sendTo('overlay', 'plugin:displayElements:sync', {
            elements,
            pluginsReady: true,
          })
          .catch(() => {
            // A reconnect may briefly have no overlay recipient.
          });
      }, RESPOND_DEBOUNCE_MS);
    };

    const unsubscribe = window.api.bridge.on(
      'plugin:displayElements:request',
      respond,
    );
    let wasReady = isLocalPluginRuntimeReady();
    const unsubscribeReadiness = subscribePluginReadiness(() => {
      const ready = isLocalPluginRuntimeReady();
      if (ready && !wasReady) respond();
      wasReady = ready;
    });

    return () => {
      if (timer) clearTimeout(timer);
      unsubscribe();
      unsubscribeReadiness();
    };
  }, []);
}
