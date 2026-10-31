use std::{
    collections::HashSet,
    time::{SystemTime, UNIX_EPOCH},
};

use serde::Serialize;
use tauri::{AppHandle, Emitter, State};

use crate::{
    defaults::{default_keys, default_positions},
    errors::CmdResult,
    models::{
        AppStoreData, CustomCssPatch, KeyCounters, KeyMappings, KeyPositions, KeyViewerKind,
        KeyViewerTab, LayerGroups, NoteSettings, NoteSettingsPatch, SelectedViewerTabs,
        SettingsPatchInput,
    },
    state::AppState,
};

const MAX_TABS_PER_VIEWER: usize = 30;

#[derive(Serialize)]
pub struct ModeResponse {
    pub success: bool,
    pub mode: String,
}

#[derive(Serialize)]
pub struct ResetAllResponse {
    pub keys: KeyMappings,
    pub positions: KeyPositions,
    pub tabs: Vec<KeyViewerTab>,
    pub selected_viewer_tabs: SelectedViewerTabs,
    pub selected_key_type: String,
}

#[derive(Serialize)]
pub struct ResetModeResponse {
    pub success: bool,
    pub mode: String,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TabsChangePayload {
    pub tabs: Vec<KeyViewerTab>,
    pub selected_key_type: String,
    pub selected_viewer_tabs: SelectedViewerTabs,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub changed_viewer_kind: Option<KeyViewerKind>,
}

#[derive(Serialize)]
pub struct TabCreateResult {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub result: Option<KeyViewerTab>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

#[derive(Serialize)]
pub struct TabDeleteResult {
    pub success: bool,
    pub selected: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

#[derive(Serialize)]
pub struct TabMutationResult {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub result: Option<TabsChangePayload>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

fn validate_tab_name(
    tabs: &[KeyViewerTab],
    viewer_kind: KeyViewerKind,
    excluded_id: Option<&str>,
    name: &str,
) -> Result<String, &'static str> {
    let trimmed = name.trim();
    if trimmed.is_empty() {
        return Err("invalid-name");
    }
    if trimmed.encode_utf16().count() > 10 {
        return Err("name-too-long");
    }
    if tabs.iter().any(|tab| {
        tab.viewer_kind == viewer_kind
            && Some(tab.id.as_str()) != excluded_id
            && tab.name.trim() == trimmed
    }) {
        return Err("duplicate-name");
    }
    Ok(trimmed.to_string())
}

fn reorder_viewer_tabs(
    tabs: &mut [KeyViewerTab],
    viewer_kind: KeyViewerKind,
    ordered_ids: &[String],
) -> Result<bool, &'static str> {
    let current_display_ids = tabs
        .iter()
        .filter(|tab| tab.viewer_kind == viewer_kind)
        .map(|tab| tab.id.clone())
        .rev()
        .collect::<Vec<_>>();
    let expected = current_display_ids.iter().collect::<HashSet<_>>();
    let requested = ordered_ids.iter().collect::<HashSet<_>>();
    if ordered_ids.len() != current_display_ids.len()
        || requested.len() != ordered_ids.len()
        || requested != expected
    {
        return Err("invalid-order");
    }
    if ordered_ids == current_display_ids {
        return Ok(false);
    }

