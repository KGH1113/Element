import { useLayoutEffect, type RefObject } from 'react';

/**
 * The size the picture is drawn at, in CSS pixels (`.en-thumb`); the key
 * viewer is fitted inside it. A frame of another size scales the whole
 * picture (see `useThumbnailScale`).
 */
export const THUMBNAIL_SIZE = { width: 208, height: 128 } as const;
/**
 * Keeps the pictures inside `container` as large as their frames
 * (`.en-card-picture`): frames are one size, so one number, kept in
 * `--en-thumb-scale`, fits them all. Nothing is drawn again when it changes.
 */
export function useThumbnailScale(container: RefObject<HTMLElement | null>) {
  useLayoutEffect(() => {
    const element = container.current;
    if (!element) return;
    const fit = () => {
      const frame = element.querySelector<HTMLElement>('.en-card-picture');
      const width = frame?.clientWidth ?? 0;
      if (width <= 0) return;
      element.style.setProperty(
        '--en-thumb-scale',
        String(width / THUMBNAIL_SIZE.width),
      );
    };
    fit();
    if (typeof ResizeObserver !== 'function') return;
    const observer = new ResizeObserver(fit);
    observer.observe(element);
    return () => observer.disconnect();
  }, [container]);
}
