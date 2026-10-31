'use no memo';

import React from 'react';
import type { Signal } from '@preact/signals-react';
import { useSignals } from '@preact/signals-react/runtime';
import CountDisplay, { type CountDisplayProps } from './CountDisplay';

interface SignalCountDisplayProps extends Omit<CountDisplayProps, 'count'> {
  countSignal: Signal<number>;
}

/** Signal 구독을 숫자 span 경계에 격리해 상위 key/stat 렌더를 피한다. */
const SignalCountDisplay = ({
  countSignal,
  ...displayProps
}: SignalCountDisplayProps) => {
  useSignals();
  return <CountDisplay count={countSignal.value ?? 0} {...displayProps} />;
};

export default SignalCountDisplay;
