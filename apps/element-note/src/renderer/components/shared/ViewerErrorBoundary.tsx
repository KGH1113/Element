import React from 'react';

/** First wait before drawing again; it doubles on each failure in a row. */
export const VIEWER_RETRY_FIRST_MS = 1000;
export const VIEWER_RETRY_LONGEST_MS = 30_000;
/** A viewer that keeps drawing this long counts as healthy again. */
export const VIEWER_HEALTHY_MS = 10_000;

interface ViewerErrorBoundaryProps {
  /** What the drawing is made from. A change draws again at once. */
  retryOn: readonly unknown[];
  children: React.ReactNode;
}

interface ViewerErrorBoundaryState {
  failed: boolean;
  retryOn: readonly unknown[];
}

function sameItems(a: readonly unknown[], b: readonly unknown[]): boolean {
  return a.length === b.length && a.every((item, index) => item === b[index]);
}

/**
 * Wraps a key viewer's drawing, not the parts that receive the app's data.
 * Without it, one drawing error removes the viewer for good: in the game
 * the page stays but shows nothing. With it, the viewer disappears only
 * until its data changes, or for a wait that grows while it keeps failing.
 */
export class ViewerErrorBoundary extends React.Component<
  ViewerErrorBoundaryProps,
  ViewerErrorBoundaryState
> {
  state: ViewerErrorBoundaryState = {
    failed: false,
    retryOn: this.props.retryOn,
  };

  private failuresInARow = 0;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private healthyTimer: ReturnType<typeof setTimeout> | null = null;

  static getDerivedStateFromError(): Partial<ViewerErrorBoundaryState> {
    return { failed: true };
  }

  static getDerivedStateFromProps(
    props: ViewerErrorBoundaryProps,
    state: ViewerErrorBoundaryState,
  ): Partial<ViewerErrorBoundaryState> | null {
    if (sameItems(props.retryOn, state.retryOn)) return null;
    return { retryOn: props.retryOn, failed: false };
  }

  componentDidCatch(error: unknown, info: React.ErrorInfo) {
    this.clearTimers();
    const wait = Math.min(
      VIEWER_RETRY_LONGEST_MS,
      VIEWER_RETRY_FIRST_MS * 2 ** this.failuresInARow,
    );
    this.failuresInARow += 1;
    console.error(
      `[Viewer] Drawing failed; drawing again when its data changes or in ${wait} ms`,
      error,
      info.componentStack,
    );
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      this.setState((state) => (state.failed ? { failed: false } : null));
    }, wait);
  }

  componentDidUpdate(
    _props: ViewerErrorBoundaryProps,
    previous: ViewerErrorBoundaryState,
  ) {
    if (!previous.failed || this.state.failed) return;
    this.clearTimers();
    this.healthyTimer = setTimeout(() => {
      this.healthyTimer = null;
      this.failuresInARow = 0;
    }, VIEWER_HEALTHY_MS);
  }

  componentWillUnmount() {
    this.clearTimers();
  }

  private clearTimers() {
    if (this.retryTimer !== null) clearTimeout(this.retryTimer);
    if (this.healthyTimer !== null) clearTimeout(this.healthyTimer);
    this.retryTimer = null;
    this.healthyTimer = null;
  }

  render() {
    return this.state.failed ? null : this.props.children;
  }
}
