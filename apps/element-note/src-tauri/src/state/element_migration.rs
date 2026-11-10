use crate::models::*;
const LEGACY_BUILTIN_TAB_IDS: [&str; 4] = ["4key", "5key", "6key", "8key"];

fn default_viewer_tab(kind: KeyViewerKind, language: &str) -> KeyViewerTab {
    let (id, name) = match (kind, language) {
        (KeyViewerKind::Hand, "ko") => ("hand-default", "손 1"),
        (KeyViewerKind::Foot, "ko") => ("foot-default", "발 1"),
        (KeyViewerKind::Hand, "zh-cn") => ("hand-default", "手部 1"),
        (KeyViewerKind::Foot, "zh-cn") => ("foot-default", "脚部 1"),
        (KeyViewerKind::Hand, "zh-Hant") => ("hand-default", "手部 1"),
        (KeyViewerKind::Foot, "zh-Hant") => ("foot-default", "腳部 1"),
        (KeyViewerKind::Hand, "ru") => ("hand-default", "Руки 1"),
        (KeyViewerKind::Foot, "ru") => ("foot-default", "Ноги 1"),
        (KeyViewerKind::Hand, _) => ("hand-default", "Hand 1"),
        (KeyViewerKind::Foot, _) => ("foot-default", "Foot 1"),
    };
    KeyViewerTab {
        id: id.to_string(),
        name: name.to_string(),
        viewer_kind: kind,
    }
}

pub(crate) fn default_viewer_tabs(language: &str) -> Vec<KeyViewerTab> {
    vec![
        default_viewer_tab(KeyViewerKind::Hand, language),
        default_viewer_tab(KeyViewerKind::Foot, language),
    ]
}

fn remove_legacy_builtin_modes(data: &mut AppStoreData) {
    for id in LEGACY_BUILTIN_TAB_IDS {
        data.keys.remove(id);
        data.key_positions.remove(id);
        data.stat_positions.remove(id);
        data.graph_positions.remove(id);
        data.knob_positions.remove(id);
        data.sprite_positions.remove(id);
        data.layer_groups.remove(id);
        data.key_counters.remove(id);
        data.tab_css_overrides.remove(id);
        data.tab_note_overrides.remove(id);
    }
    data.custom_tabs
        .retain(|tab| !LEGACY_BUILTIN_TAB_IDS.contains(&tab.id.as_str()));
}

pub(crate) fn ensure_viewer_tabs(data: &mut AppStoreData) {
    for mut fallback in default_viewer_tabs(&data.language) {
        if data
            .custom_tabs
            .iter()
            .any(|tab| tab.viewer_kind == fallback.viewer_kind)
        {
            continue;
        }
        let base_id = fallback.id.clone();
        let mut suffix = 2;
        while data.custom_tabs.iter().any(|tab| tab.id == fallback.id) {
            fallback.id = format!("{base_id}-{suffix}");
            suffix += 1;
        }
        data.custom_tabs.push(fallback);
    }

    for tab in &data.custom_tabs {
        data.keys.entry(tab.id.clone()).or_default();
        data.key_positions.entry(tab.id.clone()).or_default();
    }

    let selected_hand = data
        .custom_tabs
        .iter()
        .find(|tab| {
            tab.viewer_kind == KeyViewerKind::Hand && tab.id == data.selected_viewer_tabs.hand
        })
        .or_else(|| {
            data.custom_tabs.iter().find(|tab| {
                tab.viewer_kind == KeyViewerKind::Hand && tab.id == data.selected_key_type
            })
        })
        .or_else(|| {
            data.custom_tabs
                .iter()
                .find(|tab| tab.viewer_kind == KeyViewerKind::Hand)
        })
        .map(|tab| tab.id.clone())
        .expect("hand viewer tab must exist");
    let selected_foot = data
        .custom_tabs
        .iter()
        .find(|tab| {
            tab.viewer_kind == KeyViewerKind::Foot && tab.id == data.selected_viewer_tabs.foot
        })
        .or_else(|| {
            data.custom_tabs.iter().find(|tab| {
                tab.viewer_kind == KeyViewerKind::Foot && tab.id == data.selected_key_type
            })
        })
        .or_else(|| {
            data.custom_tabs
                .iter()
                .find(|tab| tab.viewer_kind == KeyViewerKind::Foot)
        })
        .map(|tab| tab.id.clone())
        .expect("foot viewer tab must exist");

    data.selected_viewer_tabs = SelectedViewerTabs {
        hand: selected_hand.clone(),
        foot: selected_foot,
    };
    if !data
        .custom_tabs
        .iter()
        .any(|tab| tab.id == data.selected_key_type)
    {
        data.selected_key_type = selected_hand;
    }
}

