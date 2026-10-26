#pragma once

// Page scripts shared by the macOS and Windows hosts. They run in the OBS page's
// main frame after the document is parsed. Each host first defines
// window.__irpPost(name, value) for its own bridge (WebKit message handlers or
// WebView2 web messages); the host validates every value it receives.
namespace irp::scripts {

// Observe visible key/stat geometry only; the note canvas intentionally reserves
// a larger animation viewport. No polling or layout reads run during gameplay.
inline constexpr const char *kSelection = R"JS((() => {
  let editing = false, pending = false, previous = '';
  const measure = () => {
    pending = false;
    const w = innerWidth, h = innerHeight;
    if (!w || !h) return;
    let l = w, t = h, r = 0, b = 0;
    document.querySelectorAll('[data-state], [data-key-element], .absolute.select-none').forEach(el => {
      const s = getComputedStyle(el);
      if (s.display === 'none' || s.visibility === 'hidden' || s.opacity === '0') return;
      const a = el.getBoundingClientRect();
      if (!a.width || !a.height) return;
      l = Math.min(l, a.left); t = Math.min(t, a.top); r = Math.max(r, a.right); b = Math.max(b, a.bottom);
    });
    l = Math.max(0, l); t = Math.max(0, t); r = Math.min(w, r); b = Math.min(h, b);
    const v = r > l && b > t ? [l / w, t / h, (r - l) / w, (b - t) / h] : [0, 0, 0, 0];
    const k = v.join(',');
    if (k !== previous) { previous = k; window.__irpPost('irpSelection', v); }
  };
  const schedule = () => { if (editing && !pending) { pending = true; setTimeout(measure, 32); } };
  window.__irpSetEditing = value => { editing = !!value; if (editing) schedule(); };
  new MutationObserver(schedule).observe(document.documentElement,
    { childList: true, subtree: true, attributes: true, attributeFilter: ['style', 'class', 'hidden'] });
  addEventListener('resize', schedule);
  setTimeout(measure, 0);
})();)JS";

// Read the viewer's own setting, without changing its desktop/OBS preferences.
// Wait only for bootstrap, then receive changes through its public settings API.
inline constexpr const char *kPadding = R"JS((() => {
  let observer;
  const start = () => {
    const api = window.api?.settings;
    if (!api?.get || !api?.onChanged) return false;
    observer?.disconnect();
    let revision = 0;
    const publish = state => {
      const p = state?.gridSettings?.overlayPadding;
      if (typeof p === 'number' && Number.isFinite(p)) window.__irpPost('irpPadding', p);
    };
    api.onChanged(state => { revision++; publish(state); });
    const version = revision;
    api.get().then(state => { if (revision === version) publish(state); }).catch(() => {});
    return true;
  };
  if (!start()) {
    observer = new MutationObserver(start);
    observer.observe(document.documentElement, { childList: true, subtree: true });
  }
})();)JS";

// The renderer publishes layout dimensions, not its current clipped browser
// size. Attribute filtering avoids reacting to input highlights or animation.
inline constexpr const char *kLayout = R"JS((() => {
  let bootstrapObserver;
  const names = ['data-dmn-viewer-width', 'data-dmn-viewer-height', 'data-dmn-viewer-padding'];
  const start = () => {
    const root = document.getElementById('root');
    if (!root) return;
    bootstrapObserver?.disconnect();
    let previous = '';
    const publish = () => {
      if (!names.every(name => root.hasAttribute(name))) return;
      const values = names.map(name => Number(root.getAttribute(name)));
      const key = values.join(',');
      if (key === previous) return;
      previous = key;
      window.__irpPost('irpLayout', values);
    };
    new MutationObserver(publish).observe(root, { attributes: true, attributeFilter: names });
    publish();
  };
  if (document.getElementById('root')) start();
  else {
    bootstrapObserver = new MutationObserver(start);
    bootstrapObserver.observe(document.documentElement, { childList: true, subtree: true });
  }
})();)JS";

} // namespace irp::scripts