    let source = tabs.to_vec();
    let desired_storage_ids = ordered_ids.iter().rev().collect::<Vec<_>>();
    for (desired_index, tab) in tabs
        .iter_mut()
        .filter(|tab| tab.viewer_kind == viewer_kind)
        .enumerate()
    {
        let id = desired_storage_ids[desired_index];
        *tab = source
            .iter()
            .find(|candidate| candidate.id == *id)
            .expect("validated tab id must exist")
            .clone();
    }
    Ok(true)
}

#[tauri::command]
pub fn keys_get(state: State<'_, AppState>) -> CmdResult<KeyMappings> {
    Ok(state.store.snapshot().keys)
}

#[tauri::command]
pub fn positions_get(state: State<'_, AppState>) -> CmdResult<KeyPositions> {
    Ok(state.store.snapshot().key_positions)
}

#[tauri::command]
pub fn keys_get_counters(state: State<'_, AppState>) -> CmdResult<KeyCounters> {
    Ok(state.snapshot_key_counters())
}

#[tauri::command]
pub fn keys_update(
    state: State<'_, AppState>,
    app: AppHandle,
    mappings: KeyMappings,
) -> CmdResult<KeyMappings> {
    let updated = state.store.update_keys(mappings)?;
    state.keyboard.update_mappings(updated.clone());
    app.emit("keys:changed", &updated)?;
    state.sync_counters_with_keys(&updated);
    app.emit("keys:counters", &state.snapshot_key_counters())?;
    state.obs_broadcast_counters();
    state.refresh_obs_snapshot();
    Ok(updated)
}

#[tauri::command]
pub fn positions_update(
    state: State<'_, AppState>,
    app: AppHandle,
    positions: KeyPositions,
) -> CmdResult<KeyPositions> {
    let updated = state.store.update_positions(positions)?;
    app.emit("positions:changed", &updated)?;
    state.refresh_obs_snapshot();
    Ok(updated)
}

#[tauri::command]
pub fn keys_set_mode(
    state: State<'_, AppState>,
    app: AppHandle,
    mode: String,
) -> CmdResult<ModeResponse> {
    let success = state.keyboard.set_mode(mode.clone());
    let effective = if success {
        mode
    } else {
        state.keyboard.current_mode()
    };

    state.transfer_active_keys(&effective);
    state.store.update(|store| {
        store.selected_key_type = effective.clone();
        if let Some(tab) = store.tabs.iter().find(|tab| tab.id == effective) {
            match tab.viewer_kind {
                KeyViewerKind::Hand => store.selected_viewer_tabs.hand = effective.clone(),
                KeyViewerKind::Foot => store.selected_viewer_tabs.foot = effective.clone(),
            }
        }
    })?;

    app.emit(
        "keys:mode-changed",
        &serde_json::json!({ "mode": &effective }),
    )?;
    state.refresh_obs_snapshot();
    Ok(ModeResponse {
        success,
        mode: effective,
    })
}

#[tauri::command]
pub fn keys_reset_all(state: State<'_, AppState>, app: AppHandle) -> CmdResult<ResetAllResponse> {
    let keys = default_keys().clone();
    let positions = default_positions().clone();
    let stat_positions = crate::models::StatPositions::new();
    let graph_positions = crate::models::GraphPositions::new();
    let layer_groups = LayerGroups::new();
    let tab_note_overrides = crate::models::TabNoteOverrides::new();
    let language = state.store.snapshot().language;
    let tabs = crate::state::migration::default_viewer_tabs(&language);
    let selected_viewer_tabs = SelectedViewerTabs::default();
    let selected_key_type = selected_viewer_tabs.hand.clone();
    let mut keys = keys;
    keys.insert(selected_key_type.clone(), Vec::new());
    let mut positions = positions;
    positions.insert(selected_key_type.clone(), Vec::new());
    let cleared_tab_css_ids: Vec<String> = state
        .store
        .snapshot()
        .tab_css_overrides
        .keys()
        .cloned()
        .collect();

    state.store.update(|store| {
        store.keys = keys.clone();
        store.key_positions = positions.clone();
        store.stat_positions = stat_positions.clone();
        store.graph_positions = graph_positions.clone();
        store.layer_groups = layer_groups.clone();
        store.tabs = tabs.clone();
        store.selected_viewer_tabs = selected_viewer_tabs.clone();
        store.selected_key_type = selected_key_type.clone();
        store.tab_note_overrides = tab_note_overrides.clone();
        store.tab_css_overrides.clear();
    })?;

    for tab_id in &cleared_tab_css_ids {
        state.unwatch_tab_css(tab_id);
    }

    state.keyboard.update_mappings(keys.clone());
    state.keyboard.set_mode(selected_key_type.clone());
    state.sync_counters_with_keys(&keys);
    let counters_snapshot = state.reset_key_counters();
    state.persist_key_counters()?;

    let mut note_patch = NoteSettingsPatch::default();
    let defaults = NoteSettings::default();
    note_patch.frame_limit = Some(defaults.frame_limit);
    note_patch.speed = Some(defaults.speed);
    note_patch.track_height = Some(defaults.track_height);
    note_patch.reverse = Some(defaults.reverse);
    note_patch.fade_position = Some(defaults.fade_position.clone());
    note_patch.fade_top_px = Some(defaults.fade_top_px);
    note_patch.fade_bottom_px = Some(defaults.fade_bottom_px);
    note_patch.reverse_fade_top_px = Some(defaults.reverse_fade_top_px);
    note_patch.reverse_fade_bottom_px = Some(defaults.reverse_fade_bottom_px);
    note_patch.delayed_note_enabled = Some(defaults.delayed_note_enabled);
    note_patch.short_note_threshold_ms = Some(defaults.short_note_threshold_ms);
    note_patch.short_note_min_length_px = Some(defaults.short_note_min_length_px);
    note_patch.key_display_delay_ms = Some(defaults.key_display_delay_ms);

    let settings_diff = state.settings.apply_patch(SettingsPatchInput {
        background_color: Some("transparent".to_string()),
        note_settings: Some(note_patch),
        laboratory_enabled: Some(false),
        use_custom_css: Some(false),
        custom_css: Some(CustomCssPatch {
            path: Some(None),
            content: Some(String::new()),
        }),
        note_effect: Some(true),
        overlay_locked: Some(false),
        ..SettingsPatchInput::default()
    })?;

    state.emit_settings_changed(&settings_diff, &app)?;

    app.emit("keys:changed", &keys)?;
    app.emit("positions:changed", &positions)?;
    app.emit("statPositions:changed", &stat_positions)?;
    app.emit("graphPositions:changed", &graph_positions)?;
    app.emit("layerGroups:changed", &layer_groups)?;
    app.emit(
        "tabs:changed",
        &TabsChangePayload {
            tabs: tabs.clone(),
            selected_key_type: selected_key_type.clone(),
            selected_viewer_tabs: selected_viewer_tabs.clone(),
            changed_viewer_kind: None,
        },
    )?;
    app.emit(
        "keys:mode-changed",
        &serde_json::json!({ "mode": &selected_key_type }),
    )?;
    app.emit("css:use", &serde_json::json!({ "enabled": false }))?;
    app.emit(
        "css:content",
        &serde_json::json!({ "path": serde_json::Value::Null, "content": "" }),
    )?;
    app.emit("tabNote:changed_all", &tab_note_overrides)?;
    for tab_id in cleared_tab_css_ids {
        app.emit(
            "tabCss:changed",
            &crate::commands::editor::css::TabCssResponse { tab_id, css: None },
        )?;
    }
    app.emit("keys:counters", &counters_snapshot)?;
    state.obs_broadcast_counters();
    state.refresh_obs_snapshot();

    Ok(ResetAllResponse {
        keys,
        positions,
        tabs,
        selected_viewer_tabs,
        selected_key_type,
    })
}

#[tauri::command]
pub fn keys_reset_mode(
    state: State<'_, AppState>,
    app: AppHandle,
    mode: String,
) -> CmdResult<ResetModeResponse> {
    let defaults = default_keys();
    if !defaults.contains_key(&mode) {
        return Ok(ResetModeResponse {
            success: false,
            mode,
        });
    }

    let default_pos = default_positions();

    let snapshot = state.store.snapshot();
    let mut keys = snapshot.keys;
    if let Some(value) = defaults.get(&mode) {
        keys.insert(mode.clone(), value.clone());
    }
    let mut positions = snapshot.key_positions;
    if let Some(value) = default_pos.get(&mode) {
        positions.insert(mode.clone(), value.clone());
    }
    let mut stat_positions = snapshot.stat_positions;
    stat_positions.insert(mode.clone(), Vec::new());
    let mut graph_positions = snapshot.graph_positions;
    graph_positions.insert(mode.clone(), Vec::new());
    let mut layer_groups = snapshot.layer_groups;
    layer_groups.remove(&mode);
    let mut tab_note_overrides = snapshot.tab_note_overrides;
    tab_note_overrides.remove(&mode);
    let mut tab_css_overrides = snapshot.tab_css_overrides;
    let cleared_tab_css = tab_css_overrides.remove(&mode).is_some();

    state.store.update(|store| {
        store.keys = keys.clone();
        store.key_positions = positions.clone();
        store.stat_positions = stat_positions.clone();
        store.graph_positions = graph_positions.clone();
        store.layer_groups = layer_groups.clone();
        store.tab_note_overrides = tab_note_overrides.clone();
        store.tab_css_overrides = tab_css_overrides.clone();
    })?;

    if cleared_tab_css {
        state.unwatch_tab_css(&mode);
    }

    state.keyboard.update_mappings(keys.clone());
    state.sync_counters_with_keys(&keys);
    state.reset_mode_counters(&mode);
    state.persist_key_counters()?;

    app.emit("keys:changed", &keys)?;
    app.emit("positions:changed", &positions)?;
    app.emit("statPositions:changed", &stat_positions)?;
    app.emit("graphPositions:changed", &graph_positions)?;
    app.emit("layerGroups:changed", &layer_groups)?;
    app.emit("tabNote:changed_all", &tab_note_overrides)?;
    if cleared_tab_css {
        app.emit(
            "tabCss:changed",
            &crate::commands::editor::css::TabCssResponse {
                tab_id: mode.clone(),
                css: None,
            },
        )?;
    }
    app.emit("keys:counters", &state.snapshot_key_counters())?;
    state.obs_broadcast_counters();
    state.refresh_obs_snapshot();

    Ok(ResetModeResponse {
        success: true,
        mode,
    })
}

#[tauri::command]
pub fn tabs_list(
    state: State<'_, AppState>,
    viewer_kind: KeyViewerKind,
) -> CmdResult<Vec<KeyViewerTab>> {
    Ok(state
        .store
        .snapshot()
        .tabs
        .into_iter()
        .filter(|tab| tab.viewer_kind == viewer_kind)
        .collect())
}

#[tauri::command]
pub fn tabs_create(
    state: State<'_, AppState>,
    app: AppHandle,
    viewer_kind: KeyViewerKind,
    name: String,
) -> CmdResult<TabCreateResult> {
    let snapshot = state.store.snapshot();
    let trimmed = match validate_tab_name(&snapshot.tabs, viewer_kind, None, &name) {
        Ok(name) => name,
        Err(error) => {
            return Ok(TabCreateResult {
                result: None,
                error: Some(error.to_string()),
            });
        }
    };
    if snapshot
        .tabs
        .iter()
        .filter(|tab| tab.viewer_kind == viewer_kind)
        .count()
        >= MAX_TABS_PER_VIEWER
    {
        return Ok(TabCreateResult {
            result: None,
            error: Some("max-reached".to_string()),
        });
    }

    let id = generate_tab_id(viewer_kind);
    let tab = KeyViewerTab {
        id: id.clone(),
        name: trimmed,
        viewer_kind,
    };

    let mut tabs = snapshot.tabs.clone();
    tabs.push(tab.clone());
    let mut selected_viewer_tabs = snapshot.selected_viewer_tabs.clone();
    match viewer_kind {
        KeyViewerKind::Hand => selected_viewer_tabs.hand = id.clone(),
        KeyViewerKind::Foot => selected_viewer_tabs.foot = id.clone(),
    }

    let mut keys = snapshot.keys.clone();
    keys.insert(id.clone(), Vec::new());
    let mut positions = snapshot.key_positions.clone();
    positions.insert(id.clone(), Vec::new());

    state.store.update(|store| {
        store.tabs = tabs.clone();
        store.selected_viewer_tabs = selected_viewer_tabs.clone();
        store.keys = keys.clone();
        store.key_positions = positions.clone();
        store.selected_key_type = id.clone();
    })?;

    state.keyboard.update_mappings(keys.clone());
    state.keyboard.set_mode(id.clone());
    state.sync_counters_with_keys(&keys);
    state.reset_mode_counters(&id);
    state.persist_key_counters()?;

    app.emit(
        "tabs:changed",
        &TabsChangePayload {
            tabs: tabs.clone(),
            selected_key_type: id.clone(),
            selected_viewer_tabs: selected_viewer_tabs.clone(),
            changed_viewer_kind: Some(viewer_kind),
        },
    )?;
    app.emit("keys:changed", &keys)?;
    app.emit("positions:changed", &positions)?;
    app.emit("keys:mode-changed", &serde_json::json!({ "mode": &id }))?;
    app.emit("keys:counters", &state.snapshot_key_counters())?;
    state.obs_broadcast_counters();
    state.refresh_obs_snapshot();

    Ok(TabCreateResult {
        result: Some(tab),
        error: None,
    })
}

#[tauri::command]
pub fn tabs_rename(
    state: State<'_, AppState>,
    app: AppHandle,
    viewer_kind: KeyViewerKind,
    id: String,
    name: String,
) -> CmdResult<TabMutationResult> {
    let mut error = None;
    let mut changed = false;
    let updated = state.store.update(|store| {
        let Some(index) = store
            .tabs
            .iter()
            .position(|tab| tab.id == id && tab.viewer_kind == viewer_kind)
        else {
            error = Some("not-found".to_string());
            return;
        };
        let validated = match validate_tab_name(&store.tabs, viewer_kind, Some(&id), &name) {
            Ok(name) => name,
            Err(validation_error) => {
                error = Some(validation_error.to_string());
                return;
            }
        };
        changed = store.tabs[index].name != validated;
        store.tabs[index].name = validated;
    })?;

    if let Some(error) = error {
        return Ok(TabMutationResult {
            result: None,
            error: Some(error),
        });
    }

    let payload = TabsChangePayload {
        tabs: updated.tabs,
        selected_key_type: updated.selected_key_type,
        selected_viewer_tabs: updated.selected_viewer_tabs,
        changed_viewer_kind: Some(viewer_kind),
    };
    if changed {
        app.emit("tabs:changed", &payload)?;
        state.refresh_obs_snapshot();
    }
    Ok(TabMutationResult {
        result: Some(payload),
        error: None,
    })
}

#[tauri::command]
pub fn tabs_reorder(
    state: State<'_, AppState>,
    app: AppHandle,
    viewer_kind: KeyViewerKind,
    ordered_ids: Vec<String>,
) -> CmdResult<TabMutationResult> {
    let mut error = None;
    let mut changed = false;
    let updated = state.store.update(|store| {
        match reorder_viewer_tabs(&mut store.tabs, viewer_kind, &ordered_ids) {
            Ok(was_changed) => changed = was_changed,
            Err(reorder_error) => error = Some(reorder_error.to_string()),
        }
    })?;

    if let Some(error) = error {
        return Ok(TabMutationResult {
            result: None,
            error: Some(error),
        });
    }

    let payload = TabsChangePayload {
        tabs: updated.tabs,
        selected_key_type: updated.selected_key_type,
        selected_viewer_tabs: updated.selected_viewer_tabs,
        changed_viewer_kind: Some(viewer_kind),
    };
    if changed {
        app.emit("tabs:changed", &payload)?;
        state.refresh_obs_snapshot();
    }
    Ok(TabMutationResult {
        result: Some(payload),
        error: None,
    })
}

#[tauri::command]
pub fn tabs_delete(
    state: State<'_, AppState>,
    app: AppHandle,
    id: String,
) -> CmdResult<TabDeleteResult> {
    let snapshot = state.store.snapshot();
    let Some(deleted_tab) = snapshot.tabs.iter().find(|tab| tab.id == id).cloned() else {
        return Ok(TabDeleteResult {
            success: false,
            selected: snapshot.selected_key_type,
            error: Some("not-found".to_string()),
        });
    };
    let group_tabs: Vec<&KeyViewerTab> = snapshot
        .tabs
        .iter()
        .filter(|tab| tab.viewer_kind == deleted_tab.viewer_kind)
        .collect();
    if group_tabs.len() <= 1 {
        return Ok(TabDeleteResult {
            success: false,
            selected: snapshot.selected_key_type,
            error: Some("last-tab".to_string()),
        });
    }

    let tabs: Vec<KeyViewerTab> = snapshot
        .tabs
        .iter()
        .filter(|&tab| tab.id != id)
        .cloned()
        .collect();
    let mut keys = snapshot.keys.clone();
    let mut positions = snapshot.key_positions.clone();
    keys.remove(&id);
    positions.remove(&id);
    let deleted_group_index = group_tabs.iter().position(|tab| tab.id == id).unwrap_or(0);
    let remaining_group_tabs: Vec<&KeyViewerTab> = tabs
        .iter()
        .filter(|tab| tab.viewer_kind == deleted_tab.viewer_kind)
        .collect();
    let fallback_index = deleted_group_index
        .saturating_sub(1)
        .min(remaining_group_tabs.len() - 1);
    let fallback = remaining_group_tabs[fallback_index].id.clone();
    let mut selected_viewer_tabs = snapshot.selected_viewer_tabs.clone();
    match deleted_tab.viewer_kind {
        KeyViewerKind::Hand if selected_viewer_tabs.hand == id => {
            selected_viewer_tabs.hand = fallback.clone();
        }
        KeyViewerKind::Foot if selected_viewer_tabs.foot == id => {
            selected_viewer_tabs.foot = fallback.clone();
        }
        _ => {}
    }
    let next_selected = if snapshot.selected_key_type == id {
        fallback
    } else {
        snapshot.selected_key_type.clone()
    };
    let mut stat_positions = snapshot.stat_positions.clone();
    let mut graph_positions = snapshot.graph_positions.clone();
    let mut knob_positions = snapshot.knob_positions.clone();
    let mut layer_groups = snapshot.layer_groups.clone();
    stat_positions.remove(&id);
    graph_positions.remove(&id);
    knob_positions.remove(&id);
    layer_groups.remove(&id);

    state.store.update(|store| {
        store.tabs = tabs.clone();
        store.selected_viewer_tabs = selected_viewer_tabs.clone();
        store.keys = keys.clone();
        store.key_positions = positions.clone();
        store.stat_positions = stat_positions.clone();
        store.graph_positions = graph_positions.clone();
        store.knob_positions = knob_positions.clone();
        store.layer_groups = layer_groups.clone();
        store.key_counters.remove(&id);
        store.tab_note_overrides.remove(&id);
        store.tab_css_overrides.remove(&id);
        store.selected_key_type = next_selected.clone();
    })?;
    state.unwatch_tab_css(&id);

    state.keyboard.update_mappings(keys.clone());
    state.keyboard.set_mode(next_selected.clone());
    state.sync_counters_with_keys(&keys);
    state.persist_key_counters()?;

    app.emit(
        "tabs:changed",
        &TabsChangePayload {
            tabs: tabs.clone(),
            selected_key_type: next_selected.clone(),
            selected_viewer_tabs: selected_viewer_tabs.clone(),
            changed_viewer_kind: Some(deleted_tab.viewer_kind),
        },
    )?;
    app.emit("keys:changed", &keys)?;
    app.emit("positions:changed", &positions)?;
    app.emit("statPositions:changed", &stat_positions)?;
    app.emit("graphPositions:changed", &graph_positions)?;
    app.emit("knobPositions:changed", &knob_positions)?;
    app.emit("layerGroups:changed", &layer_groups)?;
    app.emit(
        "keys:mode-changed",
        &serde_json::json!({ "mode": &next_selected }),
    )?;
    app.emit("keys:counters", &state.snapshot_key_counters())?;
    state.obs_broadcast_counters();
    state.refresh_obs_snapshot();

    Ok(TabDeleteResult {
        success: true,
        selected: next_selected,
        error: None,
    })
}

#[derive(Serialize)]
pub struct TabSelectResult {
    pub success: bool,
    pub selected: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

fn apply_viewer_tab_selection(store: &mut AppStoreData, viewer_kind: KeyViewerKind, id: &str) {
    store.selected_key_type = id.to_string();
    match viewer_kind {
        KeyViewerKind::Hand => store.selected_viewer_tabs.hand = id.to_string(),
        KeyViewerKind::Foot => store.selected_viewer_tabs.foot = id.to_string(),
    }
}

#[tauri::command]
pub fn tabs_select(
    state: State<'_, AppState>,
    app: AppHandle,
    viewer_kind: KeyViewerKind,
    id: String,
) -> CmdResult<TabSelectResult> {
    let snapshot = state.store.snapshot();
    let exists = snapshot
        .tabs
        .iter()
        .any(|tab| tab.id == id && tab.viewer_kind == viewer_kind);
    if !exists {
        return Ok(TabSelectResult {
            success: false,
            selected: snapshot.selected_key_type,
            error: Some("not-found".to_string()),
        });
    }

    let previous_viewer_mode = match viewer_kind {
        KeyViewerKind::Hand => snapshot.selected_viewer_tabs.hand.clone(),
        KeyViewerKind::Foot => snapshot.selected_viewer_tabs.foot.clone(),
    };

    // Only update the requested viewer group while holding the store write lock.
    // Building the full SelectedViewerTabs value from an earlier snapshot can
    // otherwise overwrite a near-simultaneous selection in the other group.
    let updated = state
        .store
        .update(|store| apply_viewer_tab_selection(store, viewer_kind, &id))?;
    let selected_viewer_tabs = updated.selected_viewer_tabs.clone();
    state.keyboard.set_mode(id.clone());
    state.transfer_active_keys_between(&previous_viewer_mode, &id);

    app.emit("keys:mode-changed", &serde_json::json!({ "mode": &id }))?;
    state.refresh_obs_snapshot();

    app.emit(
        "tabs:changed",
        &TabsChangePayload {
            tabs: updated.tabs,
            selected_key_type: id.clone(),
            selected_viewer_tabs,
            changed_viewer_kind: Some(viewer_kind),
        },
    )?;

    Ok(TabSelectResult {
        success: true,
        selected: id,
        error: None,
    })
}

#[cfg(test)]
mod tab_selection_tests {
    use super::{apply_viewer_tab_selection, reorder_viewer_tabs, validate_tab_name};
    use crate::models::{AppStoreData, KeyViewerKind, KeyViewerTab};

