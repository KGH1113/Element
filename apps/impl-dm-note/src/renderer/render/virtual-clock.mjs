/** A monotonic clock whose rAF cadence is the requested output frame cadence. */
export class VirtualClock {
  nowMs = 0;
  nextId = 1;
  timers = new Map();
  frames = new Map();
  afterTask = () => {};
  timerNesting = 0;

  schedule(callback, delay = 0, repeat = false, args = []) {
    if (typeof callback !== 'function')
      throw new Error('String timers are unsupported during export.');
    // Browsers clamp deeply nested timers to 4ms. Without that task progress,
    // a zero-delay retry can keep running forever at exactly the same timestamp.
    const nesting = this.timerNesting + 1;
    const interval = Math.max(
      repeat ? 1 : 0,
      nesting > 5 ? 4 : 0,
      Number(delay) || 0,
    );
    const id = this.nextId++;
    this.timers.set(id, {
      callback,
      due: this.nowMs + interval,
      interval: repeat ? interval : null,
      args,
      nesting,
    });
    return id;
  }

  advanceTo(targetMs) {
    if (!Number.isFinite(targetMs) || targetMs < this.nowMs)
      throw new Error('Export time must be monotonic.');
    let steps = 0;
    for (;;) {
      let selected = null;
      for (const [id, timer] of this.timers) {
        if (
          timer.due <= targetMs &&
          (!selected ||
            timer.due < selected.timer.due ||
            (timer.due === selected.timer.due && id < selected.id))
        )
          selected = { id, timer };
      }
      if (!selected) break;
      if (++steps > 100000)
        throw new Error('Export timer queue did not settle.');
      const { id, timer } = selected;
      this.nowMs = timer.due;
      if (timer.interval === null) this.timers.delete(id);
      else {
        timer.nesting++;
        timer.due += Math.max(timer.interval, timer.nesting > 5 ? 4 : 0);
      }
      const previousNesting = this.timerNesting;
      this.timerNesting = timer.nesting;
      try {
        timer.callback(...timer.args);
        this.afterTask();
      } finally {
        this.timerNesting = previousNesting;
      }
    }
    this.nowMs = targetMs;
  }

  sampleFrame() {
    const frames = [...this.frames];
    this.frames.clear();
    for (const [, callback] of frames) {
      callback(this.nowMs);
      this.afterTask();
    }
  }

  install(target = globalThis) {
    const nativeDate = target.Date;
    const nativeTimers = Object.fromEntries(
      [
        'setTimeout',
        'clearTimeout',
        'setInterval',
        'clearInterval',
        'requestAnimationFrame',
        'cancelAnimationFrame',
      ].map((key) => [key, target[key]]),
    );
    const oldNow = Object.getOwnPropertyDescriptor(target.performance, 'now');
    const readClock = () => this.nowMs;
    const epoch = 946684800000;
    target.Date = class extends nativeDate {
      constructor(...args) {
        super(...(args.length ? args : [epoch + readClock()]));
      }
      static now() {
        return epoch + readClock();
      }
    };
    Object.defineProperty(target.performance, 'now', {
      configurable: true,
      value: () => readClock(),
    });
    target.setTimeout = (callback, delay, ...args) =>
      this.schedule(callback, delay, false, args);
    target.setInterval = (callback, delay, ...args) =>
      this.schedule(callback, delay, true, args);
    target.clearTimeout = target.clearInterval = (id) => this.timers.delete(id);
    target.requestAnimationFrame = (callback) => {
      const id = this.nextId++;
      this.frames.set(id, callback);
      return id;
    };
    target.cancelAnimationFrame = (id) => this.frames.delete(id);
    return () => {
      this.timers.clear();
      this.frames.clear();
      target.Date = nativeDate;
      Object.assign(target, nativeTimers);
      if (oldNow) Object.defineProperty(target.performance, 'now', oldNow);
      else delete target.performance.now;
    };
  }
}
