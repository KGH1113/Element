/**
 * Raw Key Event Bus
 * 백엔드에서 raw input 이벤트를 구독 기반으로 수신
 * 구독자가 있을 때만 백엔드가 이벤트를 emit하도록 최적화
 */

import { invoke } from '@tauri-apps/api/core';
import { listen } from '@tauri-apps/api/event';

export type RawInputPayload = {
  device: 'keyboard' | 'mouse' | 'gamepad' | 'unknown';
  label: string;
  labels: string[];
  state: string;
};

export type RawKeyEventListener = (payload: RawInputPayload) => void;

class RawKeyEventBus {
  private listeners: Set<RawKeyEventListener> = new Set();
  private initialized = false;
  private unlistenFn: (() => void) | null = null;
  // Whether the backend streams raw input to this window right now.
  private backendSubscribed = false;
  // Backend subscribe/unsubscribe calls run one at a time, in order.
  private backendSync: Promise<void> = Promise.resolve();

  /**
   * 백엔드 raw input 이벤트 구독 초기화
   * 첫 번째 리스너가 등록될 때 자동으로 호출됨
   */
  private async initialize() {
    if (this.initialized) return;
    this.initialized = true;

    try {
      // 백엔드 이벤트 리스너 등록
      const unlisten = await listen<RawInputPayload>(
        'input:raw',
        ({ payload }) => {
          // 모든 리스너에게 브로드캐스트
          this.listeners.forEach((listener) => {
            try {
              listener(payload);
            } catch (error) {
              console.error('[RawKeyEventBus] Listener error:', error);
            }
          });
        },
      );

      this.unlistenFn = unlisten;
    } catch (error) {
      console.error('[RawKeyEventBus] Failed to initialize:', error);
      this.initialized = false;
    }
  }

  /**
   * Raw key 이벤트 리스너 등록
   * The returned function removes the listener at once, even while the
   * backend is still answering, so a removed listener never hears a key.
   */
  subscribe(listener: RawKeyEventListener): () => void {
    this.listeners.add(listener);
    this.syncBackend();

    return () => {
      if (!this.listeners.delete(listener)) return;
      this.syncBackend();
    };
  }

  /** Keeps the backend streaming raw input exactly while someone listens. */
  private syncBackend() {
    this.backendSync = this.backendSync.then(() => this.reconcileBackend());
  }

  private async reconcileBackend() {
    const wanted = this.listeners.size > 0;
    if (wanted === this.backendSubscribed) return;

    try {
      if (wanted) await this.initialize();
      await invoke(wanted ? 'raw_input_subscribe' : 'raw_input_unsubscribe');
      this.backendSubscribed = wanted;
    } catch (error) {
      console.error(
        '[RawKeyEventBus] Failed to update the backend subscription:',
        error,
      );
    }
  }

  /**
   * 등록된 리스너 수 (디버깅용)
   */
  getListenerCount(): number {
    return this.listeners.size;
  }

  /**
   * 모든 리스너 제거 및 정리
   */
  async dispose() {
    this.listeners.clear();
    this.syncBackend();
    await this.backendSync;

    if (this.unlistenFn) {
      this.unlistenFn();
      this.unlistenFn = null;
    }

    this.initialized = false;
  }
}

// 싱글톤 인스턴스
export const rawKeyEventBus = new RawKeyEventBus();
