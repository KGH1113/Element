//! Restore incomplete viewer migration data from Element's existing backup.
use super::migration::{load_store_from_path, normalize_state};
use crate::models::AppStoreData;
use anyhow::Result;
use std::path::Path;

pub(crate) fn restore_missing_viewer_data_from_backup(
    store_path: &Path,
    state: &mut AppStoreData,
) -> Result<bool> {
    let backup_path = store_path.with_file_name("store.pre-keyviewer-tabs.json");
    if !backup_path.exists() {
        return Ok(false);
    }

    let backup = load_store_from_path(&backup_path)?.data;
    let tab_ids: Vec<String> = state.custom_tabs.iter().map(|tab| tab.id.clone()).collect();
    let mut changed = false;

    for tab_id in tab_ids {
        if state.keys.get(&tab_id).is_none_or(Vec::is_empty) {
            if let Some(keys) = backup.keys.get(&tab_id).filter(|keys| !keys.is_empty()) {
                state.keys.insert(tab_id.clone(), keys.clone());
                changed = true;
            }
        }
        if state.key_positions.get(&tab_id).is_none_or(Vec::is_empty) {
            if let Some(positions) = backup
                .key_positions
                .get(&tab_id)
                .filter(|positions| !positions.is_empty())
            {
                state
                    .key_positions
                    .insert(tab_id.clone(), positions.clone());
                changed = true;
            }
        }
        if state.stat_positions.get(&tab_id).is_none_or(Vec::is_empty) {
            if let Some(positions) = backup
                .stat_positions
                .get(&tab_id)
                .filter(|positions| !positions.is_empty())
            {
                state
                    .stat_positions
                    .insert(tab_id.clone(), positions.clone());
                changed = true;
            }
        }
        if state.graph_positions.get(&tab_id).is_none_or(Vec::is_empty) {
            if let Some(positions) = backup
                .graph_positions
                .get(&tab_id)
                .filter(|positions| !positions.is_empty())
            {
                state
                    .graph_positions
                    .insert(tab_id.clone(), positions.clone());
                changed = true;
            }
        }
        if state.knob_positions.get(&tab_id).is_none_or(Vec::is_empty) {
            if let Some(positions) = backup
                .knob_positions
                .get(&tab_id)
                .filter(|positions| !positions.is_empty())
            {
                state
                    .knob_positions
                    .insert(tab_id.clone(), positions.clone());
                changed = true;
            }
        }
        if !state.layer_groups.contains_key(&tab_id) {
            if let Some(groups) = backup.layer_groups.get(&tab_id) {
                state.layer_groups.insert(tab_id.clone(), groups.clone());
                changed = true;
            }
        }
        if !state.tab_note_overrides.contains_key(&tab_id) {
            if let Some(settings) = backup.tab_note_overrides.get(&tab_id) {
                state
                    .tab_note_overrides
                    .insert(tab_id.clone(), settings.clone());
                changed = true;
            }
        }
        if let Some(css) = backup.tab_css_overrides.get(&tab_id) {
            if let std::collections::hash_map::Entry::Vacant(entry) =
                state.tab_css_overrides.entry(tab_id)
            {
                entry.insert(css.clone());
                changed = true;
            }
        }
    }

    *state = normalize_state(state.clone());
    crate::state::native_element_id::backfill_store_element_ids(state);
    Ok(changed)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{KeyPosition, KeySlot};

    #[test]
    fn existing_element_backup_restores_only_missing_viewer_layouts() {
        let directory = tempfile::tempdir().unwrap();
        let store_path = directory.path().join("store.json");
        let mut backup = AppStoreData::default();
        backup
            .keys
            .insert("hand-default".into(), vec![KeySlot::from("A")]);
        backup
            .keys
            .insert("foot-default".into(), vec![KeySlot::from("F")]);
        backup
            .key_positions
            .insert("hand-default".into(), vec![KeyPosition::default()]);
        backup.key_positions.insert(
            "foot-default".into(),
            vec![KeyPosition {
                dx: 420.0,
                ..KeyPosition::default()
            }],
        );
        std::fs::write(
            directory.path().join("store.pre-keyviewer-tabs.json"),
            serde_json::to_vec(&backup).unwrap(),
        )
        .unwrap();
        let mut current = AppStoreData::default();
        current
            .keys
            .insert("hand-default".into(), vec![KeySlot::from("B")]);
        current.key_positions.insert(
            "hand-default".into(),
            vec![KeyPosition {
                dx: 12.0,
                ..KeyPosition::default()
            }],
        );
        assert!(restore_missing_viewer_data_from_backup(&store_path, &mut current).unwrap());
        assert_eq!(current.keys["hand-default"], vec![KeySlot::from("B")]);
        assert_eq!(current.key_positions["hand-default"][0].dx, 12.0);
        assert_eq!(current.keys["foot-default"], vec![KeySlot::from("F")]);
        assert_eq!(current.key_positions["foot-default"][0].dx, 420.0);
        assert!(!current.key_positions["foot-default"][0].id.is_empty());
        assert!(!restore_missing_viewer_data_from_backup(&store_path, &mut current).unwrap());
    }
}