    fn mixed_tabs() -> Vec<KeyViewerTab> {
        vec![
            KeyViewerTab {
                id: "hand-a".to_string(),
                name: "Hand A".to_string(),
                viewer_kind: KeyViewerKind::Hand,
            },
            KeyViewerTab {
                id: "foot-a".to_string(),
                name: "Foot A".to_string(),
                viewer_kind: KeyViewerKind::Foot,
            },
            KeyViewerTab {
                id: "hand-b".to_string(),
                name: "Hand B".to_string(),
                viewer_kind: KeyViewerKind::Hand,
            },
            KeyViewerTab {
                id: "foot-b".to_string(),
                name: "Foot B".to_string(),
                viewer_kind: KeyViewerKind::Foot,
            },
        ]
    }

    #[test]
    fn selecting_foot_preserves_the_current_hand_tab() {
        let mut store = AppStoreData::default();
        store.selected_viewer_tabs.hand = "hand-custom".to_string();

        apply_viewer_tab_selection(&mut store, KeyViewerKind::Foot, "foot-custom");

        assert_eq!(store.selected_viewer_tabs.hand, "hand-custom");
        assert_eq!(store.selected_viewer_tabs.foot, "foot-custom");
        assert_eq!(store.selected_key_type, "foot-custom");
    }

