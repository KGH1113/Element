import { createElement } from 'react';
import { flushSync } from 'react-dom';
import { createRoot } from 'react-dom/client';
import { expect, it } from 'vitest';
import { VirtualClock } from '../../render/virtual-clock.mjs';
import { useNoteSystem } from './useNoteSystem';

it('removes a completed note at its exact fractional cleanup deadline', () => {
  const clock = new VirtualClock();
  const restore = clock.install();
  let system!: ReturnType<typeof useNoteSystem>;
  function Harness() {
    system = useNoteSystem({
      noteEffect: true,
      noteSettings: { speed: 1234, trackHeight: 123 },
    });
    return null;
  }
  const root = createRoot(document.createElement('div'));
  flushSync(() => root.render(createElement(Harness)));
  try {
    flushSync(() => system.handleKeyDown('A', 0));
    flushSync(() => clock.advanceTo(350));
    flushSync(() => system.handleKeyUp('A', 350));
    const note = system.notesRef.current.A[0];
    expect(note.isActive).toBe(false);
    const deadline = note.endTime! + (323 * 1000) / 1234;
    // The old pixel conversion yielded 322.99999999999994 at this deadline.
    flushSync(() => clock.advanceTo(deadline));
    expect(system.notesRef.current.A).toBeUndefined();
    expect(clock.timers.size).toBe(0);
  } finally {
    flushSync(() => root.unmount());
    restore();
  }
});
