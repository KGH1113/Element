use std::{
    fs,
    path::{Path, PathBuf},
};

use anyhow::{Context, Result};
use base64::{engine::general_purpose::STANDARD as BASE64_STANDARD, Engine as _};
use dirs_next::config_dir;
use serde::Deserialize;
use serde_json::{Number, Value};
use uuid::Uuid;

use crate::{
    defaults::{default_keys, default_positions},
    models::{
        AppStoreData, FontType, GhostKey, GraphPositions, KeyCounters, KeyMappings, KeyPosition,
        KeyPositions, KeyViewerKind, KeyViewerTab, NoteSettings, OverlayBounds, SelectedViewerTabs,
        StatPositions,
    },
};

const LEGACY_OVERLAY_WIDTH: f64 = 860.0;
const LEGACY_OVERLAY_HEIGHT: f64 = 320.0;
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
        data.layer_groups.remove(id);
        data.key_counters.remove(id);
        data.tab_css_overrides.remove(id);
        data.tab_note_overrides.remove(id);
    }
    data.tabs
        .retain(|tab| !LEGACY_BUILTIN_TAB_IDS.contains(&tab.id.as_str()));
}

fn ensure_viewer_tabs(data: &mut AppStoreData) {
    if !data
        .tabs
        .iter()
        .any(|tab| tab.viewer_kind == KeyViewerKind::Hand)
    {
        data.tabs
            .push(default_viewer_tab(KeyViewerKind::Hand, &data.language));
    }
    if !data
        .tabs
        .iter()
        .any(|tab| tab.viewer_kind == KeyViewerKind::Foot)
    {
        data.tabs
            .push(default_viewer_tab(KeyViewerKind::Foot, &data.language));
    }

    for tab in &data.tabs {
        data.keys.entry(tab.id.clone()).or_default();
        data.key_positions.entry(tab.id.clone()).or_default();
    }

    let selected_hand = data
        .tabs
        .iter()
        .find(|tab| {
            tab.viewer_kind == KeyViewerKind::Hand && tab.id == data.selected_viewer_tabs.hand
        })
        .or_else(|| {
            data.tabs.iter().find(|tab| {
                tab.viewer_kind == KeyViewerKind::Hand && tab.id == data.selected_key_type
            })
        })
        .or_else(|| {
            data.tabs
                .iter()
                .find(|tab| tab.viewer_kind == KeyViewerKind::Hand)
        })
        .map(|tab| tab.id.clone())
        .expect("hand viewer tab must exist");
    let selected_foot = data
        .tabs
        .iter()
        .find(|tab| {
            tab.viewer_kind == KeyViewerKind::Foot && tab.id == data.selected_viewer_tabs.foot
        })
        .or_else(|| {
            data.tabs
                .iter()
                .find(|tab| tab.viewer_kind == KeyViewerKind::Foot)
        })
        .map(|tab| tab.id.clone())
        .expect("foot viewer tab must exist");

    data.selected_viewer_tabs = SelectedViewerTabs {
        hand: selected_hand.clone(),
        foot: selected_foot,
    };
    if !data.tabs.iter().any(|tab| tab.id == data.selected_key_type) {
        data.selected_key_type = selected_hand;
    }
}

/// store 파일 로드 및 마이그레이션 적용
pub(crate) fn load_store_from_path(path: &Path) -> Result<(AppStoreData, bool)> {
    let content = fs::read_to_string(path)
        .with_context(|| format!("failed to read store file at {}", path.display()))?;
    let (data, repaired_numeric_fields) = match serde_json::from_str::<AppStoreData>(&content) {
        Ok(data) => (data, false),
        Err(primary_error) => {
            let mut value: Value = serde_json::from_str(&content).unwrap_or(Value::Null);
            coerce_integral_json_numbers(&mut value);
            match serde_json::from_value::<AppStoreData>(value) {
                Ok(data) => {
                    log::info!(
                        "[Store] Recovered legacy numeric fields after parse error: {primary_error}"
                    );
                    (data, true)
                }
                Err(repair_error) => {
                    log::warn!(
                        "[Store] Falling back to field recovery after parse errors: {primary_error}; {repair_error}"
                    );
                    (repair_legacy_state(&content), true)
                }
            }
        }
    };
    let needs_viewer_tabs_migration = !data.key_viewer_tabs_migrated;
    let needs_font_persist = data.font_settings.custom_fonts.iter().any(|font| {
        font.font_type == FontType::Local
            && font
                .css_content
                .as_ref()
                .map(|c| !c.trim().is_empty())
                .unwrap_or(false)
    });
    let needs_border_color_fix = has_convertible_note_border_color(&data);
    let needs_persist = repaired_numeric_fields
        || needs_viewer_tabs_migration
        || needs_font_persist
        || needs_border_color_fix;
    let state = normalize_state(data);
    if needs_persist {
        log::info!(
            "[Store] Persisting migrated store file at {}",
            path.display()
        );
    }
    Ok((state, needs_persist))
}

