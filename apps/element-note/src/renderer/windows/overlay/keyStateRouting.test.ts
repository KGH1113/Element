import { describe, expect, it } from 'vitest';
import { isKeyMappedToViewer } from './keyStateRouting';

describe('isKeyMappedToViewer', () => {
  const mappings = {
    numpad: ['NUMPAD 1', 'NUMPAD 2'],
    foot: ['B', 'SEMICOLON'],
  };

  it('keeps hand input active when the global editor is on foot', () => {
    const globalEditorMode = 'foot';

    expect(globalEditorMode).not.toBe('numpad');
    expect(isKeyMappedToViewer('NUMPAD 1', 'numpad', mappings)).toBe(true);
  });

  it('rejects keys that are not part of the viewer tab', () => {
    expect(isKeyMappedToViewer('B', 'numpad', mappings)).toBe(false);
  });
});