pub(crate) fn migrate_legacy_ghost_keys(positions_by_mode: &mut KeyPositions) -> bool {
    let mut changed = false;
    for positions in positions_by_mode.values_mut() {
        for position in positions.iter_mut() {
            let Some(class_name) = position.class_name.as_deref() else {
                continue;
            };
            let tokens: Vec<&str> = class_name.split_whitespace().collect();
            if !tokens.contains(&"ghost") {
                continue;
            }
            position.key_limiter_excluded = true;
            let remaining = tokens
                .into_iter()
                .filter(|token| *token != "ghost")
                .collect::<Vec<_>>()
                .join(" ");
            position.class_name = if remaining.is_empty() {
                None
            } else {
                Some(remaining)
            };
            changed = true;
        }
    }
    changed
}

fn same_rect(a: &KeyPosition, b: &KeyPosition) -> bool {
    const TOLERANCE: f64 = 0.5;
    (a.dx - b.dx).abs() < TOLERANCE
        && (a.dy - b.dy).abs() < TOLERANCE
        && (a.width - b.width).abs() < TOLERANCE
        && (a.height - b.height).abs() < TOLERANCE
}

/// Folds legacy limiter-excluded keys stacked exactly on another key into that
/// key's ghost slot, keeping their look. Excluded keys that stand alone keep
/// working as before.
pub(crate) fn fold_stacked_ghost_keys(
    keys_by_mode: &mut KeyMappings,
    positions_by_mode: &mut KeyPositions,
) -> bool {
    let mut changed = false;
    for (mode, keys) in keys_by_mode.iter_mut() {
        let Some(positions) = positions_by_mode.get_mut(mode) else {
            continue;
        };
        let len = keys.len().min(positions.len());
        let mut folded = vec![false; len];
        for ghost_index in 0..len {
            let legacy = &positions[ghost_index];
            if !legacy.key_limiter_excluded || legacy.hidden || legacy.ghost.is_some() {
                continue;
            }
            let owner_index = (0..len).find(|&index| {
                let owner = &positions[index];
                index != ghost_index
                    && !folded[index]
                    && !owner.key_limiter_excluded
                    && owner.ghost.is_none()
                    && keys[index] != keys[ghost_index]
                    && same_rect(owner, legacy)
            });
            let Some(owner_index) = owner_index else {
                continue;
            };
            let mut settings = positions[ghost_index].clone();
            settings.key_limiter_excluded = false;
            settings.layer_name = None;
            settings.group_id = None;
            positions[owner_index].ghost = Some(GhostKey {
                enabled: true,
                key: Some(keys[ghost_index].canonical()),
                settings: Box::new(settings),
            });
            folded[ghost_index] = true;
            changed = true;
        }
        if folded.iter().any(|value| *value) {
            let mut index = 0;
            keys.retain(|_| {
                let keep = index >= len || !folded[index];
                index += 1;
                keep
            });
            let mut index = 0;
            positions.retain(|_| {
                let keep = index >= len || !folded[index];
                index += 1;
                keep
            });
        }
    }
    changed
}

