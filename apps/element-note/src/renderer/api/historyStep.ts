/**
 * 실행 취소 한 단계의 경계.
 *
 * 누르기 한 번이나 키 한 번에서 나온 저장은 모두 같은 단계다. 끄는 동안의 저장과 놓을 때의
 * 저장, 여러 요소 종류의 저장이 같은 ID를 받아 네이티브 히스토리에서 한 단계로 합쳐진다.
 * 입력 칸에 글자를 이어서 넣는 동안과, 그 칸을 떠나며 값을 확정하는 저장도 한 단계다.
 */

import { isTextEntry } from '@utils/core/keyboardInput';

let listening = false;
/** 지금 진행 중인 단계. */
let current: string | undefined;
/** 마지막 경계가 끝낸 단계와, 그때 입력 중이던 칸. */
let left: string | undefined;
let leftField: Element | null = null;
/** 그 칸이 포커스를 잃는 중이다. 이때 확정하는 값은 끝난 단계의 마지막 저장이다. */
let settling = false;
/** 입력 칸에 글자를 이어서 넣는 중이다. */
let typing = false;

const MODIFIER_KEYS = new Set(['Shift', 'Control', 'Alt', 'Meta']);

/**
 * 지금 저장이 속한 단계의 ID. 경계를 듣지 않는 창(키뷰어)에서는 undefined이고,
 * 그때는 저장마다 따로 기록된다.
 */
export function historyStepId(): string | undefined {
  if (!listening) return undefined;
  return settling ? left : current;
}

/** 편집 창에서 한 번 켠다. 돌려준 함수로 끈다. */
export function startHistorySteps(target: Window = window): () => void {
  /** 새 단계를 시작한다. fieldBeingLeft는 이 동작으로 떠나게 될 입력 칸이다. */
  const begin = (fieldBeingLeft: Element | null) => {
    left = current;
    current = crypto.randomUUID();
    leftField = fieldBeingLeft;
    settling = false;
  };
  const focusedField = (pressed: EventTarget | null) => {
    const focused = target.document.activeElement;
    const inside = pressed instanceof Node && focused?.contains(pressed);
    return isTextEntry(focused) && !inside ? focused : null;
  };

  const handlePress = (event: Event) => {
    typing = false;
    begin(focusedField(event.target));
  };
  const handleKeyDown = (event: KeyboardEvent) => {
    // 누르고 있는 키의 반복과, 끌면서 누르는 보조 키는 같은 동작이다.
    if (event.repeat || MODIFIER_KEYS.has(event.key)) return;
    // Tab은 다음 칸으로 넘어가므로 글자 입력을 끝낸다.
    const entersText = event.key !== 'Tab' && isTextEntry(event.target);
    if (typing && entersText) return;
    typing = entersText;
    begin(entersText ? null : focusedField(null));
  };
  const handleLeaving = (event: FocusEvent) => {
    if (!leftField || event.target !== leftField) return;
    settling = true;
    // 누군가 이벤트 전파를 막아 handleLeft가 불리지 않아도 풀리게 한다.
    setTimeout(handleLeft, 0);
  };
  const handleLeft = () => {
    if (!settling) return;
    settling = false;
    leftField = null;
  };

  listening = true;
  typing = false;
  begin(null);
  target.addEventListener('pointerdown', handlePress, true);
  target.addEventListener('keydown', handleKeyDown, true);
  target.addEventListener('focusout', handleLeaving, true);
  target.addEventListener('focusout', handleLeft);
  return () => {
    listening = false;
    current = undefined;
    left = undefined;
    leftField = null;
    settling = false;
    target.removeEventListener('pointerdown', handlePress, true);
    target.removeEventListener('keydown', handleKeyDown, true);
    target.removeEventListener('focusout', handleLeaving, true);
    target.removeEventListener('focusout', handleLeft);
  };
}
