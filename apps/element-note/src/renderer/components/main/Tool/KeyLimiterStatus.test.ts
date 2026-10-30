import { describe, expect, it } from 'vitest';
import { getKeyLimiterStatusPresentation } from './keyLimiterStatusPresentation';

describe('getKeyLimiterStatusPresentation', () => {
  it('keeps connection and delivery states distinct', () => {
    expect(
      getKeyLimiterStatusPresentation('connecting', 'idle', false),
    ).toMatchObject({
      labelKey: 'keyLimiter.connecting',
      tone: 'neutral',
    });
    expect(
      getKeyLimiterStatusPresentation('ready', 'sending', false),
    ).toMatchObject({
      labelKey: 'keyLimiter.sending',
      tone: 'progress',
    });
    expect(
      getKeyLimiterStatusPresentation('ready', 'applied', false),
    ).toMatchObject({
      labelKey: 'keyLimiter.applied',
      tone: 'success',
    });
  });

  it('shows persistent warnings after the completion flash settles', () => {
    expect(
      getKeyLimiterStatusPresentation('ready', 'idle', true),
    ).toMatchObject({
      labelKey: 'keyLimiter.partial',
      tone: 'warning',
    });
    expect(
      getKeyLimiterStatusPresentation('retrying', 'idle', false),
    ).toMatchObject({
      labelKey: 'keyLimiter.reconnecting',
      tone: 'warning',
    });
  });

  it('gives terminal and delivery errors precedence', () => {
    expect(
      getKeyLimiterStatusPresentation('version-mismatch', 'sending', false)
        .labelKey,
    ).toBe('keyLimiter.versionMismatch');
    expect(
      getKeyLimiterStatusPresentation('ready', 'error', false).labelKey,
    ).toBe('keyLimiter.deliveryFailed');
  });
});