fn coerce_integral_json_numbers(value: &mut Value) {
    match value {
        Value::Array(values) => {
            for value in values {
                coerce_integral_json_numbers(value);
            }
        }
        Value::Object(values) => {
            for value in values.values_mut() {
                coerce_integral_json_numbers(value);
            }
        }
        Value::Number(number) if number.is_f64() => {
            let Some(value) = number.as_f64() else {
                return;
            };
            if value.is_finite() && value.fract() == 0.0 {
                if value >= 0.0 && value <= u64::MAX as f64 {
                    *number = Number::from(value as u64);
                } else if value >= i64::MIN as f64 && value <= i64::MAX as f64 {
                    *number = Number::from(value as i64);
                }
            }
        }
        _ => {}
    }
}

pub(crate) fn restore_missing_viewer_data_from_backup(
    store_path: &Path,
    state: &mut AppStoreData,
) -> Result<bool> {
    let backup_path = store_path.with_file_name("store.pre-keyviewer-tabs.json");
    if !backup_path.exists() {
        return Ok(false);
    }

    let (backup, _) = load_store_from_path(&backup_path)?;
    let tab_ids: Vec<String> = state.tabs.iter().map(|tab| tab.id.clone()).collect();
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
    Ok(changed)
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
                key: Some(keys[ghost_index].clone()),
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

/// 레거시 store 파일 경로 탐색
pub(crate) fn find_legacy_store_file() -> Option<PathBuf> {
    // 고정된 레거시 경로: %APPDATA%/dm-note/config.json
    let base = config_dir()?;
    let candidate = base.join("dm-note").join("config.json");
    if candidate.exists() {
        Some(candidate)
    } else {
        None
    }
}

/// 로컬 폰트 base64 cssContent → 앱 데이터 경로 기반 파일로 변환
pub(crate) fn migrate_local_fonts_to_app_data(
    app_data_dir: &Path,
    data: &mut AppStoreData,
) -> bool {
    let mut changed = false;

    let has_local_fonts = data
        .font_settings
        .custom_fonts
        .iter()
        .any(|font| font.font_type == FontType::Local);
    if !has_local_fonts {
        return false;
    }

    let fonts_dir = app_data_dir.join("fonts");
    if let Err(err) = fs::create_dir_all(&fonts_dir) {
        log::warn!(
            "[Fonts] Failed to create fonts directory at {}: {err}",
            fonts_dir.display()
        );
        return false;
    }

    for font in data.font_settings.custom_fonts.iter_mut() {
        if font.font_type != FontType::Local {
            continue;
        }

        let local_path = match font.local_path.as_ref() {
            Some(path) if !path.trim().is_empty() => path.trim(),
            _ => "",
        };

        if !local_path.is_empty() {
            let source = PathBuf::from(local_path);

            // 이미 앱 데이터 fonts 디렉터리 내부에 있으면 cssContent만 제거
            if source.starts_with(&fonts_dir) && source.exists() {
                if font.css_content.is_some() {
                    font.css_content = None;
                    changed = true;
                }
                continue;
            }

            if source.exists() {
                let ext = source
                    .extension()
                    .and_then(|e| e.to_str())
                    .unwrap_or("ttf")
                    .to_lowercase();
                let dest = fonts_dir.join(format!("{}.{}", Uuid::new_v4(), ext));
                match fs::copy(&source, &dest) {
                    Ok(_) => {
                        font.local_path = Some(dest.to_string_lossy().to_string());
                        font.css_content = None;
                        changed = true;
                        continue;
                    }
                    Err(err) => {
                        log::warn!(
                            "[Fonts] Failed to import local font from {}: {err}",
                            source.display()
                        );
                    }
                }
            }
        }

        // 폰트 파일 임포트 실패 시 비활성화하고 cssContent 제거 (store 비대화 방지)
        if font.enabled {
            font.enabled = false;
            changed = true;
        }
        if font.css_content.is_some() {
            font.css_content = None;
            changed = true;
        }
    }

    changed
}

/// 키 이미지 data URL/로컬 파일 → 앱 데이터 디렉터리로 마이그레이션
pub(crate) fn migrate_key_images_to_app_data(app_data_dir: &Path, data: &mut AppStoreData) -> bool {
    let mut changed = false;

    let has_any_images = data.key_positions.values().any(|positions| {
        positions.iter().any(|position| {
            option_has_non_empty_text(&position.active_image)
                || option_has_non_empty_text(&position.inactive_image)
        })
    }) || data.stat_positions.values().any(|positions| {
        positions.iter().any(|stat_position| {
            option_has_non_empty_text(&stat_position.position.active_image)
                || option_has_non_empty_text(&stat_position.position.inactive_image)
        })
    }) || data.graph_positions.values().any(|positions| {
        positions.iter().any(|graph_position| {
            option_has_non_empty_text(&graph_position.position.active_image)
                || option_has_non_empty_text(&graph_position.position.inactive_image)
        })
    });

    if !has_any_images {
        return false;
    }

    let images_dir = app_data_dir.join("images");
    if let Err(err) = fs::create_dir_all(&images_dir) {
        log::warn!(
            "[Images] Failed to create images directory at {}: {err}",
            images_dir.display()
        );
        return false;
    }

    for positions in data.key_positions.values_mut() {
        for position in positions.iter_mut() {
            changed |= migrate_image_reference_to_app_data(&images_dir, &mut position.active_image);
            changed |=
                migrate_image_reference_to_app_data(&images_dir, &mut position.inactive_image);
        }
    }

    for positions in data.stat_positions.values_mut() {
        for stat_position in positions.iter_mut() {
            changed |= migrate_image_reference_to_app_data(
                &images_dir,
                &mut stat_position.position.active_image,
            );
            changed |= migrate_image_reference_to_app_data(
                &images_dir,
                &mut stat_position.position.inactive_image,
            );
        }
    }

    for positions in data.graph_positions.values_mut() {
        for graph_position in positions.iter_mut() {
            changed |= migrate_image_reference_to_app_data(
                &images_dir,
                &mut graph_position.position.active_image,
            );
            changed |= migrate_image_reference_to_app_data(
                &images_dir,
                &mut graph_position.position.inactive_image,
            );
        }
    }

    changed
}

/// 개별 이미지 참조를 앱 데이터 디렉터리로 마이그레이션
fn migrate_image_reference_to_app_data(images_dir: &Path, image_ref: &mut Option<String>) -> bool {
    let Some(raw_value) = image_ref.clone() else {
        return false;
    };

    let trimmed = raw_value.trim();
    if trimmed.is_empty() {
        return false;
    }

    if let Some((bytes, extension)) = decode_image_data_url(trimmed) {
        let dest = images_dir.join(format!("{}.{}", Uuid::new_v4(), extension));
        match fs::write(&dest, bytes) {
            Ok(_) => {
                *image_ref = Some(dest.to_string_lossy().to_string());
                return true;
            }
            Err(err) => {
                log::warn!(
                    "[Images] Failed to migrate data URL image into {}: {err}",
                    dest.display()
                );
                return false;
            }
        }
    }

    let source = PathBuf::from(trimmed);
    if !source.is_absolute() || !source.exists() {
        return false;
    }
    if source.starts_with(images_dir) {
        return false;
    }

    let extension = normalize_image_extension(source.extension().and_then(|ext| ext.to_str()));
    let dest = images_dir.join(format!("{}.{}", Uuid::new_v4(), extension));
    match fs::copy(&source, &dest) {
        Ok(_) => {
            *image_ref = Some(dest.to_string_lossy().to_string());
            true
        }
        Err(err) => {
            log::warn!(
                "[Images] Failed to copy local image from {}: {err}",
                source.display()
            );
            false
        }
    }
}

/// `rgba(r, g, b, a)` 문자열을 `#RRGGBB`로 변환. rgba 형식이 아니거나 파싱 실패 시 None
fn rgba_to_hex(color: &str) -> Option<String> {
    let inner = color.trim().strip_prefix("rgba(")?.strip_suffix(')')?;
    let mut parts = inner.split(',');
    let r: u8 = parts.next()?.trim().parse().ok()?;
    let g: u8 = parts.next()?.trim().parse().ok()?;
    let b: u8 = parts.next()?.trim().parse().ok()?;
    Some(format!("#{r:02X}{g:02X}{b:02X}"))
}

/// noteBorderColor가 변환 가능한 rgba면 #RRGGBB로 교체 (그 외 입력은 그대로)
fn migrate_note_border_color(color: &mut Option<String>) {
    if let Some(hex) = color.as_deref().and_then(rgba_to_hex) {
        *color = Some(hex);
    }
}

/// store에 #RRGGBB로 변환 가능한 rgba noteBorderColor가 남아 있는지 (디스크 영속 판단용)
fn has_convertible_note_border_color(data: &AppStoreData) -> bool {
    let convertible = |color: &Option<String>| color.as_deref().and_then(rgba_to_hex).is_some();
    data.key_positions
        .values()
        .flatten()
        .any(|pos| convertible(&pos.note_border_color))
        || data
            .stat_positions
            .values()
            .flatten()
            .any(|stat| convertible(&stat.position.note_border_color))
        || data
            .graph_positions
            .values()
            .flatten()
            .any(|graph| convertible(&graph.position.note_border_color))
}

/// store 데이터 정규화 및 레거시 마이그레이션 적용
pub(crate) fn normalize_state(mut data: AppStoreData) -> AppStoreData {
    if let Some(path) = data.custom_css.path.clone() {
        if !data
            .custom_css_history
            .iter()
            .any(|entry| entry.path == path)
        {
            data.custom_css_history
                .push(crate::models::CustomCssHistoryEntry {
                    path,
                    loaded_at: 0,
                    last_used_at: 0,
                });
        }
    }
    data.custom_css_history.retain(|entry| {
        Path::new(&entry.path)
            .extension()
            .and_then(|value| value.to_str())
            == Some("css")
    });
    data.custom_css_history
        .sort_by_key(|entry| std::cmp::Reverse(entry.last_used_at));
    let mut seen_css_paths = std::collections::HashSet::new();
    data.custom_css_history
        .retain(|entry| seen_css_paths.insert(entry.path.clone()));
    data.custom_css_history.truncate(10);
    if data.overlay_windows.hand.bounds.is_none() {
        data.overlay_windows.hand.bounds = data.overlay_bounds.take();
        data.overlay_windows.hand.last_content_top_offset =
            data.overlay_last_content_top_offset.take();
        data.overlay_windows.hand.bounds_are_logical = data.overlay_bounds_are_logical;
    }
    data.overlay_bounds = None;
    data.overlay_last_content_top_offset = None;
    data.overlay_bounds_are_logical = false;

    remove_legacy_builtin_modes(&mut data);
    if !data.key_viewer_tabs_migrated {
        for tab in &mut data.tabs {
            tab.viewer_kind = KeyViewerKind::Hand;
        }
        data.selected_viewer_tabs = SelectedViewerTabs::default();
        // The legacy editor selection becomes the initial hand selection only
        // during migration. Later saves must preserve each viewer independently.
        data.selected_viewer_tabs.hand = data.selected_key_type.clone();
        data.key_viewer_tabs_migrated = true;
    }
    ensure_viewer_tabs(&mut data);

    if data.keys.is_empty() {
        data.keys = default_keys().clone();
    }

    if data.key_positions.is_empty() {
        data.key_positions = default_positions().clone();
    }

    // 레거시 마이그레이션: 전역 noteSettings.borderRadius → 키별 noteBorderRadius
    if let Some(legacy_border_radius) = data.note_settings.border_radius.take() {
        for positions in data.key_positions.values_mut() {
            for pos in positions.iter_mut() {
                pos.note_border_radius = Some(legacy_border_radius as f64);
            }
        }
    }

    // 마이그레이션: noteBorderColor에 잘못 저장된 rgba(...) → #RRGGBB 복구 (이슈 #73)
    // 배치 편집 경로가 정규화 없이 rgba 문자열을 저장해 오버레이에서 초록색으로 깨짐
    for positions in data.key_positions.values_mut() {
        for pos in positions.iter_mut() {
            migrate_note_border_color(&mut pos.note_border_color);
            pos.strip_nested_ghost();
            if let Some(ghost) = pos.ghost.as_mut() {
                migrate_note_border_color(&mut ghost.settings.note_border_color);
            }
        }
    }
    for positions in data.stat_positions.values_mut() {
        for stat in positions.iter_mut() {
            migrate_note_border_color(&mut stat.position.note_border_color);
        }
    }
    for positions in data.graph_positions.values_mut() {
        for graph in positions.iter_mut() {
            migrate_note_border_color(&mut graph.position.note_border_color);
        }
    }

    // 레거시 마이그레이션: fadePosition enum → 방향별 픽셀 fade 값
    data.note_settings.migrate_fade_position();
    for tab in data.tab_note_overrides.values_mut() {
        tab.migrate_fade_position();
    }

    let labels = crate::models::counter_labels(&data.keys, &data.key_positions);
    merge_default_counters(&mut data.key_counters, &labels);

    data.counter_animation_presets =
        crate::models::normalize_user_counter_animation_presets(data.counter_animation_presets);

    // 마이그레이션: 기존 기본 카운터 설정의 반전된 active 색상 보정
    // (레거시 패턴에 해당하는 경우만 적용, 사용자 커스텀은 유지)
    for positions in data.key_positions.values_mut() {
        for pos in positions.iter_mut() {
            pos.counter.migrate_legacy_defaults();
        }
    }
    for positions in data.stat_positions.values_mut() {
        for pos in positions.iter_mut() {
            pos.position.counter.migrate_legacy_defaults();
        }
    }
    for positions in data.graph_positions.values_mut() {
        for pos in positions.iter_mut() {
            pos.position.counter.migrate_legacy_defaults();
        }
    }

    ensure_viewer_tabs(&mut data);

    let _ = data.custom_js.normalize();

    data
}

/// 키 카운터 기본값 병합
fn merge_default_counters(target: &mut KeyCounters, keys: &KeyMappings) {
    for (mode, key_list) in keys.iter() {
        let entry = target.entry(mode.clone()).or_default();
        entry.retain(|key, _| key_list.contains(key));
        for key in key_list.iter() {
            entry.entry(key.clone()).or_insert(0);
        }
    }

    let available_modes: std::collections::HashSet<_> = keys.keys().cloned().collect();
    target.retain(|mode, _| available_modes.contains(mode));
}

/// 레거시/비정상 store 파일 수동 복구
fn repair_legacy_state(raw: &str) -> AppStoreData {
    let mut value: Value = serde_json::from_str(raw).unwrap_or(Value::Null);
    coerce_integral_json_numbers(&mut value);
    let mut data = AppStoreData::default();
    if let Value::Object(obj) = value {
        if let Some(v) = obj.get("hardwareAcceleration").and_then(Value::as_bool) {
            data.hardware_acceleration = v;
        }
        if let Some(v) = obj.get("alwaysOnTop").and_then(Value::as_bool) {
            data.always_on_top = v;
        }
        if let Some(v) = obj.get("overlayLocked").and_then(Value::as_bool) {
            data.overlay_locked = v;
        }
        if let Some(v) = obj.get("noteEffect").and_then(Value::as_bool) {
            data.note_effect = v;
        }
        if let Some(v) = obj
            .get("noteSettings")
            .and_then(|v| serde_json::from_value::<NoteSettings>(v.clone()).ok())
        {
            data.note_settings = v;
        }
        if let Some(v) = obj.get("selectedKeyType").and_then(Value::as_str) {
            data.selected_key_type = v.to_string();
        }
        if let Some(v) = obj
            .get("customTabs")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.tabs = v;
        }
        if let Some(v) = obj
            .get("counterAnimationPresets")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.counter_animation_presets = v;
        }
        if let Some(v) = obj.get("angleMode").and_then(Value::as_str) {
            data.angle_mode = v.to_string();
        }
        if let Some(v) = obj.get("language").and_then(Value::as_str) {
            data.language = v.to_string();
        }
        if let Some(v) = obj.get("laboratoryEnabled").and_then(Value::as_bool) {
            data.laboratory_enabled = v;
        }
        if let Some(v) = obj.get("developerModeEnabled").and_then(Value::as_bool) {
            data.developer_mode_enabled = v;
        }
        if let Some(v) = obj.get("trayEnabled").and_then(Value::as_bool) {
            data.tray_enabled = v;
        }
        if let Some(v) = obj.get("mainWindowHidden").and_then(Value::as_bool) {
            data.main_window_hidden = v;
        }
        if let Some(v) = obj
            .get("keys")
            .and_then(|v| serde_json::from_value::<KeyMappings>(v.clone()).ok())
        {
            data.keys = v;
        }
        if let Some(v) = obj
            .get("keyPositions")
            .and_then(|v| serde_json::from_value::<KeyPositions>(v.clone()).ok())
        {
            data.key_positions = v;
        }
        if let Some(v) = obj
            .get("statPositions")
            .and_then(|v| serde_json::from_value::<StatPositions>(v.clone()).ok())
        {
            data.stat_positions = v;
        }
        if let Some(v) = obj
            .get("graphPositions")
            .and_then(|v| serde_json::from_value::<GraphPositions>(v.clone()).ok())
        {
            data.graph_positions = v;
        }
        if let Some(v) = obj
            .get("keyCounters")
            .and_then(|v| serde_json::from_value::<KeyCounters>(v.clone()).ok())
        {
            data.key_counters = v;
        }
        if let Some(v) = obj.get("backgroundColor").and_then(Value::as_str) {
            data.background_color = v.to_string();
        }
        if let Some(v) = obj.get("useCustomCSS").and_then(Value::as_bool) {
            data.use_custom_css = v;
        } else if let Some(v) = obj.get("useCustomCss").and_then(Value::as_bool) {
            data.use_custom_css = v;
        }
        if let Some(v) = obj
            .get("customCSS")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.custom_css = v;
        } else if let Some(v) = obj
            .get("customCss")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.custom_css = v;
        }
        if let Some(v) = obj
            .get("customCssHistory")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.custom_css_history = v;
        }
        if let Some(v) = obj.get("useCustomJS").and_then(Value::as_bool) {
            data.use_custom_js = v;
        } else if let Some(v) = obj.get("useCustomJs").and_then(Value::as_bool) {
            data.use_custom_js = v;
        }
        if let Some(v) = obj
            .get("customJS")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.custom_js = v;
        } else if let Some(v) = obj
            .get("customJs")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.custom_js = v;
        }
        if let Some(v) = obj
            .get("overlayResizeAnchor")
            .and_then(|v| serde_json::from_value(v.clone()).ok())
        {
            data.overlay_resize_anchor = v;
        }
        if let Some(v) = obj
            .get("overlayWindowBounds")
            .and_then(|v| serde_json::from_value::<LegacyOverlayBounds>(v.clone()).ok())
        {
            data.overlay_bounds = Some(OverlayBounds {
                x: v.x,
                y: v.y,
                width: v.width,
                height: v.height,
            });
        }
        if data.overlay_bounds.is_none() {
            if let Some(v) = obj
                .get("overlayWindowPosition")
                .and_then(|v| serde_json::from_value::<LegacyOverlayPosition>(v.clone()).ok())
            {
                data.overlay_bounds = Some(OverlayBounds {
                    x: v.x,
                    y: v.y,
                    width: LEGACY_OVERLAY_WIDTH,
                    height: LEGACY_OVERLAY_HEIGHT,
                });
            }
        }
        if let Some(v) = obj
            .get("overlayLastContentTopOffset")
            .and_then(Value::as_f64)
        {
            data.overlay_last_content_top_offset = Some(v);
        }
        if let Some(v) = obj.get("keyCounterEnabled").and_then(Value::as_bool) {
            data.key_counter_enabled = v;
        }
    }
    migrate_legacy_ghost_keys(&mut data.key_positions);
    let _ = data.custom_js.normalize();
    normalize_state(data)
}

