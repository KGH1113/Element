/** Keep app-owned dynamic styles authorized by the document's native CSP. */
export function getOrCreateStyleElement(id: string): HTMLStyleElement {
  let style = document.getElementById(id) as HTMLStyleElement | null;
  if (!style) {
    style = document.createElement('style');
    style.id = id;
  }
  // Tauri gives the render document's inline style a nonce. Once style-src
  // contains that nonce, unsafe-inline no longer authorizes new style elements.
  // Read the property: browsers deliberately hide nonce attribute values.
  const nonce = document.querySelector<HTMLStyleElement>('style[nonce]')?.nonce;
  if (nonce && !style.nonce) style.nonce = nonce;
  if (!style.isConnected) document.head.appendChild(style);
  return style;
}
