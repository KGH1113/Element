import { describe, expect, it } from 'vitest';
import { isTextEntry, shortcutLetter } from './keyboardInput';

describe('reading a shortcut', () => {
  it('reads the letter the key types', () => {
    expect(shortcutLetter({ key: 'z', code: 'KeyZ' })).toBe('z');
    expect(shortcutLetter({ key: 'Z', code: 'KeyZ' })).toBe('z');
  });

  it('reads the key position when a Korean input source types a jamo', () => {
    expect(shortcutLetter({ key: 'ㅋ', code: 'KeyZ' })).toBe('z');
    expect(shortcutLetter({ key: 'ㅛ', code: 'KeyY' })).toBe('y');
  });

  it('follows a Latin layout instead of the key position', () => {
    // Dvorak: the key at the QWERTY Z position types a semicolon, B's types X.
    expect(shortcutLetter({ key: ';', code: 'KeyZ' })).toBe('');
    expect(shortcutLetter({ key: 'x', code: 'KeyB' })).toBe('x');
  });

  it('reads the key position while an input method holds the key', () => {
    expect(shortcutLetter({ key: 'Process', code: 'KeyZ' })).toBe('z');
  });

  it('is not a letter for other keys', () => {
    expect(shortcutLetter({ key: 'ArrowLeft', code: 'ArrowLeft' })).toBe('');
    expect(shortcutLetter({ key: '1', code: 'Digit1' })).toBe('');
  });
});

describe('telling a text field apart', () => {
  it('knows inputs and text areas', () => {
    expect(isTextEntry(document.createElement('input'))).toBe(true);
    expect(isTextEntry(document.createElement('textarea'))).toBe(true);
  });

  it('leaves buttons, the page and nothing alone', () => {
    expect(isTextEntry(document.createElement('button'))).toBe(false);
    expect(isTextEntry(document.body)).toBe(false);
    expect(isTextEntry(null)).toBe(false);
    expect(isTextEntry(window)).toBe(false);
  });
});
