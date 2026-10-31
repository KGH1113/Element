import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Key } from '@components/shared/Key';
import { createDefaultCounterSettings } from '@src/types/key/keys';
import { setKeyActive } from '@stores/signals/keySignals';
import { setKeyCounter } from '@stores/signals/keyCounterSignals';
import { applyStatsSnapshot } from '@stores/signals/statsSignals';
import InsideCounterLayout from './InsideCounterLayout';
import StatItem from './StatItem';

const styleCalls = vi.hoisted(() => ({ count: 0 }));

vi.mock('@hooks/overlay/useKeyElementStyles', async (importOriginal) => {
  const original =
    await importOriginal<typeof import('@hooks/overlay/useKeyElementStyles')>();
  return {
    ...original,
    computeKeyElementStyles: (
      ...args: Parameters<typeof original.computeKeyElementStyles>
    ) => {
      styleCalls.count += 1;
      return original.computeKeyElementStyles(...args);
    },
  };
});

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

const defaultCounter = createDefaultCounterSettings();
const insideCounter = {
  ...defaultCounter,
  enabled: true,
  placement: 'inside' as const,
  animation: {
    ...defaultCounter.animation,
    enabled: false,
  },
};

let container: HTMLDivElement;
let root: Root;

beforeEach(() => {
  container = document.createElement('div');
  document.body.appendChild(container);
  act(() => {
    root = createRoot(container);
  });
  styleCalls.count = 0;
});

afterEach(() => {
  act(() => root.unmount());
  container.remove();
});

const counterTexts = () =>
  [...container.querySelectorAll('span.counter')].map(
    (element) => element.textContent,
  );

describe('inside counter signal subscription', () => {
  it('updates a Key count without rerendering the Key body', () => {
    const mode = 'subscription-hand';
    const key = 'subscription-key';
    act(() => {
      setKeyActive(key, false);
      setKeyCounter(mode, key, 0);
      root.render(
        <Key
          keyName="A"
          globalKey={key}
          mode={mode}
          counterEnabled
          position={{
            dx: 0,
            dy: 0,
            width: 60,
            height: 60,
            counter: insideCounter,
          }}
        />,
      );
    });
    const rendersAfterMount = styleCalls.count;
    const keyElement = container.firstElementChild as HTMLElement;
    const inactiveStyle = keyElement.getAttribute('style');
    expect(keyElement.dataset.state).toBe('inactive');

    act(() => setKeyCounter(mode, key, 7));
    expect(counterTexts()).toEqual(['7']);
    expect(styleCalls.count).toBe(rendersAfterMount);

    act(() => setKeyActive(key, true));
    expect(keyElement.dataset.state).toBe('active');
    expect(keyElement.getAttribute('style')).not.toBe(inactiveStyle);
    expect(styleCalls.count).toBe(rendersAfterMount + 1);
  });

  it('keeps hand and foot counters isolated for the same key', () => {
    const key = 'shared-mode-key';
    act(() => {
      setKeyActive(key, false);
      setKeyCounter('hand', key, 1);
      setKeyCounter('foot', key, 2);
      root.render(
        <>
          <Key
            keyName="A"
            globalKey={key}
            mode="hand"
            counterEnabled
            position={{
              dx: 0,
              dy: 0,
              width: 60,
              height: 60,
              counter: insideCounter,
            }}
          />
          <Key
            keyName="A"
            globalKey={key}
            mode="foot"
            counterEnabled
            position={{
              dx: 0,
              dy: 0,
              width: 60,
              height: 60,
              counter: insideCounter,
            }}
          />
        </>,
      );
    });
    const rendersAfterMount = styleCalls.count;

    act(() => setKeyCounter('hand', key, 9));

    expect(counterTexts()).toEqual(['9', '2']);
    expect(styleCalls.count).toBe(rendersAfterMount);
  });

  it('updates a stat count without rerendering the StatItem body', () => {
    act(() => {
      applyStatsSnapshot({ kps: 0, kpsAvg: 0, kpsMax: 0, total: 0 });
      root.render(
        <StatItem
          statType="total"
          label="Total"
          counterEnabled
          position={{
            dx: 0,
            dy: 0,
            width: 100,
            height: 40,
            counter: insideCounter,
          }}
        />,
      );
    });
    const rendersAfterMount = styleCalls.count;

    act(() => applyStatsSnapshot({ kps: 3, kpsAvg: 1, kpsMax: 3, total: 12 }));

    expect(counterTexts()).toEqual(['12']);
    expect(styleCalls.count).toBe(rendersAfterMount);
  });

  it('keeps the numeric preview path available without a signal', () => {
    act(() => {
      root.render(
        <InsideCounterLayout
          count={23}
          labelText="A"
          textStyle={{}}
          active={false}
          counterSettings={insideCounter}
        />,
      );
    });

    expect(counterTexts()).toEqual(['23']);
  });
});