fn option_has_non_empty_text(value: &Option<String>) -> bool {
    value
        .as_ref()
        .map(|text| !text.trim().is_empty())
        .unwrap_or(false)
}

fn decode_image_data_url(value: &str) -> Option<(Vec<u8>, String)> {
    let (header, payload) = value.split_once(',')?;
    let header_lower = header.to_ascii_lowercase();
    if !header_lower.starts_with("data:image/") || !header_lower.contains(";base64") {
        return None;
    }

    let mime = header
        .split(';')
        .next()
        .and_then(|part| part.strip_prefix("data:"))
        .unwrap_or("image/png");
    let extension = extension_from_image_mime(mime);
    let bytes = BASE64_STANDARD.decode(payload.as_bytes()).ok()?;
    Some((bytes, extension))
}

fn extension_from_image_mime(mime: &str) -> String {
    match mime.trim().to_ascii_lowercase().as_str() {
        "image/jpeg" | "image/jpg" => "jpg".to_string(),
        "image/png" => "png".to_string(),
        "image/webp" => "webp".to_string(),
        "image/gif" => "gif".to_string(),
        "image/bmp" => "bmp".to_string(),
        "image/svg+xml" => "svg".to_string(),
        "image/x-icon" | "image/vnd.microsoft.icon" => "ico".to_string(),
        "image/avif" => "avif".to_string(),
        _ => "png".to_string(),
    }
}

