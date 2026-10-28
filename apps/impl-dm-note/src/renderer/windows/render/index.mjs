import { VirtualClock } from '../../render/virtual-clock.mjs';
import { installFrozenShim } from '../../render/frozen-shim.mjs';
import { keyCandidates } from '../../render/timing.mjs';

const nativeInvoke = window.__TAURI_INTERNALS__.invoke.bind(
  window.__TAURI_INTERNALS__,
);
const nativeTimeout = window.setTimeout.bind(window);
const realSleep = (ms) => new Promise((resolve) => nativeTimeout(resolve, ms));
const acknowledge = (result, error = null) =>
  nativeInvoke('dmnote_render_ack', { result, error });
// This callback is private to the backend and is never a plugin clock source.
const paint = async () => {
  // Hidden native windows can suspend real rAF. Finish GPU work and yield a
  // task; WK snapshot/WebView2 capture provides the compositor completion ACK.
  for (const canvas of document.querySelectorAll('canvas')) {
    const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
    if (gl) gl.finish();
  }
  await realSleep(0);
};

async function start() {
  const config = window.__dmnRenderConfig;
  const clock = new VirtualClock();
  clock.nowMs = Number(config.initialOutputTimeUs) / 1000;
  clock.install();
  let seed = 0x4d4e5231;
  Math.random = () => {
    seed = (Math.imul(1664525, seed) + 1013904223) | 0;
    return (seed >>> 0) / 4294967296;
  };
  for (const [key, value] of Object.entries(config.localStorage || {}))
    if (value != null) localStorage.setItem(key, value);
  const shim = installFrozenShim(
    config.snapshot,
    config.viewerKind,
    config.assetUrls,
    config.pluginStorage,
    config.pluginElements,
  );
  const [
    { createRoot },
    { flushSync },
    React,
    { I18nProvider },
    { default: App },
    { useKeyStore },
    { usePluginDisplayElementStore },
    { ghostKeyLabels },
  ] = await Promise.all([
    import('react-dom/client'),
    import('react-dom'),
    import('react'),
    import('@contexts/I18nContext'),
    import('@windows/overlay/App'),
    import('@stores/data/useKeyStore'),
    import('@stores/plugin/usePluginDisplayElementStore'),
    import('@src/renderer/editor/model/ghostKeys'),
    import('@components/overlay/WebGLTracksOGL'),
    import('@api/dmnoteApi'),
    import('@styles/viewer-compat-v1.css'),
  ]);
  const container = document.getElementById('root');
  container.dataset.dmnViewerSurface = '';
  const root = createRoot(container);
  flushSync(() =>
    root.render(
      React.createElement(I18nProvider, null, React.createElement(App)),
    ),
  );
  const animations = new WeakMap();
  const sampleStyles = () => {
    flushSync(() => {});
    for (const animation of document.getAnimations()) {
      if (!animations.has(animation)) {
        animations.set(animation, clock.nowMs);
        animation.pause();
      }
      animation.currentTime = clock.nowMs - animations.get(animation);
    }
  };
  clock.afterTask = sampleStyles;
  const deadline = 1000;
  for (
    let attempt = 0;
    attempt < deadline &&
    (!useKeyStore.getState().isBootstrapped || !shim.ready());
    attempt++
  )
    await realSleep(10);
  if (!useKeyStore.getState().isBootstrapped || !shim.ready())
    throw new Error('The render overlay did not initialize.');
  flushSync(() =>
    usePluginDisplayElementStore
      .getState()
      .setElements(config.pluginElements || [], { skipSync: true }),
  );
  // Plugin restoration includes a 100ms timer; finish it before the first input.
  clock.advanceTo(clock.nowMs + 100);
  clock.sampleFrame();
  sampleStyles();
  await document.fonts.ready;
  await Promise.all([...document.images].map((image) => image.decode()));
  // Never give the private render surface keyboard focus or DOM input events.
  for (const event of [
    'keydown',
    'keyup',
    'mousedown',
    'mouseup',
    'pointerdown',
    'pointerup',
  ])
    window.addEventListener(
      event,
      (e) => {
        e.preventDefault();
        e.stopImmediatePropagation();
      },
      true,
    );
  let nextFrame = 0;
  const active = new Set();
  window.__dmnRenderFrame = async (request) => {
    try {
      if (request.frameIndex !== nextFrame)
        throw new Error('Render frame is out of sequence.');
      const keyState = useKeyStore.getState();
      const selectedMode = keyState.selectedKeyType;
      const mappedKeys = keyState.keyMappings[selectedMode] || [];
      // Ghost keys come last, like the native hook's routing order.
      const selectedKeys = mappedKeys.concat(
        ghostKeyLabels(mappedKeys, keyState.positions[selectedMode] || []),
      );
      for (const event of request.events) {
        clock.advanceTo(event.outputTimeUs / 1000);
        sampleStyles();
        const candidates = keyCandidates(event.key);
        // Raw events reach plugins even if this preset has no visible mapped key.
        flushSync(() =>
          shim.emit('input:raw', {
            device: event.key.startsWith('Mouse') ? 'mouse' : 'keyboard',
            label: candidates[0],
            labels: candidates,
            state: event.down ? 'DOWN' : 'UP',
          }),
        );
        // Match the native hook's first mapped label, rather than displaying two
        // aliases for one physical key when a preset contains both label names.
        const matched = selectedKeys.find((key) => candidates.includes(key));
        for (const key of matched ? [matched] : []) {
          if (event.down === active.has(key)) continue;
          if (event.down) active.add(key);
          else active.delete(key);
          flushSync(() => {
            if (event.down && shim.counterEnabled) {
              const byKey = (shim.counters[selectedMode] ||= {});
              const count = (byKey[key] = (byKey[key] || 0) + 1);
              shim.emit('keys:counter', { mode: selectedMode, key, count });
            }
            shim.emit('keys:state', {
              mode: selectedMode,
              key,
              state: event.down ? 'DOWN' : 'UP',
              eventAgeMs: 0,
            });
          });
        }
        await Promise.resolve();
        sampleStyles();
      }
      clock.advanceTo(request.outputTimeUs / 1000);
      clock.sampleFrame();
      await Promise.resolve();
      sampleStyles();
      await paint();
      nextFrame++;
      await acknowledge({
        frameIndex: request.frameIndex,
        outputTimeUs: request.outputTimeUs,
        replayTimeUs: request.replayTimeUs,
        activeKeys: [...active],
        ...(config.verify
          ? {
              counters: shim.counters,
              pluginStorage: shim.storage,
              pluginElements: usePluginDisplayElementStore.getState().elements,
            }
          : {}),
      });
    } catch (error) {
      await acknowledge({}, String(error));
    }
  };
  await acknowledge({ ready: true });
}
start().catch((error) => acknowledge({}, String(error)));
