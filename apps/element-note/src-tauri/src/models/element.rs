//! Element's viewer identity and ghost-key contract layered over the v2 editor.
use super::*;

#[derive(Debug, Clone, Copy, Default, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(rename_all = "lowercase")]
pub enum KeyViewerKind {
    #[default]
    Hand,
    Foot,
}

pub type KeyViewerTab = CustomTab;

pub fn project_legacy_keys(keys: &KeyMappings) -> HashMap<String, Vec<String>> {
    keys.iter()
        .map(|(mode, slots)| (mode.clone(), slots.iter().map(KeySlot::canonical).collect()))
        .collect()
}

pub(crate) fn serialize_legacy_keys<S: serde::Serializer>(
    keys: &KeyMappings,
    serializer: S,
) -> Result<S::Ok, S::Error> {
    project_legacy_keys(keys).serialize(serializer)
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct SelectedViewerTabs {
    pub hand: String,
    pub foot: String,
}

impl Default for SelectedViewerTabs {
    fn default() -> Self {
        Self {
            hand: "hand-default".into(),
            foot: "foot-default".into(),
        }
    }
}

#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct OverlayWindowLayout {
    pub bounds: Option<OverlayBounds>,
    pub last_content_top_offset: Option<f64>,
    #[serde(default)]
    pub bounds_are_logical: bool,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct OverlayWindowLayouts {
    pub hand: OverlayWindowLayout,
    pub foot: OverlayWindowLayout,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct GhostKey {
    #[serde(default)]
    pub enabled: bool,
    #[serde(default)]
    pub key: Option<String>,
    pub settings: Box<KeyPosition>,
}

impl GhostKey {
    pub fn active_key(&self, owner: &str) -> Option<&str> {
        self.key
            .as_deref()
            .filter(|key| self.enabled && !key.is_empty() && *key != owner)
    }
}

impl KeyPosition {
    pub fn strip_nested_ghost(&mut self) {
        if let Some(ghost) = self.ghost.as_mut() {
            ghost.settings.ghost = None;
        }
    }
}

pub fn ghost_key_labels<'a>(
    keys: &'a [KeySlot],
    positions: &'a [KeyPosition],
) -> impl Iterator<Item = &'a str> {
    keys.iter()
        .zip(positions)
        .filter_map(|(key, position)| position.ghost.as_ref()?.active_key(&key.canonical()))
}

pub fn counter_labels(
    keys: &KeyMappings,
    positions: &KeyPositions,
) -> HashMap<String, Vec<String>> {
    keys.iter()
        .map(|(mode, slots)| {
            let mut labels: Vec<String> = slots.iter().map(KeySlot::canonical).collect();
            if let Some(positions) = positions.get(mode) {
                labels.extend(ghost_key_labels(slots, positions).map(str::to_string));
            }
            (mode.clone(), labels)
        })
        .collect()
}

/// Runtime-only slots keep ghost input, counters, and sounds aligned without
/// adding ghost elements to the persisted v2 editor document.
pub(crate) fn viewer_runtime_mappings(keys: &KeyMappings, positions: &KeyPositions) -> KeyMappings {
    let mut runtime = keys.clone();
    for (mode, slots) in &mut runtime {
        if let Some(positions) = positions.get(mode) {
            let ghosts: Vec<_> = ghost_key_labels(slots, positions)
                .map(|label| KeySlot::Single(label.to_owned()))
                .collect();
            slots.extend(ghosts);
        }
    }
    runtime
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn bootstrap_defaults_preserve_element_v1_appearance() {
        let defaults = serde_json::to_value(DefaultsPayload::default()).unwrap();
        let counter = &defaults["counterSettings"];
        assert_eq!(counter["fill"]["idle"], "rgba(121, 121, 121, 0.9)");
        assert_eq!(counter["fill"]["active"], "#FFFFFF");
        assert_eq!(counter["fontSize"], 16);
        assert_eq!(counter["fontWeight"], 700);
        assert_eq!(counter["stroke"]["idle"], "transparent");
        let settings = &defaults["settings"];
        assert_eq!(settings["noteSettings"]["speed"], 180);
        assert_eq!(settings["noteSettings"]["trackHeight"], 150);
        assert_eq!(settings["uiTheme"], "dark");
        assert_eq!(settings["keyLimiterEnabled"], true);
        assert_eq!(settings["inGameViewersEnabled"], true);
        let position = KeyPosition::default();
        assert!(position.active_background_color.is_none());
        assert!(position.active_font_color.is_none());
        assert!(position.font_weight.is_none());
        assert!(position.font_bold.is_none());
    }

    #[test]
    fn ghost_runtime_slots_preserve_compounds_without_changing_editor_document() {
        let compound: KeySlot =
            serde_json::from_value(serde_json::json!({"keys":["A","S"],"match":"all"})).unwrap();
        let keys = KeyMappings::from([("hand".into(), vec![compound.clone()])]);
        let positions = KeyPositions::from([(
            "hand".into(),
            vec![KeyPosition {
                ghost: Some(GhostKey {
                    enabled: true,
                    key: Some("R".into()),
                    settings: Box::default(),
                }),
                ..KeyPosition::default()
            }],
        )]);
        let runtime = viewer_runtime_mappings(&keys, &positions);
        assert_eq!(runtime["hand"], vec![compound.clone(), KeySlot::from("R")]);
        assert_eq!(keys["hand"], vec![compound]);
        assert_eq!(project_legacy_keys(&keys)["hand"], vec!["A+S"]);
        assert_eq!(counter_labels(&keys, &positions)["hand"], vec!["A+S", "R"]);
    }
}