fn normalize_image_extension(extension: Option<&str>) -> String {
    match extension
        .unwrap_or_default()
        .trim()
        .to_ascii_lowercase()
        .as_str()
    {
        "jpg" | "jpeg" => "jpg".to_string(),
        "png" => "png".to_string(),
        "webp" => "webp".to_string(),
        "gif" => "gif".to_string(),
        "bmp" => "bmp".to_string(),
        "svg" => "svg".to_string(),
        "ico" => "ico".to_string(),
        "avif" => "avif".to_string(),
        _ => "png".to_string(),
    }
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct LegacyOverlayBounds {
    x: f64,
    y: f64,
    width: f64,
    height: f64,
}

#[derive(Deserialize)]
struct LegacyOverlayPosition {
    x: f64,
    y: f64,
}

#[cfg(test)]
mod tests {
    use super::{
        fold_stacked_ghost_keys, migrate_legacy_ghost_keys, normalize_state, rgba_to_hex,
        LEGACY_BUILTIN_TAB_IDS,
    };
    use crate::models::{
        AppStoreData, KeyMappings, KeyPosition, KeyPositions, KeyViewerKind, KeyViewerTab,
    };

    fn key_at(dx: f64, excluded: bool) -> KeyPosition {
        let mut position: KeyPosition = serde_json::from_value(serde_json::json!({
            "dx": dx, "dy": 90.0, "width": 65.0, "height": 65.0,
            "count": 0, "noteColor": "#BA712B", "noteOpacity": 80,
        }))
        .unwrap();
        position.key_limiter_excluded = excluded;
        position
    }

    #[test]
    fn stacked_legacy_ghost_keys_fold_into_their_owner() {
        let mut keys = KeyMappings::from([(
            "numpad".to_string(),
            vec!["E".to_string(), "R".to_string(), "Q".to_string()],
        )]);
        let mut legacy_ghost = key_at(493.0, true);
        legacy_ghost.note_opacity = 0;
        let mut positions = KeyPositions::from([(
            "numpad".to_string(),
            vec![key_at(493.0, false), legacy_ghost, key_at(600.0, true)],
        )]);

        assert!(fold_stacked_ghost_keys(&mut keys, &mut positions));

        assert_eq!(keys["numpad"], vec!["E".to_string(), "Q".to_string()]);
        let owner = &positions["numpad"][0];
        let ghost = owner.ghost.as_ref().expect("ghost slot");
        assert!(ghost.enabled);
        assert_eq!(ghost.key.as_deref(), Some("R"));
        assert_eq!(ghost.settings.note_opacity, 0);
        assert!(!ghost.settings.key_limiter_excluded);
        // A lone excluded key has no owner and keeps its legacy behavior.
        assert!(positions["numpad"][1].key_limiter_excluded);
        assert!(!fold_stacked_ghost_keys(&mut keys, &mut positions));
    }

    #[test]
    fn legacy_editor_selection_becomes_the_hand_selection() {
        let data = AppStoreData {
          key_viewer_tabs_migrated: false,
          selected_key_type: "numpad".into(),
          tabs: ["hand-default", "numpad"]
            .into_iter()
            .map(|id| KeyViewerTab {
                id: id.into(),
                name: id.into(),
                viewer_kind: KeyViewerKind::Hand,
            })
            .collect(),
          ..AppStoreData::default()
        };

        let normalized = normalize_state(data);

        assert_eq!(normalized.selected_viewer_tabs.hand, "numpad");
        assert_eq!(normalized.selected_key_type, "numpad");
        assert!(normalized.key_viewer_tabs_migrated);
    }

    #[test]
    fn invalid_hand_selection_falls_back_without_changing_the_foot_selection() {
        let mut data = AppStoreData {
          key_viewer_tabs_migrated: true,
          tabs: super::default_viewer_tabs("en"),
          selected_key_type: "foot-default".into(),
          ..AppStoreData::default()
        };
        data.selected_viewer_tabs.hand = "missing".into();

        let normalized = normalize_state(data);

        assert_eq!(normalized.selected_viewer_tabs.hand, "hand-default");
        assert_eq!(normalized.selected_viewer_tabs.foot, "foot-default");
        assert_eq!(normalized.selected_key_type, "foot-default");
    }

    #[test]
    fn rgba_to_hex_converts_and_drops_alpha() {
        assert_eq!(
            rgba_to_hex("rgba(255, 0, 167, 1)").as_deref(),
            Some("#FF00A7")
        );
        assert_eq!(
            rgba_to_hex("rgba(18, 52, 86, 0)").as_deref(),
            Some("#123456")
        );
    }

    #[test]
    fn rgba_to_hex_ignores_non_rgba() {
        assert_eq!(rgba_to_hex("#FF00A7"), None);
        assert_eq!(rgba_to_hex("garbage"), None);
        assert_eq!(rgba_to_hex("rgba(300, 0, 0, 1)"), None); // u8 범위 초과
    }

    #[test]
    fn legacy_ghost_class_is_migrated_without_touching_other_classes() {
        let mut position: KeyPosition = serde_json::from_str(
            r##"{
                "dx": 0, "dy": 0, "width": 60, "height": 60,
                "count": 0, "noteColor": "#FFFFFF", "noteOpacity": 80,
                "className": "wide ghost selected ghost"
            }"##,
        )
        .unwrap();
        position.key_limiter_excluded = false;
        let mut positions = KeyPositions::new();
        positions.insert("4key".to_string(), vec![position]);

        assert!(migrate_legacy_ghost_keys(&mut positions));
        let migrated = &positions["4key"][0];
        assert!(migrated.key_limiter_excluded);
        assert_eq!(migrated.class_name.as_deref(), Some("wide selected"));
    }

    #[test]
    fn ghostly_class_is_not_migrated() {
        let position: KeyPosition = serde_json::from_str(
            r##"{
                "dx": 0, "dy": 0, "width": 60, "height": 60,
                "count": 0, "noteColor": "#FFFFFF", "noteOpacity": 80,
                "className": "ghostly selected"
            }"##,
        )
        .unwrap();
        let mut positions = KeyPositions::new();
        positions.insert("4key".to_string(), vec![position]);

        assert!(!migrate_legacy_ghost_keys(&mut positions));
        let untouched = &positions["4key"][0];
        assert!(!untouched.key_limiter_excluded);
        assert_eq!(untouched.class_name.as_deref(), Some("ghostly selected"));
    }

    #[test]
    fn legacy_builtin_key_viewers_are_removed_and_group_defaults_are_created() {
        let mut data = AppStoreData::default();
        data.tabs.clear();
        data.keys.clear();
        data.key_positions.clear();
        data.selected_key_type = "4key".to_string();
        for id in LEGACY_BUILTIN_TAB_IDS {
            data.keys.insert(id.to_string(), vec!["A".to_string()]);
            data.key_positions.insert(id.to_string(), Vec::new());
        }

        let normalized = normalize_state(data);

        for id in LEGACY_BUILTIN_TAB_IDS {
            assert!(!normalized.keys.contains_key(id));
            assert!(!normalized.key_positions.contains_key(id));
        }
        assert_eq!(normalized.selected_key_type, "hand-default");
        assert_eq!(normalized.tabs.len(), 2);
        assert!(normalized
            .tabs
            .iter()
            .any(|tab| tab.viewer_kind == KeyViewerKind::Hand));
        assert!(normalized
            .tabs
            .iter()
            .any(|tab| tab.viewer_kind == KeyViewerKind::Foot));
        assert!(normalized.keys.contains_key("hand-default"));
        assert!(normalized.keys.contains_key("foot-default"));
    }
}
