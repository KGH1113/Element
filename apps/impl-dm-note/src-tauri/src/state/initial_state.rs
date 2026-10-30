//! What a brand-new install starts with: ready-made hand and foot key viewers
//! drawn in the bundled MapleStory font.
//!
//! Only the first launch uses this. Reset Data and per-tab reset keep their
//! own defaults in `defaults.rs`.

use std::{fs, io, path::Path};

use serde::Deserialize;

use crate::models::{
    AppStoreData, CustomFont, FontType, GraphPositions, KeyMappings, KeyPositions, KeyViewerTab,
    SelectedViewerTabs, StatPositions,
};

static INITIAL_KEY_VIEWERS: &str = include_str!("../../initial_key_viewers.json");

/// Tabs and elements in `initial_key_viewers.json`. Field names match store.json.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct InitialKeyViewers {
    tabs: Vec<KeyViewerTab>,
    selected_viewer_tabs: SelectedViewerTabs,
    keys: KeyMappings,
    key_positions: KeyPositions,
    #[serde(default)]
    stat_positions: StatPositions,
    #[serde(default)]
    graph_positions: GraphPositions,
}

/// A font file shipped inside the app and copied into `<app data>/fonts`.
struct BundledFont {
    id: &'static str,
    /// CSS family name; the initial keys refer to it in `fontFamily`.
    name: &'static str,
    file_name: &'static str,
    bytes: &'static [u8],
}

const INITIAL_FONT: BundledFont = BundledFont {
    id: "font_builtin_maplestory_otf_bold",
    name: "MAPLESTORY_OTF_BOLD",
    file_name: "builtin-maplestory-otf-bold.otf",
    bytes: include_bytes!("../../assets/fonts/MAPLESTORY_OTF_BOLD.OTF"),
};

/// Store data for the first launch, before `normalize_state`.
///
/// If the font can't be copied, the key viewers still load and use the
/// default font.
pub(crate) fn initial_state(app_data_dir: &Path) -> AppStoreData {
    let viewers: InitialKeyViewers = serde_json::from_str(INITIAL_KEY_VIEWERS)
        .expect("bundled initial_key_viewers.json must match the store models");

    let mut data = AppStoreData {
        selected_key_type: viewers.selected_viewer_tabs.hand.clone(),
        tabs: viewers.tabs,
        selected_viewer_tabs: viewers.selected_viewer_tabs,
        keys: viewers.keys,
        key_positions: viewers.key_positions,
        stat_positions: viewers.stat_positions,
        graph_positions: viewers.graph_positions,
        ..AppStoreData::default()
    };

    match install_font(app_data_dir, &INITIAL_FONT) {
        Ok(font) => data.font_settings.custom_fonts.push(font),
        Err(err) => log::warn!(
            "[Store] Couldn't copy {} into the fonts folder, so the key viewers use the default font: {err}",
            INITIAL_FONT.file_name
        ),
    }

    data
}

