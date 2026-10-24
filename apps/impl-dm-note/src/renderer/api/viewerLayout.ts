/** Publish the computed CSS viewport for an embedded OBS host to observe. */
export function publishViewerLayout(
  width: number,
  height: number,
  padding: number,
): void {
  const root = document.querySelector<HTMLElement>('[data-dmn-viewer-surface]');
  if (
    !root ||
    ![width, height, padding].every(Number.isFinite) ||
    width <= 0 ||
    height <= 0 ||
    padding < 0
  )
    return;

  const viewportWidth = Math.min(4096, Math.ceil(width));
  const viewportHeight = Math.min(4096, Math.ceil(height));
  const values = {
    'data-dmn-viewer-width': viewportWidth,
    'data-dmn-viewer-height': viewportHeight,
    'data-dmn-viewer-padding': Math.min(
      padding,
      (Math.min(viewportWidth, viewportHeight) - 1) / 2,
    ),
  };
  for (const [name, value] of Object.entries(values)) {
    const text = String(value);
    if (root.getAttribute(name) !== text) root.setAttribute(name, text);
  }
}
