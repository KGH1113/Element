import { useEffect, useState } from 'react';

// The top bar and the settings both remount as screens change. Starting from
// the last known state keeps the switch from flicking while the app answers.
let lastKnownVisible = true;

/**
 * Whether the key viewer is shown on the desktop, and a way to show or hide
 * it. The desktop is one of three places the key viewer can be shown; which
 * one is in use is chosen in settings ("Where to show").
 */
export function useOverlayVisibility() {
  const [visible, setVisibleState] = useState(lastKnownVisible);

  useEffect(() => {
    const setVisible = (next: boolean) => {
      lastKnownVisible = next;
      setVisibleState(next);
    };
    window.api.overlay
      .get()
      .then((state) => setVisible(state.visible))
      .catch((error) => {
        console.error('Failed to fetch overlay visibility', error);
      });
    return window.api.overlay.onVisibility(({ visible: next }) =>
      setVisible(next),
    );
  }, []);

  const set = (next: boolean) => {
    if (next === visible) return;
    setVisibleState(next);
    window.api.overlay.setVisible(next).catch((error) => {
      console.error('Failed to show or hide the overlay', error);
      // It goes back to what is really on screen.
      setVisibleState(!next);
    });
  };
  const toggle = () => set(!visible);

  return { visible, set, toggle };
}