pub(crate) fn normalize_element_state(data: &mut AppStoreData) {
    remove_legacy_builtin_modes(data);
    ensure_viewer_tabs(data);
    migrate_legacy_ghost_keys(&mut data.key_positions);
    if !data.ghost_keys_folded {
        fold_stacked_ghost_keys(&mut data.keys, &mut data.key_positions);
        data.ghost_keys_folded = true;
    }
    for positions in data.key_positions.values_mut() {
        for position in positions {
            position.strip_nested_ghost();
        }
    }
    data.key_viewer_tabs_migrated = true;
    data.tab_order = data.custom_tabs.iter().map(|tab| tab.id.clone()).collect();
    if data.overlay_windows.hand.bounds.is_none() {
        data.overlay_windows.hand.bounds = data
            .overlay_bounds
            .as_ref()
            .map(|bounds| bounds.public_bounds());
        data.overlay_windows.hand.last_content_top_offset = data.overlay_last_content_top_offset;
        data.overlay_windows.hand.bounds_are_logical = data.overlay_bounds_are_logical;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn tab(id: &str, viewer_kind: KeyViewerKind) -> CustomTab {
        CustomTab {
            id: id.to_string(),
            name: id.to_string(),
            viewer_kind,
        }
    }

    #[test]
    fn missing_viewer_kind_uses_a_unique_id_without_reusing_the_other_layout() {
        for (occupied_id, existing_kind, missing_kind) in [
            ("hand-default", KeyViewerKind::Foot, KeyViewerKind::Hand),
            ("foot-default", KeyViewerKind::Hand, KeyViewerKind::Foot),
        ] {
            let position = KeyPosition {
                font_weight: Some(800),
                ..KeyPosition::default()
            };
            let mut data = AppStoreData {
                custom_tabs: vec![
                    tab(occupied_id, existing_kind),
                    tab(&format!("{occupied_id}-2"), existing_kind),
                ],
                keys: KeyMappings::from([(occupied_id.to_string(), vec![KeySlot::from("A")])]),
                key_positions: KeyPositions::from([(
                    occupied_id.to_string(),
                    vec![position.clone()],
                )]),
                ..AppStoreData::default()
            };
            ensure_viewer_tabs(&mut data);
            let added = data
                .custom_tabs
                .iter()
                .find(|tab| tab.viewer_kind == missing_kind)
                .unwrap();
            assert_eq!(added.id, format!("{occupied_id}-3"));
            assert!(data.keys[&added.id].is_empty());
            assert_eq!(data.keys[occupied_id], [KeySlot::from("A")]);
            assert_eq!(data.key_positions[occupied_id], [position]);
            assert_eq!(data.custom_tabs[0].viewer_kind, existing_kind);
        }
    }

    #[test]
    fn viewer_selection_stays_in_its_kind_and_uses_the_selected_foot_tab_as_fallback() {
        let mut data = AppStoreData {
            custom_tabs: vec![
                tab("h1", KeyViewerKind::Hand),
                tab("h2", KeyViewerKind::Hand),
                tab("f1", KeyViewerKind::Foot),
                tab("f2", KeyViewerKind::Foot),
            ],
            selected_key_type: "f2".to_string(),
            selected_viewer_tabs: SelectedViewerTabs {
                hand: "h2".to_string(),
                foot: "h1".to_string(),
            },
            ..AppStoreData::default()
        };
        ensure_viewer_tabs(&mut data);
        assert_eq!(data.selected_viewer_tabs.hand, "h2");
        assert_eq!(data.selected_viewer_tabs.foot, "f2");
        assert_eq!(data.selected_key_type, "f2");
    }

    #[test]
    fn legacy_stacked_ghost_migration_preserves_both_appearances_and_is_idempotent() {
        let owner = KeyPosition {
            dx: 12.0,
            dy: 34.0,
            font_weight: Some(700),
            ..KeyPosition::default()
        };
        let mut ghost = KeyPosition {
            class_name: Some("ghost custom-ghost".to_string()),
            font_weight: Some(800),
            background_color: Some("#102030".to_string()),
            active_image: Some("/ghost-active.png".to_string()),
            sound_path: Some("/ghost.wav".to_string()),
            layer_name: Some("old ghost layer".to_string()),
            group_id: Some("old group".to_string()),
            ..owner.clone()
        };
        ghost.counter.stroke.idle = "#123456".to_string();
        ghost.counter.font_weight = 900;
        let mut data = AppStoreData {
            custom_tabs: vec![
                tab("hand-custom", KeyViewerKind::Hand),
                tab("foot-custom", KeyViewerKind::Foot),
            ],
            keys: KeyMappings::from([(
                "hand-custom".to_string(),
                vec![KeySlot::from("A"), KeySlot::from("B")],
            )]),
            key_positions: KeyPositions::from([(
                "hand-custom".to_string(),
                vec![owner.clone(), ghost.clone()],
            )]),
            selected_key_type: "hand-custom".to_string(),
            ghost_keys_folded: false,
            ..AppStoreData::default()
        };
        normalize_element_state(&mut data);
        ghost.class_name = Some("custom-ghost".to_string());
        ghost.layer_name = None;
        ghost.group_id = None;
        let mut expected_owner = owner;
        expected_owner.ghost = Some(GhostKey {
            enabled: true,
            key: Some("B".to_string()),
            settings: Box::new(ghost),
        });
        assert_eq!(data.keys["hand-custom"], [KeySlot::from("A")]);
        assert_eq!(data.key_positions["hand-custom"], [expected_owner]);
        let once = serde_json::to_value(&data).unwrap();
        normalize_element_state(&mut data);
        assert_eq!(serde_json::to_value(&data).unwrap(), once);
    }

    #[test]
    fn removed_builtin_modes_do_not_leave_sprite_layouts_or_change_custom_appearance() {
        let position = KeyPosition {
            font_weight: Some(700),
            ..KeyPosition::default()
        };
        let mut data = AppStoreData {
            custom_tabs: vec![
                tab("4key", KeyViewerKind::Hand),
                tab("custom", KeyViewerKind::Foot),
            ],
            keys: KeyMappings::from([
                ("4key".to_string(), vec![KeySlot::from("A")]),
                ("custom".to_string(), vec![KeySlot::from("B")]),
            ]),
            key_positions: KeyPositions::from([
                ("4key".to_string(), vec![position.clone()]),
                ("custom".to_string(), vec![position.clone()]),
            ]),
            sprite_positions: SpritePositions::from([(
                "4key".to_string(),
                vec![ReactiveSpritePosition::default()],
            )]),
            selected_key_type: "custom".to_string(),
            ..AppStoreData::default()
        };
        normalize_element_state(&mut data);
        assert!(!data.keys.contains_key("4key"));
        assert!(!data.key_positions.contains_key("4key"));
        assert!(!data.sprite_positions.contains_key("4key"));
        assert!(!data.custom_tabs.iter().any(|tab| tab.id == "4key"));
        assert_eq!(data.key_positions["custom"], [position]);
        assert_eq!(data.selected_viewer_tabs.foot, "custom");
    }
}