    #[test]
    fn selecting_hand_preserves_the_current_foot_tab() {
        let mut store = AppStoreData::default();
        store.selected_viewer_tabs.foot = "foot-custom".to_string();

        apply_viewer_tab_selection(&mut store, KeyViewerKind::Hand, "hand-custom");

        assert_eq!(store.selected_viewer_tabs.hand, "hand-custom");
        assert_eq!(store.selected_viewer_tabs.foot, "foot-custom");
        assert_eq!(store.selected_key_type, "hand-custom");
    }

    #[test]
    fn reorder_uses_display_order_and_preserves_other_viewer_slots() {
        let mut tabs = mixed_tabs();

        assert!(reorder_viewer_tabs(
            &mut tabs,
            KeyViewerKind::Hand,
            &["hand-a".to_string(), "hand-b".to_string()],
        )
        .unwrap());

        assert_eq!(
            tabs.iter().map(|tab| tab.id.as_str()).collect::<Vec<_>>(),
            ["hand-b", "foot-a", "hand-a", "foot-b"]
        );
        assert_eq!(
            tabs.iter()
                .filter(|tab| tab.viewer_kind == KeyViewerKind::Hand)
                .map(|tab| tab.id.as_str())
                .rev()
                .collect::<Vec<_>>(),
            ["hand-a", "hand-b"]
        );
    }

