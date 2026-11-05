//! Compatibility commands for Element's existing editor, backed by v2 transactions.
use crate::{
    commands::{
        editor::state::{emit_best_effort, publish_legacy_editor_change},
        run_blocking, run_history_mutation,
    },
    errors::{CmdResult, EditorCommitError},
    models::*,
    state::{history::HistoryScope, store::AuxEditorTransactionOptions},
};
use serde_json::{json, Value};
use std::collections::HashSet;
use tauri::{AppHandle, WebviewWindow};

pub(crate) fn tabs_payload(store: &AppStoreData, kind: Option<KeyViewerKind>) -> Value {
    json!({"tabs":store.custom_tabs,"selectedKeyType":store.selected_key_type,"selectedViewerTabs":store.selected_viewer_tabs,"changedViewerKind":kind})
}

fn select(store: &mut AppStoreData, kind: KeyViewerKind, id: &str) {
    store.selected_key_type = id.into();
    match kind {
        KeyViewerKind::Hand => store.selected_viewer_tabs.hand = id.into(),
        KeyViewerKind::Foot => store.selected_viewer_tabs.foot = id.into(),
    }
}

fn validate_name(
    store: &AppStoreData,
    kind: KeyViewerKind,
    except: Option<&str>,
    name: &str,
) -> Result<String, &'static str> {
    let name = name.trim();
    if name.is_empty() {
        return Err("invalid-name");
    }
    if name.encode_utf16().count() > 10 {
        return Err("name-too-long");
    }
    if store.custom_tabs.iter().any(|tab| {
        tab.viewer_kind == kind && Some(tab.id.as_str()) != except && tab.name.trim() == name
    }) {
        return Err("duplicate-name");
    }
    Ok(name.into())
}

async fn mutate_tabs(
    app: AppHandle,
    window: WebviewWindow,
    name: &'static str,
    kind: Option<KeyViewerKind>,
    mutate: impl FnOnce(&mut AppStoreData) -> Result<Value, EditorCommitError> + Send + 'static,
) -> CmdResult<Value> {
    run_history_mutation(app, window.label().into(), move |app, state, admission| {
        let (transaction, _) =
            state.commit_editor_transaction_preserving_runtime_counters(app, |counters| {
                state
                    .store
                    .commit_aux_editor_transaction_with_runtime_counters_admission(
                        AuxEditorTransactionOptions {
                            scope: HistoryScope::CustomTabs,
                            observed_history_epoch: None,
                            origin: EditorCommitOrigin::LegacyAdapter(name.into()),
                            touched_fields: &[
                                EditorField::Keys,
                                EditorField::KeyPositions,
                                EditorField::StatPositions,
                                EditorField::GraphPositions,
                                EditorField::KnobPositions,
                                EditorField::SpritePositions,
                                EditorField::LayerGroups,
                            ],
                        },
                        admission,
                        counters,
                        move |store| {
                            let result = mutate(store)?;
                            store.tab_order = crate::state::tab_metadata::normalize_tab_order(
                                &store.tab_order,
                                &store.custom_tabs,
                            );
                            Ok(result)
                        },
                    )
            })?;
        publish_legacy_editor_change(state, app, &transaction.change);
        state.refresh_viewer_keyboards();
        let snapshot = state.store.snapshot();
        emit_best_effort(app, "tabs:changed", &tabs_payload(&snapshot, kind));
        emit_best_effort(
            app,
            "keys:mode-changed",
            &json!({"mode":snapshot.selected_key_type}),
        );
        emit_best_effort(app, "keys:changed", &project_legacy_keys(&snapshot.keys));
        emit_best_effort(app, "positions:changed", &snapshot.key_positions);
        state.refresh_obs_snapshot();
        crate::services::native_viewers::notify_changed(app);
        Ok(transaction.value)
    })
    .await
}

#[tauri::command]
pub async fn tabs_list(app: AppHandle, viewer_kind: KeyViewerKind) -> CmdResult<Vec<KeyViewerTab>> {
    run_blocking(app, move |_, state| {
        Ok(state
            .store
            .snapshot()
            .custom_tabs
            .into_iter()
            .filter(|t| t.viewer_kind == viewer_kind)
            .collect())
    })
    .await
}

