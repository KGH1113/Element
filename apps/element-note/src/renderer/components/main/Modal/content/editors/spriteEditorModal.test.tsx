import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createSpriteDraft, createSpritePose } from './spriteEditorDraft';
import SpriteEditorModal from './SpriteEditorModal';

const { loadImage, probeImageSize } = vi.hoisted(() => ({
  loadImage: vi.fn(),
  probeImageSize: vi.fn(),
}));

vi.mock('@api/modules/resourceApi', () => ({ imageApi: { load: loadImage } }));
vi.mock('@utils/media/assetProbe', () => ({ probeImageSize }));
vi.mock('@contexts/useTranslation', () => ({
  useTranslation: () => ({ i18n: { language: 'en' } }),
}));
vi.mock('@stores/data/useKeyStore', () => ({
  useKeyStore: (select: (state: unknown) => unknown) =>
    select({ positions: {}, keyMappings: {} }),
}));

(
  globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }
).IS_REACT_ACT_ENVIRONMENT = true;

describe('sprite editor modal', () => {
  let host: HTMLDivElement;
  let root: Root;
  const button = (text: string) => {
    const found = Array.from(document.querySelectorAll('button')).find(
      (element) => element.textContent === text,
    );
    if (!found) throw new Error(`Missing button: ${text}`);
    return found;
  };
  const submit = () => {
    const form = document.querySelector('form');
    if (!form) throw new Error('Missing sprite form');
    form.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
  };

  beforeEach(() => {
    vi.resetAllMocks();
    host = document.createElement('div');
    document.body.append(host);
    root = createRoot(host);
  });
  afterEach(() => {
    act(() => root.unmount());
    host.remove();
  });

  it('keeps the draft and blocks dismissal while a save is pending, then allows retry', async () => {
    let rejectSave!: (error: Error) => void;
    const onSave = vi.fn(
      () =>
        new Promise<void>((_, reject) => {
          rejectSave = reject;
        }),
    );
    const onClose = vi.fn();
    const initial = { ...createSpriteDraft(null), layerName: 'My sprite' };
    act(() =>
      root.render(
        <SpriteEditorModal
          initial={initial}
          mode="4key"
          onSave={onSave}
          onClose={onClose}
        />,
      ),
    );
    act(submit);
    expect(button('Saving…').disabled).toBe(true);
    act(() =>
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
      ),
    );
    expect(onClose).not.toHaveBeenCalled();
    await act(async () => rejectSave(new Error('save failed')));
    expect(document.querySelector('[role="alert"]')?.textContent).toContain(
      'Your edits are kept',
    );
    expect(button('Save').disabled).toBe(false);
    expect(
      document.querySelector<HTMLInputElement>('input[type="text"]')?.value,
    ).toBe('My sprite');
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ layerName: 'My sprite' }),
    );
  });

  it('prevents an unbound pose from reaching the editor commit', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const initial = { ...createSpriteDraft(null), poses: [createSpritePose()] };
    act(() =>
      root.render(
        <SpriteEditorModal
          initial={initial}
          mode="4key"
          onSave={onSave}
          onClose={vi.fn()}
        />,
      ),
    );
    await act(async () => submit());
    expect(onSave).not.toHaveBeenCalled();
    expect(document.querySelector('[role="alert"]')?.textContent).toContain(
      'at least one trigger key',
    );
  });

  it('keeps the prior image when a selected file cannot be decoded', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const initial = {
      ...createSpriteDraft(null),
      baseImage: 'data:image/png;base64,old',
    };
    loadImage.mockResolvedValue({ success: true, imagePath: '/broken.png' });
    probeImageSize.mockResolvedValue(null);
    act(() =>
      root.render(
        <SpriteEditorModal
          initial={initial}
          mode="4key"
          onSave={onSave}
          onClose={vi.fn()}
        />,
      ),
    );
    await act(async () => button('Choose image').click());
    expect(document.querySelector('[role="alert"]')?.textContent).toContain(
      'could not be loaded',
    );
    await act(async () => submit());
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ baseImage: initial.baseImage }),
    );
  });

  it('commits measured image size and aspect ratio after a successful selection', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    loadImage.mockResolvedValue({ success: true, imagePath: '/sprite.png' });
    probeImageSize.mockResolvedValue({ width: 600, height: 300 });
    act(() =>
      root.render(
        <SpriteEditorModal
          initial={null}
          mode="4key"
          onSave={onSave}
          onClose={vi.fn()}
        />,
      ),
    );
    await act(async () => button('Choose image').click());
    await act(async () => submit());
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({
        baseImage: '/sprite.png',
        width: 200,
        height: 100,
        referenceNaturalSize: {
          source: '/sprite.png',
          width: 600,
          height: 300,
        },
      }),
    );
  });
});
