//! Explicit upstream editor layouts for inherited transaction tests.
//! These are custom tabs; production defaults remain Element's hand/foot viewers.
use crate::models::*;
use once_cell::sync::Lazy;

static KEYS: Lazy<KeyMappings> = Lazy::new(|| {
    serde_json::from_str(include_str!("../../tests/fixtures/upstream-keys.json")).unwrap()
});
static POSITIONS: Lazy<KeyPositions> = Lazy::new(|| {
    serde_json::from_str(include_str!("../../tests/fixtures/upstream-positions.json")).unwrap()
});
static STATS: Lazy<StatPositions> = Lazy::new(|| {
    serde_json::from_str(include_str!(
        "../../tests/fixtures/upstream-stat_positions.json"
    ))
    .unwrap()
});

pub(crate) fn default_keys() -> &'static KeyMappings {
    &KEYS
}
pub(crate) fn default_positions() -> &'static KeyPositions {
    &POSITIONS
}
pub(crate) fn default_stat_positions() -> &'static StatPositions {
    &STATS
}

pub(crate) fn fixture_store_data() -> AppStoreData {
    let custom_tabs = ["test-4key", "test-5key", "test-6key", "test-8key"]
        .into_iter()
        .map(|id| CustomTab {
            id: id.into(),
            name: id.into(),
            viewer_kind: if id == "test-5key" {
                KeyViewerKind::Foot
            } else {
                KeyViewerKind::Hand
            },
        })
        .collect();
    let mut data = crate::state::migration::normalize_state(AppStoreData {
        keys: KEYS.clone(),
        key_positions: POSITIONS.clone(),
        stat_positions: STATS.clone(),
        custom_tabs,
        key_viewer_data_recovered: true,
        selected_key_type: "test-4key".into(),
        selected_viewer_tabs: SelectedViewerTabs {
            hand: "test-4key".into(),
            foot: "test-5key".into(),
        },
        tab_order: ["test-4key", "test-5key", "test-6key", "test-8key"]
            .map(String::from)
            .to_vec(),
        ..AppStoreData::default()
    });
    crate::state::native_element_id::backfill_store_element_ids(&mut data);
    data
}
