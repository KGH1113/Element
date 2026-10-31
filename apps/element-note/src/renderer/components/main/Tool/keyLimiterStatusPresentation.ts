import type { KeyLimiterConnectionState } from '@src/renderer/ipc/keyLimiterIpcCoordinator';
import type { KeyLimiterDeliveryState } from '@stores/useKeyLimiterStatusStore';

export type StatusIcon = 'none' | 'progress' | 'check' | 'warning' | 'error';

export interface StatusPresentation {
  labelKey: string;
  tone: 'neutral' | 'progress' | 'success' | 'warning' | 'error';
  icon: StatusIcon;
}

export function getKeyLimiterStatusPresentation(
  connectionState: KeyLimiterConnectionState,
  deliveryState: KeyLimiterDeliveryState,
  hasUnsupportedKeys: boolean,
): StatusPresentation {
  if (connectionState === 'version-mismatch') {
    return {
      labelKey: 'keyLimiter.versionMismatch',
      tone: 'error',
      icon: 'error',
    };
  }
  if (connectionState === 'retrying') {
    return {
      labelKey: 'keyLimiter.reconnecting',
      tone: 'warning',
      icon: 'warning',
    };
  }
  if (
    connectionState === 'disconnected' ||
    connectionState === 'connecting' ||
    connectionState === 'disposed'
  ) {
    return {
      labelKey: 'keyLimiter.connecting',
      tone: 'neutral',
      icon: 'progress',
    };
  }
  if (deliveryState === 'error') {
    return {
      labelKey: 'keyLimiter.deliveryFailed',
      tone: 'error',
      icon: 'error',
    };
  }
  if (deliveryState === 'sending') {
    return {
      labelKey: 'keyLimiter.sending',
      tone: 'progress',
      icon: 'progress',
    };
  }
  if (deliveryState === 'applied') {
    return { labelKey: 'keyLimiter.applied', tone: 'success', icon: 'check' };
  }
  if (deliveryState === 'released') {
    return { labelKey: 'keyLimiter.released', tone: 'success', icon: 'check' };
  }
  if (hasUnsupportedKeys) {
    return { labelKey: 'keyLimiter.partial', tone: 'warning', icon: 'warning' };
  }
  return { labelKey: 'keyLimiter.connected', tone: 'success', icon: 'none' };
}