#[tauri::command]
pub async fn tabs_create(
    app: AppHandle,
    window: WebviewWindow,
    viewer_kind: KeyViewerKind,
    name: String,
) -> CmdResult<Value> {
    mutate_tabs(
        app,
        window,
        "tabs_create",
        Some(viewer_kind),
        move |store| {
            let name = match validate_name(store, viewer_kind, None, &name) {
                Ok(name) => name,
                Err(e) => return Ok(json!({"error":e})),
            };
            if store
                .custom_tabs
                .iter()
                .filter(|t| t.viewer_kind == viewer_kind)
                .count()
                >= 30
            {
                return Ok(json!({"error":"max-reached"}));
            }
            let id = format!(
                "{}-{}",
                if viewer_kind == KeyViewerKind::Hand {
                    "hand"
                } else {
                    "foot"
                },
                uuid::Uuid::new_v4().simple()
            );
            let tab = CustomTab {
                id: id.clone(),
                name,
                viewer_kind,
            };
            store.custom_tabs.push(tab.clone());
            store.keys.insert(id.clone(), vec![]);
            store.key_positions.insert(id.clone(), vec![]);
            select(store, viewer_kind, &id);
            Ok(json!({"result":tab}))
        },
    )
    .await
}

#[tauri::command]
pub async fn tabs_rename(
    app: AppHandle,
    window: WebviewWindow,
    viewer_kind: KeyViewerKind,
    id: String,
    name: String,
) -> CmdResult<Value> {
    mutate_tabs(
        app,
        window,
        "tabs_rename",
        Some(viewer_kind),
        move |store| {
            let name = match validate_name(store, viewer_kind, Some(&id), &name) {
                Ok(n) => n,
                Err(e) => return Ok(json!({"error":e})),
            };
            let Some(tab) = store
                .custom_tabs
                .iter_mut()
                .find(|t| t.id == id && t.viewer_kind == viewer_kind)
            else {
                return Ok(json!({"error":"not-found"}));
            };
            tab.name = name;
            Ok(json!({"result":tabs_payload(store,Some(viewer_kind))}))
        },
    )
    .await
}

#[tauri::command]
pub async fn tabs_reorder(
    app: AppHandle,
    window: WebviewWindow,
    viewer_kind: KeyViewerKind,
    ordered_ids: Vec<String>,
) -> CmdResult<Value> {
    mutate_tabs(
        app,
        window,
        "tabs_reorder",
        Some(viewer_kind),
        move |store| {
            let expected: HashSet<_> = store
                .custom_tabs
                .iter()
                .filter(|t| t.viewer_kind == viewer_kind)
                .map(|t| t.id.clone())
                .collect();
            if ordered_ids.len() != expected.len()
                || ordered_ids.iter().cloned().collect::<HashSet<_>>() != expected
            {
                return Ok(json!({"error":"invalid-order"}));
            }
            let tabs = store.custom_tabs.clone();
            let mut order = ordered_ids.iter().rev();
            for slot in store
                .custom_tabs
                .iter_mut()
                .filter(|t| t.viewer_kind == viewer_kind)
            {
                let id = order.next().expect("validated order");
                *slot = tabs
                    .iter()
                    .find(|t| &t.id == id)
                    .expect("validated id")
                    .clone();
            }
            store.tab_order = store.custom_tabs.iter().map(|t| t.id.clone()).collect();
            Ok(json!({"result":tabs_payload(store,Some(viewer_kind))}))
        },
    )
    .await
}

#[tauri::command]
pub async fn tabs_select(
    app: AppHandle,
    window: WebviewWindow,
    viewer_kind: KeyViewerKind,
    id: String,
) -> CmdResult<Value> {
    mutate_tabs(
        app,
        window,
        "tabs_select",
        Some(viewer_kind),
        move |store| {
            if !store
                .custom_tabs
                .iter()
                .any(|t| t.id == id && t.viewer_kind == viewer_kind)
            {
                return Ok(
                    json!({"success":false,"selected":store.selected_key_type,"error":"not-found"}),
                );
            }
            select(store, viewer_kind, &id);
            Ok(json!({"success":true,"selected":id}))
        },
    )
    .await
}