fn install_font(app_data_dir: &Path, font: &BundledFont) -> io::Result<CustomFont> {
    let fonts_dir = app_data_dir.join("fonts");
    fs::create_dir_all(&fonts_dir)?;

    let path = fonts_dir.join(font.file_name);
    let is_complete = fs::metadata(&path)
        .map(|meta| meta.len() == font.bytes.len() as u64)
        .unwrap_or(false);
    if !is_complete {
        fs::write(&path, font.bytes)?;
    }

    Ok(CustomFont {
        id: font.id.to_string(),
        font_type: FontType::Local,
        name: font.name.to_string(),
        display_name: font.name.to_string(),
        enabled: true,
        local_path: Some(path.to_string_lossy().to_string()),
        css_content: None,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        models::{GraphStatType, KeyPosition, KeyViewerKind, StatType},
        state::migration::{
            load_store_from_path, migrate_local_fonts_to_app_data, normalize_state,
        },
    };

    fn temp_app_data_dir() -> std::path::PathBuf {
        std::env::temp_dir().join(format!(
            "impldmnote-initial-state-test-{}",
            uuid::Uuid::new_v4()
        ))
    }

    fn with_initial_state(check: impl FnOnce(&AppStoreData, &Path)) {
        let dir = temp_app_data_dir();
        let data = normalize_state(initial_state(&dir));
        check(&data, &dir);
        let _ = fs::remove_dir_all(&dir);
    }

    fn keys(data: &AppStoreData, tab: &str) -> Vec<String> {
        data.keys.get(tab).cloned().unwrap_or_default()
    }

    fn strings(values: &[&str]) -> Vec<String> {
        values.iter().map(|value| value.to_string()).collect()
    }

    const TOP_ROW: [&str; 8] = [
        "TAB",
        "1",
        "2",
        "E",
        "P",
        "EQUALS",
        "BACKSPACE",
        "BACKSLASH",
    ];

    #[test]
    fn opens_on_16k_hand_and_0k_foot() {
        with_initial_state(|data, _| {
            let tabs: Vec<(&str, &str, KeyViewerKind)> = data
                .tabs
                .iter()
                .map(|tab| (tab.id.as_str(), tab.name.as_str(), tab.viewer_kind))
                .collect();
            assert_eq!(
                tabs,
                vec![
                    ("hand-16k", "16K", KeyViewerKind::Hand),
                    ("hand-12k", "12K", KeyViewerKind::Hand),
                    ("hand-10k", "10K", KeyViewerKind::Hand),
                    ("hand-8k", "8K", KeyViewerKind::Hand),
                    ("hand-4k", "4K", KeyViewerKind::Hand),
                    ("foot-0k", "0K", KeyViewerKind::Foot),
                    ("foot-2k", "2K", KeyViewerKind::Foot),
                    ("foot-4k", "4K", KeyViewerKind::Foot),
                    ("foot-6k", "6K", KeyViewerKind::Foot),
                    ("foot-8k", "8K", KeyViewerKind::Foot),
                ]
            );
            assert_eq!(data.selected_viewer_tabs.hand, "hand-16k");
            assert_eq!(data.selected_viewer_tabs.foot, "foot-0k");
            assert_eq!(data.selected_key_type, "hand-16k");
        });
    }

    #[test]
    fn hand_tabs_bind_the_real_16k_keys() {
        with_initial_state(|data, _| {
            let mut sixteen = strings(&TOP_ROW);
            sixteen.extend(strings(&[
                "CAPS LOCK",
                "LEFT CTRL",
                "C",
                "SPACE",
                "21",
                "DOT",
                "25",
                "RETURN",
            ]));
            assert_eq!(keys(data, "hand-16k"), sixteen);

            let mut twelve = strings(&TOP_ROW);
            twelve.extend(strings(&["C", "SPACE", "21", "DOT"]));
            assert_eq!(keys(data, "hand-12k"), twelve);

            let mut ten = strings(&TOP_ROW);
            ten.extend(strings(&["SPACE", "21"]));
            assert_eq!(keys(data, "hand-10k"), ten);

            assert_eq!(keys(data, "hand-8k"), strings(&TOP_ROW));
            assert_eq!(keys(data, "hand-4k"), strings(&["2", "E", "P", "EQUALS"]));
        });
    }

    #[test]
    fn foot_tabs_bind_function_keys() {
        with_initial_state(|data, _| {
            assert!(keys(data, "foot-0k").is_empty());
            assert_eq!(keys(data, "foot-2k"), strings(&["F8", "F3"]));
            assert_eq!(keys(data, "foot-4k"), strings(&["F8", "F7", "F4", "F3"]));
            assert_eq!(
                keys(data, "foot-6k"),
                strings(&["F8", "F7", "F6", "F4", "F3", "F2"])
            );
            assert_eq!(
                keys(data, "foot-8k"),
                strings(&["F8", "F7", "F6", "F5", "F4", "F3", "F2", "F1"])
            );
        });
    }

    #[test]
    fn keys_show_their_own_names() {
        with_initial_state(|data, _| {
            for tab in &data.tabs {
                let positions = &data.key_positions[&tab.id];
                assert_eq!(positions.len(), keys(data, &tab.id).len(), "{}", tab.id);
                for position in positions {
                    assert_eq!(position.display_text, None, "{}", tab.id);
                    assert_eq!(position.count, 0, "{}", tab.id);
                }
            }
        });
    }

    #[test]
    fn hand_tabs_keep_ghost_keys_on_2_e_p_equals() {
        with_initial_state(|data, _| {
            let expected = vec![
                ("2".to_string(), "W".to_string()),
                ("E".to_string(), "R".to_string()),
                ("P".to_string(), "O".to_string()),
                ("EQUALS".to_string(), "SQUARE BRACKET OPEN".to_string()),
            ];
            for tab in data
                .tabs
                .iter()
                .filter(|tab| tab.viewer_kind == KeyViewerKind::Hand)
            {
                let ghosts: Vec<(String, String)> = keys(data, &tab.id)
                    .into_iter()
                    .zip(&data.key_positions[&tab.id])
                    .filter_map(|(key, position): (String, &KeyPosition)| {
                        let ghost = position.ghost.as_ref()?;
                        assert!(ghost.enabled, "{}", tab.id);
                        assert_eq!(ghost.settings.display_text, None, "{}", tab.id);
                        Some((key, ghost.key.clone()?))
                    })
                    .collect();
                assert_eq!(ghosts, expected, "{}", tab.id);
            }
        });
    }

    #[test]
    fn hand_tabs_show_kps_counter_and_graph() {
        with_initial_state(|data, _| {
            for tab in &data.tabs {
                let stats = data
                    .stat_positions
                    .get(&tab.id)
                    .cloned()
                    .unwrap_or_default();
                let graphs = data
                    .graph_positions
                    .get(&tab.id)
                    .cloned()
                    .unwrap_or_default();
                match tab.viewer_kind {
                    KeyViewerKind::Hand => {
                        assert_eq!(stats.len(), 1, "{}", tab.id);
                        assert_eq!(stats[0].stat_type, StatType::Kps);
                        assert_eq!(graphs.len(), 1, "{}", tab.id);
                        assert_eq!(graphs[0].stat_type, GraphStatType::Kps);
                    }
                    KeyViewerKind::Foot => {
                        assert!(stats.is_empty(), "{}", tab.id);
                        assert!(graphs.is_empty(), "{}", tab.id);
                    }
                }
            }
        });
    }

    #[test]
    fn copies_the_font_the_keys_use() {
        with_initial_state(|data, dir| {
            let fonts = &data.font_settings.custom_fonts;
            assert_eq!(fonts.len(), 1);
            let font = &fonts[0];
            assert_eq!(font.font_type, FontType::Local);
            assert!(font.enabled);

            let path = font.local_path.as_deref().expect("font path");
            assert!(Path::new(path).starts_with(dir.join("fonts")));
            assert_eq!(fs::read(path).unwrap(), INITIAL_FONT.bytes);

            for positions in data.key_positions.values() {
                for position in positions {
                    assert_eq!(position.font_family.as_deref(), Some(font.name.as_str()));
                }
            }
        });
    }

    #[test]
    fn second_launch_loads_the_same_key_viewers() {
        with_initial_state(|first, dir| {
            let path = dir.join("store.json");
            fs::write(&path, serde_json::to_string(first).unwrap()).unwrap();

            let (mut second, _) = load_store_from_path(&path).unwrap();
            assert!(!migrate_local_fonts_to_app_data(dir, &mut second));
            assert_eq!(second.tabs, first.tabs);
            assert_eq!(second.selected_viewer_tabs, first.selected_viewer_tabs);
            assert_eq!(second.selected_key_type, first.selected_key_type);
            assert_eq!(second.keys, first.keys);
            assert_eq!(second.key_positions, first.key_positions);
            assert_eq!(second.stat_positions, first.stat_positions);
            assert_eq!(second.graph_positions, first.graph_positions);
            assert_eq!(second.font_settings, first.font_settings);
        });
    }

    #[test]
    fn font_copy_survives_a_second_first_run() {
        let dir = temp_app_data_dir();
        let first = initial_state(&dir);
        let second = initial_state(&dir);
        assert_eq!(first.font_settings, second.font_settings);
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn missing_fonts_folder_still_gives_key_viewers() {
        let dir = temp_app_data_dir();
        fs::create_dir_all(&dir).unwrap();
        // A file where the fonts folder should be makes the copy fail.
        fs::write(dir.join("fonts"), b"").unwrap();

        let data = normalize_state(initial_state(&dir));
        assert!(data.font_settings.custom_fonts.is_empty());
        assert_eq!(data.selected_viewer_tabs.hand, "hand-16k");
        assert_eq!(keys(&data, "hand-16k").len(), 16);

        let _ = fs::remove_dir_all(&dir);
    }
}