    #[test]
    fn reorder_rejects_missing_duplicate_and_cross_viewer_ids() {
        let original = mixed_tabs();
        for invalid in [
            vec!["hand-a".to_string()],
            vec!["hand-a".to_string(), "hand-a".to_string()],
            vec!["hand-a".to_string(), "foot-a".to_string()],
        ] {
            let mut tabs = original.clone();
            assert_eq!(
                reorder_viewer_tabs(&mut tabs, KeyViewerKind::Hand, &invalid),
                Err("invalid-order")
            );
            assert_eq!(tabs, original);
        }
    }

    #[test]
    fn tab_names_are_trimmed_scoped_and_count_utf16_units() {
        let tabs = mixed_tabs();
        assert_eq!(
            validate_tab_name(&tabs, KeyViewerKind::Hand, None, "  New  "),
            Ok("New".to_string())
        );
        assert_eq!(
            validate_tab_name(&tabs, KeyViewerKind::Hand, None, "Hand A"),
            Err("duplicate-name")
        );
        assert_eq!(
            validate_tab_name(&tabs, KeyViewerKind::Foot, None, "Hand A"),
            Ok("Hand A".to_string())
        );
        assert_eq!(
            validate_tab_name(&tabs, KeyViewerKind::Hand, None, "😀😀😀😀abc"),
            Err("name-too-long")
        );
        assert_eq!(
            validate_tab_name(&tabs, KeyViewerKind::Hand, Some("hand-a"), "Hand A"),
            Ok("Hand A".to_string())
        );
    }
}

/// undo/redo 시 탭 목록과 손·발 선택 상태를 원자적으로 복원
#[tauri::command]
pub fn tabs_restore(
    state: State<'_, AppState>,
    app: AppHandle,
    tabs: Vec<KeyViewerTab>,
    selected_key_type: String,
    selected_viewer_tabs: SelectedViewerTabs,
) -> CmdResult<()> {
    let mut restored = state.store.snapshot();
    restored.tabs = tabs;
    restored.selected_key_type = selected_key_type;
    restored.selected_viewer_tabs = selected_viewer_tabs;
    let restored = crate::state::migration::normalize_state(restored);
    let tabs = restored.tabs.clone();
    let selected_key_type = restored.selected_key_type.clone();
    let selected_viewer_tabs = restored.selected_viewer_tabs.clone();

    state.store.update(|store| {
        *store = restored.clone();
    })?;

    state.keyboard.set_mode(selected_key_type.clone());
    state.transfer_active_keys(&selected_key_type);

    app.emit(
        "tabs:changed",
        &TabsChangePayload {
            tabs,
            selected_key_type: selected_key_type.clone(),
            selected_viewer_tabs,
            changed_viewer_kind: None,
        },
    )?;
    app.emit(
        "keys:mode-changed",
        &serde_json::json!({ "mode": &selected_key_type }),
    )?;
    state.refresh_obs_snapshot();
    Ok(())
}

#[tauri::command]
pub fn keys_reset_counters(state: State<'_, AppState>, app: AppHandle) -> CmdResult<KeyCounters> {
    let snapshot = state.reset_key_counters();
    state.persist_key_counters()?;
    app.emit("keys:counters", &snapshot)?;
    state.obs_broadcast_counters();
    Ok(snapshot)
}

#[tauri::command]
pub fn keys_reset_counters_mode(
    state: State<'_, AppState>,
    app: AppHandle,
    mode: String,
) -> CmdResult<KeyCounters> {
    state.reset_mode_counters(&mode);
    state.persist_key_counters()?;
    let snapshot = state.snapshot_key_counters();
    app.emit("keys:counters", &snapshot)?;
    state.obs_broadcast_counters();
    Ok(snapshot)
}

#[tauri::command]
pub fn keys_reset_single_counter(
    state: State<'_, AppState>,
    app: AppHandle,
    mode: String,
    key: String,
) -> CmdResult<KeyCounters> {
    state.reset_single_key_counter(&mode, &key);
    state.persist_key_counters()?;
    let snapshot = state.snapshot_key_counters();
    app.emit("keys:counters", &snapshot)?;
    state.obs_broadcast_counters();
    Ok(snapshot)
}

#[tauri::command]
pub fn keys_set_counters(
    state: State<'_, AppState>,
    app: AppHandle,
    counters: KeyCounters,
) -> CmdResult<KeyCounters> {
    let keys_snapshot = state.store.snapshot().keys;
    let updated = state.replace_key_counters(counters, &keys_snapshot)?;
    app.emit("keys:counters", &updated)?;
    state.obs_broadcast_counters();
    Ok(updated)
}

#[tauri::command]
pub fn layer_groups_get(state: State<'_, AppState>) -> CmdResult<LayerGroups> {
    Ok(state.store.snapshot().layer_groups)
}

#[tauri::command]
pub fn layer_groups_update(
    state: State<'_, AppState>,
    app: AppHandle,
    groups: LayerGroups,
) -> CmdResult<LayerGroups> {
    let updated = state.store.update_layer_groups(groups)?;
    app.emit("layerGroups:changed", &updated)?;
    state.refresh_obs_snapshot();
    Ok(updated)
}

fn generate_tab_id(viewer_kind: KeyViewerKind) -> String {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis();
    let prefix = match viewer_kind {
        KeyViewerKind::Hand => "hand",
        KeyViewerKind::Foot => "foot",
    };
    format!("{prefix}-{now}")
}

#[derive(Serialize)]
pub struct RawInputSubscribeResponse {
    pub count: u32,
}

/// Subscribe to raw input stream (increment subscriber count)
#[tauri::command]
pub fn raw_input_subscribe(state: State<'_, AppState>) -> CmdResult<RawInputSubscribeResponse> {
    let count = state.subscribe_raw_input();
    log::debug!("[RawInput] Subscribe: count = {}", count);
    Ok(RawInputSubscribeResponse { count })
}

/// Unsubscribe from raw input stream (decrement subscriber count)
#[tauri::command]
pub fn raw_input_unsubscribe(state: State<'_, AppState>) -> CmdResult<RawInputSubscribeResponse> {
    let count = state.unsubscribe_raw_input();
    log::debug!("[RawInput] Unsubscribe: count = {}", count);
    Ok(RawInputSubscribeResponse { count })
}