#[tauri::command]
pub async fn tabs_delete(app: AppHandle, window: WebviewWindow, id: String) -> CmdResult<Value> {
    mutate_tabs(app, window, "tabs_delete", None, move |store| {
        let Some(tab) = store.custom_tabs.iter().find(|t| t.id == id).cloned() else {
            return Ok(
                json!({"success":false,"selected":store.selected_key_type,"error":"not-found"}),
            );
        };
        let Some(fallback) = store
            .custom_tabs
            .iter()
            .find(|t| t.viewer_kind == tab.viewer_kind && t.id != id)
            .map(|t| t.id.clone())
        else {
            return Ok(
                json!({"success":false,"selected":store.selected_key_type,"error":"last-tab"}),
            );
        };
        store.custom_tabs.retain(|t| t.id != id);
        store.keys.remove(&id);
        store.key_positions.remove(&id);
        store.stat_positions.remove(&id);
        store.graph_positions.remove(&id);
        store.knob_positions.remove(&id);
        store.sprite_positions.remove(&id);
        store.layer_groups.remove(&id);
        store.key_counters.remove(&id);
        store.tab_css_overrides.remove(&id);
        store.tab_note_overrides.remove(&id);
        if store.selected_viewer_tabs.hand == id {
            store.selected_viewer_tabs.hand = fallback.clone();
        }
        if store.selected_viewer_tabs.foot == id {
            store.selected_viewer_tabs.foot = fallback.clone();
        }
        if store.selected_key_type == id {
            store.selected_key_type = fallback;
        }
        Ok(json!({"success":true,"selected":store.selected_key_type}))
    })
    .await
}

#[tauri::command]
pub async fn tabs_restore(
    app: AppHandle,
    window: WebviewWindow,
    tabs: Vec<KeyViewerTab>,
    selected_key_type: String,
    selected_viewer_tabs: SelectedViewerTabs,
) -> CmdResult<Value> {
    mutate_tabs(app, window, "tabs_restore", None, move |store| {
        store.custom_tabs = tabs;
        store.selected_key_type = selected_key_type;
        store.selected_viewer_tabs = selected_viewer_tabs;
        crate::state::element_migration::ensure_viewer_tabs(store);
        Ok(Value::Null)
    })
    .await
}

#[tauri::command]
pub async fn keys_update(
    app: AppHandle,
    window: WebviewWindow,
    mappings: KeyMappings,
) -> CmdResult<std::collections::HashMap<String, Vec<String>>> {
    run_history_mutation(app, window.label().into(), move |app, state, admission| {
        let transaction = state
            .store
            .commit_legacy_editor_transaction_with_admission(
                EditorCommitOrigin::LegacyAdapter("keys_update".into()),
                &[EditorField::Keys],
                admission,
                move |store| {
                    let mut mappings = mappings;
                    for (mode, slots) in &mut mappings {
                        if let Some(existing) = store.keys.get(mode) {
                            for slot in slots {
                                if let Some(original) = existing
                                    .iter()
                                    .find(|old| old.canonical() == slot.canonical())
                                {
                                    *slot = original.clone();
                                }
                            }
                        }
                    }
                    store.keys = mappings;
                    Ok(())
                },
            )?;
        publish_legacy_editor_change(state, app, &transaction.change);
        state.refresh_viewer_keyboards();
        let keys = transaction.change.document.keys;
        emit_best_effort(app, "keys:changed", &project_legacy_keys(&keys));
        state.refresh_obs_snapshot();
        Ok(project_legacy_keys(&keys))
    })
    .await
}

#[tauri::command]
pub async fn positions_update(
    app: AppHandle,
    window: WebviewWindow,
    positions: KeyPositions,
) -> CmdResult<KeyPositions> {
    run_history_mutation(app, window.label().into(), move |app, state, admission| {
        let transaction = state
            .store
            .commit_legacy_editor_transaction_with_admission(
                EditorCommitOrigin::LegacyAdapter("positions_update".into()),
                &[EditorField::KeyPositions],
                admission,
                move |store| {
                    store.key_positions = positions;
                    Ok(())
                },
            )?;
        publish_legacy_editor_change(state, app, &transaction.change);
        state.refresh_viewer_keyboards();
        let positions = transaction.change.document.key_positions;
        emit_best_effort(app, "positions:changed", &positions);
        state.refresh_obs_snapshot();
        Ok(positions)
    })
    .await
}

#[tauri::command]
pub async fn layer_groups_update(
    app: AppHandle,
    window: WebviewWindow,
    groups: LayerGroups,
) -> CmdResult<LayerGroups> {
    run_history_mutation(app, window.label().into(), move |app, state, admission| {
        let transaction = state
            .store
            .commit_legacy_editor_transaction_with_admission(
                EditorCommitOrigin::LegacyAdapter("layer_groups_update".into()),
                &[EditorField::LayerGroups],
                admission,
                move |store| {
                    store.layer_groups = groups;
                    Ok(())
                },
            )?;
        publish_legacy_editor_change(state, app, &transaction.change);
        let groups = transaction.change.document.layer_groups;
        emit_best_effort(app, "layerGroups:changed", &groups);
        state.refresh_obs_snapshot();
        Ok(groups)
    })
    .await
}
