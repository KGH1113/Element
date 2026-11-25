/**
 * 키 입력이 글자 입력 칸으로 가는지, 단축키의 글쇠가 무엇인지 읽는다.
 */

/** 글자를 입력하는 칸인지. 이 칸에 포커스가 있으면 캔버스 단축키는 양보한다. */
export function isTextEntry(target: unknown): target is HTMLElement {
  if (!(target instanceof HTMLElement)) return false;
  return (
    target.tagName === 'INPUT' ||
    target.tagName === 'TEXTAREA' ||
    target.isContentEditable === true
  );
}

/**
 * 단축키의 글쇠를 소문자 한 글자로 돌려준다. 글쇠가 아니면 빈 문자열.
 * 한글 같은 입력 소스에서는 key가 'ㅋ'처럼 나오므로, 그때는 자판 위치(code)로 읽는다.
 * 영문 배열(드보락 등)이 낸 글자와 기호는 그 배열을 따른다.
 */
export function shortcutLetter(
  event: Pick<KeyboardEvent, 'key' | 'code'>,
): string {
  if (/^[a-z]$/i.test(event.key)) return event.key.toLowerCase();
  if (/^[\x20-\x7e]$/.test(event.key)) return '';
  return /^Key[A-Z]$/.test(event.code) ? event.code.slice(3).toLowerCase() : '';
}
