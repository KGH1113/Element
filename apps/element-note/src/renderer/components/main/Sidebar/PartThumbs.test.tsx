import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { keyPositionSchema } from '@src/types/key/keys';
import {
  CountThumb,
  FaceThumb,
  KeyThumb,
  NoteThumb,
  TextThumb,
} from './PartThumbs';

describe('part thumbnails', () => {
  let root: Root;
  let container: HTMLDivElement;

  const key = (extra: Record<string, unknown> = {}) =>
    keyPositionSchema.parse({
      dx: 300,
      dy: 200,
      width: 140,
      height: 80,
      count: 128,
      noteColor: '#f59e0b',
      noteOpacity: 100,
      useInlineStyles: true,
      backgroundColor: '#1f1f22',
      borderColor: '#8a8a93',
      fontColor: '#f5f5f7',
      ...extra,
    });

  const draw = (node: React.ReactNode) => act(() => root.render(node));

  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  it('draws the key itself, small, with its colors and its shape', () => {
    draw(<FaceThumb position={key()} />);
    const box = container.querySelector<HTMLElement>('.en-mini-key')!;
    // A 140 × 80 key fitted into the well keeps its shape.
    expect(parseFloat(box.style.width) / parseFloat(box.style.height)).toBe(
      140 / 80,
    );
    const face = container.querySelector<HTMLElement>('.en-mini-key-face')!;
    expect(face.style.backgroundColor).toBe('rgb(31, 31, 34)');
    // Where the key is on the canvas plays no part in its picture.
    expect(face.style.transform).toMatch(/^scale\(/);
  });

  it('writes the key name at a size that can be read', () => {
    draw(<KeyThumb position={key()} label="Space" />);
    expect(container.querySelector('.en-key-thumb-label')?.textContent).toBe(
      'Space',
    );
    expect(
      container.querySelector<HTMLElement>('.en-key-thumb-label')!.style.color,
    ).toBe('rgb(245, 245, 247)');
  });

  it('shows the notes in the note color and the count over the key', () => {
    draw(
      <>
        <NoteThumb position={key()} />
        <CountThumb position={key()} />
      </>,
    );
    expect(
      Array.from(
        container.querySelectorAll<HTMLElement>('.en-thumb-notes > i'),
        (note) => note.style.background,
      ),
    ).toEqual(['rgb(245, 158, 11)', 'rgb(245, 158, 11)']);
    expect(container.querySelector('.en-thumb-count')?.textContent).toBe('128');
  });

  it('outlines a face that cannot be seen, as a ghost key has at first', () => {
    const clear = key({
      backgroundColor: 'rgba(31, 31, 34, 0)',
      borderColor: 'rgba(138, 138, 147, 0)',
      fontColor: 'rgba(245, 245, 247, 0)',
    });
    draw(
      <>
        <FaceThumb position={clear} />
        <TextThumb position={clear} />
      </>,
    );
    expect(
      container.querySelector('.en-mini-key')?.hasAttribute('data-empty'),
    ).toBe(true);
    expect(container.querySelector('.en-mini-key-face')).toBeNull();
    expect(
      container.querySelector('.en-thumb-text')?.hasAttribute('data-empty'),
    ).toBe(true);
  });
});
