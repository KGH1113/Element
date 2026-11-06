/**
 * A read-only, local Tauri surface. No RPC reaches the running desktop app.
 * @param {string | { mode: 'recorded', counters?: Record<string, Record<string, number>> | null }} [counterBaseline]
 *   'zero', 'live', or a run's recorded counts.
 */
export function installFrozenShim(
  snapshot,
  viewerKind,
  assetUrls = {},
  pluginStorage = {},
  pluginElements = [],
  counterBaseline = 'zero',
) {
  const bootstrap = structuredClone(snapshot);
  const settings = bootstrap.settings;
  settings.backgroundColor = 'transparent';
  settings.noteSettings.frameLimit = 0;
  localStorage.setItem('dmnote:locale', settings.language);
  localStorage.setItem('dmnote:locale_initialized', '1');
  const callbacks = new Map(),
    listeners = new Map();
  let nextId = 1;
  // A render counts from zero unless the caller asks to continue from the live
  // totals frozen at begin, or from a run's recorded counts (TUFReplay saves
  // them per run; slots the run lacks continue from the live totals). Either
  // way the render's presses stay here and never reach the live counters.
  const counters =
    counterBaseline === 'live'
      ? structuredClone(bootstrap.keyCounters ?? {})
      : counterBaseline?.mode === 'recorded'
        ? recordedOverLive(bootstrap.keyCounters, counterBaseline.counters)
        : {};
  const storage = structuredClone(pluginStorage);
  const emit = (event, payload) => {
    for (const [id, listener] of [...listeners])
      if (listener.event === event)
        callbacks.get(listener.handler)?.({ event, id, payload });
  };
  const getters = {
    app_bootstrap: () => ({
      ...bootstrap,
      keyCounters: structuredClone(counters),
    }),
    settings_get: () => settings,
    keys_get: () => bootstrap.keys,
    keys_get_counters: () => counters,
    editor_get: () => ({
      revision: bootstrap.editorRevision ?? 0,
      document: {
        schemaVersion: 1,
        keys: bootstrap.keys || {},
        keyPositions: bootstrap.positions || {},
        statPositions: bootstrap.statPositions || {},
        graphPositions: bootstrap.graphPositions || {},
        knobPositions: bootstrap.knobPositions || {},
        spritePositions: bootstrap.spritePositions || {},
        layerGroups: bootstrap.layerGroups || {},
      },
    }),
    positions_get: () => bootstrap.positions,
    stat_positions_get: () => bootstrap.statPositions || {},
    graph_positions_get: () => bootstrap.graphPositions || {},
    knob_positions_get: () => bootstrap.knobPositions || {},
    sprite_positions_get: () => bootstrap.spritePositions || {},
    layer_groups_get: () => bootstrap.layerGroups || {},
    note_tab_get_all: () => bootstrap.tabNoteOverrides || {},
    css_get: () => settings.customCSS,
    css_get_use: () => settings.useCustomCSS,
    css_tab_get_all: () => bootstrap.tabCssOverrides || {},
    js_get: () => settings.customJS,
    js_get_use: () => settings.useCustomJS,
  };
  const invoke = async (command, args = {}) => {
    if (command === 'plugin:event|listen') {
      const id = nextId++;
      listeners.set(id, { event: args.event, handler: args.handler });
      return id;
    }
    if (command === 'plugin:event|unlisten') {
      const listener = listeners.get(args.eventId);
      if (listener) callbacks.delete(listener.handler);
      listeners.delete(args.eventId);
      return;
    }
    if (command === 'plugin:event|emit' || command === 'plugin:event|emit_to') {
      emit(args.event, args.payload);
      return;
    }
    if (getters[command]) return structuredClone(getters[command]());
    if (command === 'plugin_instances_get') {
      const saved = storage[`${args.pluginId}/instances`];
      return structuredClone({
        pluginId: args.pluginId,
        instances: Array.isArray(saved) ? saved : [],
        modelRevision: 0,
        authorityGeneration: 0,
      });
    }
    if (command === 'plugin:window|current_monitor')
      return {
        name: 'Export',
        size: { width: innerWidth, height: innerHeight },
        position: { x: 0, y: 0 },
        workArea: {
          size: { width: innerWidth, height: innerHeight },
          position: { x: 0, y: 0 },
        },
        scaleFactor: 1,
      };
    if (command === 'plugin:window|is_decorated') return false;
    if (command === 'plugin:window|is_always_on_top') return false;
    if (
      command === 'plugin_bridge_send' ||
      command === 'plugin_bridge_send_to'
    ) {
      if (args.messageType === 'plugin:displayElements:request')
        emit('plugin-bridge:message', {
          type: 'plugin:displayElements:sync',
          data: { elements: pluginElements },
        });
      else
        emit('plugin-bridge:message', {
          type: args.messageType,
          data: args.data,
        });
      return;
    }
    if (command === 'plugin_storage_get')
      return structuredClone(storage[args.key] ?? null);
    if (command === 'plugin_storage_set') {
      storage[args.key] = structuredClone(args.value);
      return;
    }
    if (command === 'plugin_storage_remove') {
      delete storage[args.key];
      return;
    }
    if (command === 'plugin_storage_keys') return Object.keys(storage);
    if (command === 'plugin_storage_has_data')
      return Object.keys(storage).some((key) => key.startsWith(args.prefix));
    if (
      command === 'plugin_storage_clear' ||
      command === 'plugin_storage_clear_by_prefix'
    ) {
      const keys = Object.keys(storage).filter(
        (key) =>
          command === 'plugin_storage_clear' || key.startsWith(args.prefix),
      );
      keys.forEach((key) => delete storage[key]);
      return keys.length;
    }
    if (
      command === 'raw_input_subscribe' ||
      command === 'raw_input_unsubscribe'
    )
      return;
    if (command === 'keys_set_counters') {
      Object.assign(counters, structuredClone(args.counters));
      return counters;
    }
    if (
      [
        'overlay_resize',
        'window_get_cursor_settings',
        'window_get_cursor_position',
        'window_set_cursor',
        'window_set_cursor_visible',
      ].includes(command)
    )
      return null;
    throw new Error(`Unsupported command in frozen export: ${command}`);
  };
  const label = `${viewerKind}-overlay`;
  const internals = {
    invoke,
    transformCallback: (callback, once = false) => {
      const id = nextId++;
      callbacks.set(id, (data) => {
        if (once) callbacks.delete(id);
        callback?.(data);
      });
      return id;
    },
    unregisterCallback: (id) => callbacks.delete(id),
    runCallback: (id, data) => callbacks.get(id)?.(data),
    callbacks,
    convertFileSrc: (path) => {
      if (path.startsWith('data:')) return path;
      if (!Object.hasOwn(assetUrls, path))
        throw new Error(`Asset was not frozen for export: ${path}`);
      return assetUrls[path];
    },
    metadata: {
      currentWindow: { label },
      currentWebview: { label, windowLabel: label },
    },
  };
  globalThis.__dmnRenderInternals = internals;
  globalThis.__dmnRenderEventInternals = {
    unregisterListener: (_event, id) => {
      const entry = listeners.get(id);
      if (entry) callbacks.delete(entry.handler);
      listeners.delete(id);
    },
  };
  window.__dmn_window_type = 'overlay';
  return {
    emit,
    counters,
    storage,
    counterEnabled: settings.keyCounterEnabled,
    ready: () =>
      [...listeners.values()].some((entry) => entry.event === 'keys:state') &&
      [...listeners.values()].some((entry) => entry.event === 'keys:counter'),
    dispose: () => {
      listeners.clear();
      callbacks.clear();
      delete globalThis.__dmnRenderInternals;
      delete globalThis.__dmnRenderEventInternals;
    },
  };
}

/** `recorded` over a copy of `live`, keeping only whole, non-negative presses. */
function recordedOverLive(live, recorded) {
  const counters = structuredClone(live ?? {});
  if (!recorded || typeof recorded !== 'object') return counters;
  for (const [tab, keys] of Object.entries(recorded)) {
    if (!keys || typeof keys !== 'object') continue;
    const tabCounts = (counters[tab] ??= {});
    for (const [key, presses] of Object.entries(keys))
      if (Number.isInteger(presses) && presses >= 0) tabCounts[key] = presses;
  }
  return counters;
}
