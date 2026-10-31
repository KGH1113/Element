use std::{
    fs,
    path::{Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

use rfd::FileDialog;
use serde::Serialize;
use tauri::{AppHandle, Emitter, State};

use crate::{
    errors::{CmdResult, CommandError},
    models::{CustomCss, CustomCssHistoryEntry, TabCss, TabCssOverrides},
    state::AppState,
};

const MAX_CSS_BYTES: u64 = 1024 * 1024;
const MAX_CSS_HISTORY: usize = 10;

fn now_epoch_secs() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

fn read_valid_css(path: &Path) -> Result<(String, String), String> {
    if path.extension().and_then(|value| value.to_str()) != Some("css") {
        return Err("invalid-extension".to_string());
    }
    let metadata = fs::metadata(path).map_err(|_| "not-found".to_string())?;
    if !metadata.is_file() {
        return Err("not-a-file".to_string());
    }
    if metadata.len() > MAX_CSS_BYTES {
        return Err("file-too-large".to_string());
    }
    let canonical = path.canonicalize().map_err(|_| "not-found".to_string())?;
    let content = fs::read_to_string(&canonical).map_err(|_| "invalid-utf8".to_string())?;
    Ok((canonical.to_string_lossy().into_owned(), content))
}

fn touch_css_history(entries: &mut Vec<CustomCssHistoryEntry>, path: String) {
    let now = now_epoch_secs();
    let loaded_at = entries
        .iter()
        .find(|entry| entry.path == path)
        .map_or(now, |entry| entry.loaded_at);
    entries.retain(|entry| entry.path != path);
    entries.insert(
        0,
        CustomCssHistoryEntry {
            path,
            loaded_at,
            last_used_at: now,
        },
    );
    entries.truncate(MAX_CSS_HISTORY);
}

fn has_tab(state: &AppState, tab_id: &str) -> bool {
    state
        .store
        .snapshot()
        .tabs
        .iter()
        .any(|tab| tab.id == tab_id)
}

fn is_authorized_css_path(state: &AppState, path: &str) -> bool {
    let snapshot = state.store.snapshot();
    snapshot.custom_css.path.as_deref() == Some(path)
        || snapshot
            .custom_css_history
            .iter()
            .any(|entry| entry.path == path)
        || snapshot
            .tab_css_overrides
            .values()
            .any(|css| css.path.as_deref() == Some(path))
}

/// OBS 브릿지에 CSS 설정 변경을 settings_diff로 전달 (전체 스냅샷 브로드캐스트 방지)
fn notify_obs_css(state: &AppState) {
    let snap = state.store.snapshot();
    let diff = serde_json::json!({
        "useCustomCSS": snap.use_custom_css,
        "customCSS": snap.custom_css,
    });
    state.notify_obs_settings_diff(diff);
}

#[derive(Serialize)]
pub struct CssToggleResponse {
    pub enabled: bool,
}

#[derive(Serialize)]
pub struct CssSetContentResponse {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

#[derive(Serialize)]
pub struct CssLoadResponse {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub content: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CssHistoryMutationResponse {
    pub success: bool,
    pub error: Option<String>,
    pub css: Option<CustomCss>,
}

// ========== 탭별 CSS 응답 타입 ==========

#[derive(Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct TabCssResponse {
    pub tab_id: String,
    pub css: Option<TabCss>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TabCssLoadResponse {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
    pub tab_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub css: Option<TabCss>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TabCssClearResponse {
    pub success: bool,
    pub tab_id: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TabCssToggleResponse {
    pub success: bool,
    pub tab_id: String,
    pub enabled: bool,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TabCssSetResponse {
    pub success: bool,
    pub tab_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub css: Option<TabCss>,
}

#[tauri::command]
pub fn css_get(state: State<'_, AppState>) -> CmdResult<CustomCss> {
    Ok(state.store.snapshot().custom_css)
}

#[tauri::command]
pub fn css_get_use(state: State<'_, AppState>) -> CmdResult<bool> {
    Ok(state.store.snapshot().use_custom_css)
}

#[tauri::command]
pub fn css_toggle(
    state: State<'_, AppState>,
    app: AppHandle,
    enabled: bool,
) -> CmdResult<CssToggleResponse> {
    let _operation = state.lock_css_operation();
    state.store.update(|store| {
        store.use_custom_css = enabled;
    })?;

    app.emit("css:use", &CssToggleResponse { enabled })?;

    if enabled {
        let css = state.store.snapshot().custom_css;
        app.emit("css:content", &css)?;

        // CSS 핫리로딩: 활성화 시 파일 워칭 시작
        if let Some(path) = &css.path {
            if let Err(err) = state.watch_global_css(path) {
                log::warn!("[css_toggle] Failed to start watching: {}", err);
            }
        }
    } else {
        // CSS 핫리로딩: 비활성화 시 워칭 중지
        state.unwatch_global_css();
    }

    notify_obs_css(&state);
    Ok(CssToggleResponse { enabled })
}

#[tauri::command]
pub fn css_reset(state: State<'_, AppState>, app: AppHandle) -> CmdResult<()> {
    let _operation = state.lock_css_operation();
    // CSS 핫리로딩: 전역 CSS 워칭 중지
    state.unwatch_global_css();

    state.store.update(|store| {
        store.use_custom_css = false;
        store.custom_css = CustomCss::default();
    })?;

    app.emit("css:use", &CssToggleResponse { enabled: false })?;
    app.emit("css:content", &CustomCss::default())?;

    notify_obs_css(&state);
    Ok(())
}

#[tauri::command]
pub fn css_set_content(
    state: State<'_, AppState>,
    app: AppHandle,
    content: String,
) -> CmdResult<CssSetContentResponse> {
    let _operation = state.lock_css_operation();
    let mut current = state.store.snapshot().custom_css;
    current.content = content.clone();

    state.store.update(|store| {
        store.custom_css = current.clone();
    })?;

    app.emit("css:content", &current)?;

    notify_obs_css(&state);
    Ok(CssSetContentResponse {
        success: true,
        error: None,
    })
}

#[tauri::command]
pub fn css_load(state: State<'_, AppState>, app: AppHandle) -> CmdResult<CssLoadResponse> {
    let picked = FileDialog::new().add_filter("CSS", &["css"]).pick_file();

    let Some(path) = picked else {
        return Ok(CssLoadResponse {
            success: false,
            error: None,
            content: None,
            path: None,
        });
    };

    let _operation = state.lock_css_operation();
    let requested_path = path.to_string_lossy().to_string();
    match read_valid_css(&path) {
        Ok((path_string, content)) => {
            // 이전 파일 워칭 중지
            state.unwatch_global_css();

            let css = CustomCss {
                path: Some(path_string.clone()),
                content: content.clone(),
            };
            state.store.update(|store| {
                store.custom_css = css.clone();
                touch_css_history(&mut store.custom_css_history, path_string.clone());
            })?;

            app.emit("css:content", &css)?;

            // CSS 핫리로딩: 새 파일 워칭 시작 (use_custom_css가 활성화된 경우에만)
            if state.store.snapshot().use_custom_css {
                if let Err(err) = state.watch_global_css(&path_string) {
                    log::warn!("[css_load] Failed to start watching: {}", err);
                }
            }

            notify_obs_css(&state);
            Ok(CssLoadResponse {
                success: true,
                error: None,
                content: Some(content),
                path: Some(path_string),
            })
        }
        Err(err) => Ok(CssLoadResponse {
            success: false,
            error: Some(err.to_string()),
            content: None,
            path: Some(requested_path),
        }),
    }
}

// ========== 탭별 CSS 커맨드 ==========

/// 모든 탭의 CSS 오버라이드 조회
#[tauri::command]
pub fn css_tab_get_all(state: State<'_, AppState>) -> CmdResult<TabCssOverrides> {
    Ok(state.store.snapshot().tab_css_overrides)
}

/// 특정 탭의 CSS 조회
#[tauri::command]
pub fn css_tab_get(state: State<'_, AppState>, tab_id: String) -> CmdResult<TabCssResponse> {
    let overrides = state.store.snapshot().tab_css_overrides;
    let css = overrides.get(&tab_id).cloned();
    Ok(TabCssResponse { tab_id, css })
}

/// 특정 탭에 CSS 파일 로드
#[tauri::command]
pub fn css_tab_load(
    state: State<'_, AppState>,
    app: AppHandle,
    tab_id: String,
) -> CmdResult<TabCssLoadResponse> {
    if !has_tab(&state, &tab_id) {
        return Ok(TabCssLoadResponse {
            success: false,
            error: Some("tab-not-found".into()),
            tab_id,
            css: None,
        });
    }
    let picked = FileDialog::new().add_filter("CSS", &["css"]).pick_file();

    let Some(path) = picked else {
        return Ok(TabCssLoadResponse {
            success: false,
            error: None,
            tab_id,
            css: None,
        });
    };

    let _operation = state.lock_css_operation();
    match read_valid_css(&path) {
        Ok((path_string, content)) => {
            // 이전 탭 CSS 워칭 중지
            state.unwatch_tab_css(&tab_id);

            let tab_css = TabCss {
                path: Some(path_string.clone()),
                content: content.clone(),
                enabled: true,
            };

            state.store.update(|store| {
                store
                    .tab_css_overrides
                    .insert(tab_id.clone(), tab_css.clone());
                touch_css_history(&mut store.custom_css_history, path_string.clone());
            })?;

            let response = TabCssResponse {
                tab_id: tab_id.clone(),
                css: Some(tab_css.clone()),
            };
            app.emit("tabCss:changed", &response)?;

            // CSS 핫리로딩: 새 탭 CSS 파일 워칭 시작
            if let Err(err) = state.watch_tab_css(&path_string, &tab_id) {
                log::warn!(
                    "[css_tab_load] Failed to start watching tab {}: {}",
                    tab_id,
                    err
                );
            }

            Ok(TabCssLoadResponse {
                success: true,
                error: None,
                tab_id,
                css: Some(tab_css),
            })
        }
        Err(err) => Ok(TabCssLoadResponse {
            success: false,
            error: Some(err),
            tab_id,
            css: None,
        }),
    }
}

#[tauri::command]
pub fn css_history_get(state: State<'_, AppState>) -> CmdResult<Vec<CustomCssHistoryEntry>> {
    Ok(state.store.snapshot().custom_css_history)
}

#[tauri::command]
pub fn css_history_activate(
    state: State<'_, AppState>,
    app: AppHandle,
    path: String,
) -> CmdResult<CssHistoryMutationResponse> {
    let _operation = state.lock_css_operation();
    if !state
        .store
        .snapshot()
        .custom_css_history
        .iter()
        .any(|entry| entry.path == path)
    {
        return Ok(CssHistoryMutationResponse {
            success: false,
            error: Some("not-authorized".into()),
            css: None,
        });
    }
    let (canonical, content) = match read_valid_css(Path::new(&path)) {
        Ok(value) => value,
        Err(error) => {
            return Ok(CssHistoryMutationResponse {
                success: false,
                error: Some(error),
                css: None,
            })
        }
    };
    let css = CustomCss {
        path: Some(canonical.clone()),
        content,
    };
    state.unwatch_global_css();
    state.store.update(|store| {
        store.custom_css = css.clone();
        store.custom_css_history.retain(|entry| entry.path != path);
        touch_css_history(&mut store.custom_css_history, canonical.clone());
    })?;
    app.emit("css:content", &css)?;
    if state.store.snapshot().use_custom_css {
        let _ = state.watch_global_css(&canonical);
    }
    notify_obs_css(&state);
    Ok(CssHistoryMutationResponse {
        success: true,
        error: None,
        css: Some(css),
    })
}

#[tauri::command]
pub fn css_history_remove(
    state: State<'_, AppState>,
    path: String,
) -> CmdResult<CssHistoryMutationResponse> {
    let _operation = state.lock_css_operation();
    state.store.update(|store| {
        store.custom_css_history.retain(|entry| entry.path != path);
    })?;
    Ok(CssHistoryMutationResponse {
        success: true,
        error: None,
        css: None,
    })
}

#[tauri::command]
pub fn css_tab_apply_history(
    state: State<'_, AppState>,
    app: AppHandle,
    tab_id: String,
    path: String,
) -> CmdResult<TabCssLoadResponse> {
    let _operation = state.lock_css_operation();
    if !has_tab(&state, &tab_id) {
        return Ok(TabCssLoadResponse {
            success: false,
            error: Some("tab-not-found".into()),
            tab_id,
            css: None,
        });
    }
    if !state
        .store
        .snapshot()
        .custom_css_history
        .iter()
        .any(|entry| entry.path == path)
    {
        return Ok(TabCssLoadResponse {
            success: false,
            error: Some("not-authorized".into()),
            tab_id,
            css: None,
        });
    }
    let (canonical, content) = match read_valid_css(Path::new(&path)) {
        Ok(value) => value,
        Err(error) => {
            return Ok(TabCssLoadResponse {
                success: false,
                error: Some(error),
                tab_id,
                css: None,
            })
        }
    };
    let css = TabCss {
        path: Some(canonical.clone()),
        content,
        enabled: true,
    };
    state.unwatch_tab_css(&tab_id);
    state.store.update(|store| {
        store.tab_css_overrides.insert(tab_id.clone(), css.clone());
        store.custom_css_history.retain(|entry| entry.path != path);
        touch_css_history(&mut store.custom_css_history, canonical.clone());
    })?;
    app.emit(
        "tabCss:changed",
        &TabCssResponse {
            tab_id: tab_id.clone(),
            css: Some(css.clone()),
        },
    )?;
    let _ = state.watch_tab_css(&canonical, &tab_id);
    Ok(TabCssLoadResponse {
        success: true,
        error: None,
        tab_id,
        css: Some(css),
    })
}

#[tauri::command]
pub fn css_tab_export(
    state: State<'_, AppState>,
    tab_id: String,
) -> CmdResult<CssSetContentResponse> {
    let Some(css) = state
        .store
        .snapshot()
        .tab_css_overrides
        .get(&tab_id)
        .cloned()
    else {
        return Ok(CssSetContentResponse {
            success: false,
            error: Some("css-not-found".into()),
        });
    };
    let Some(path) = FileDialog::new()
        .add_filter("CSS", &["css"])
        .set_file_name(format!("{tab_id}.css"))
        .save_file()
    else {
        return Ok(CssSetContentResponse {
            success: false,
            error: None,
        });
    };
    let tmp: PathBuf = path.with_extension("css.tmp");
    let result = fs::write(&tmp, css.content.as_bytes()).and_then(|_| fs::rename(&tmp, &path));
    match result {
        Ok(()) => Ok(CssSetContentResponse {
            success: true,
            error: None,
        }),
        Err(error) => {
            let _ = fs::remove_file(tmp);
            Ok(CssSetContentResponse {
                success: false,
                error: Some(error.to_string()),
            })
        }
    }
}

/// 특정 탭의 CSS 제거 (전역 CSS로 폴백)
#[tauri::command]
pub fn css_tab_clear(
    state: State<'_, AppState>,
    app: AppHandle,
    tab_id: String,
) -> CmdResult<TabCssClearResponse> {
    let _operation = state.lock_css_operation();
    if !has_tab(&state, &tab_id) {
        return Err(CommandError::msg("tab-not-found"));
    }
    // CSS 핫리로딩: 탭 CSS 워칭 중지
    state.unwatch_tab_css(&tab_id);

    state.store.update(|store| {
        store.tab_css_overrides.remove(&tab_id);
    })?;

    let response = TabCssResponse {
        tab_id: tab_id.clone(),
        css: None,
    };
    app.emit("tabCss:changed", &response)?;

    Ok(TabCssClearResponse {
        success: true,
        tab_id,
    })
}

/// 특정 탭의 CSS 직접 설정 (복원용)
#[tauri::command]
pub fn css_tab_set(
    state: State<'_, AppState>,
    app: AppHandle,
    tab_id: String,
    css: Option<TabCss>,
) -> CmdResult<TabCssSetResponse> {
    let _operation = state.lock_css_operation();
    if !has_tab(&state, &tab_id) {
        return Err(CommandError::msg("tab-not-found"));
    }
    if let Some(path) = css.as_ref().and_then(|value| value.path.as_deref()) {
        if !is_authorized_css_path(&state, path) {
            return Err(CommandError::msg("not-authorized"));
        }
    }
    // 이전 탭 CSS 워칭 중지
    state.unwatch_tab_css(&tab_id);

    if let Some(ref tab_css) = css {
        state.store.update(|store| {
            store
                .tab_css_overrides
                .insert(tab_id.clone(), tab_css.clone());
        })?;

        // CSS 핫리로딩: 파일이 있고 enabled인 경우 워칭 시작
        if tab_css.enabled {
            if let Some(path) = &tab_css.path {
                if let Err(err) = state.watch_tab_css(path, &tab_id) {
                    log::warn!(
                        "[css_tab_set] Failed to start watching tab {}: {}",
                        tab_id,
                        err
                    );
                }
            }
        }
    } else {
        state.store.update(|store| {
            store.tab_css_overrides.remove(&tab_id);
        })?;
    }

    let response = TabCssResponse {
        tab_id: tab_id.clone(),
        css: css.clone(),
    };
    app.emit("tabCss:changed", &response)?;

    Ok(TabCssSetResponse {
        success: true,
        tab_id,
        css,
    })
}

/// 특정 탭의 CSS 사용 여부 토글
#[tauri::command]
pub fn css_tab_toggle(
    state: State<'_, AppState>,
    app: AppHandle,
    tab_id: String,
    enabled: bool,
) -> CmdResult<TabCssToggleResponse> {
    let _operation = state.lock_css_operation();
    if !has_tab(&state, &tab_id) {
        return Err(CommandError::msg("tab-not-found"));
    }
    let mut updated_css: Option<TabCss> = None;

    state.store.update(|store| {
        if let Some(tab_css) = store.tab_css_overrides.get_mut(&tab_id) {
            tab_css.enabled = enabled;
            updated_css = Some(tab_css.clone());
        } else {
            // 탭 CSS가 없으면 기본 설정으로 생성
            let new_css = TabCss {
                path: None,
                content: String::new(),
                enabled,
            };
            store
                .tab_css_overrides
                .insert(tab_id.clone(), new_css.clone());
            updated_css = Some(new_css);
        }
    })?;

    // CSS 핫리로딩: 활성화/비활성화에 따른 워칭 관리
    if let Some(ref css) = updated_css {
        if enabled {
            if let Some(path) = &css.path {
                if let Err(err) = state.watch_tab_css(path, &tab_id) {
                    log::warn!(
                        "[css_tab_toggle] Failed to start watching tab {}: {}",
                        tab_id,
                        err
                    );
                }
            }
        } else {
            state.unwatch_tab_css(&tab_id);
        }
    }

    let response = TabCssResponse {
        tab_id: tab_id.clone(),
        css: updated_css,
    };
    app.emit("tabCss:changed", &response)?;

    Ok(TabCssToggleResponse {
        success: true,
        tab_id,
        enabled,
    })
}

#[cfg(test)]
mod tests {
    use super::{read_valid_css, touch_css_history, CustomCssHistoryEntry, MAX_CSS_BYTES};
    use std::fs;
    use tempfile::tempdir;

    #[test]
    fn css_file_validation_accepts_only_small_utf8_css_files() {
        let dir = tempdir().expect("tempdir");
        let css = dir.path().join("viewer.css");
        fs::write(&css, ".key { border: none; }").expect("write css");
        let (path, content) = read_valid_css(&css).expect("valid css");
        assert!(path.ends_with("viewer.css"));
        assert!(content.contains("border"));

        let text = dir.path().join("viewer.txt");
        fs::write(&text, "body {}").expect("write text");
        assert_eq!(read_valid_css(&text).unwrap_err(), "invalid-extension");

        let oversized = dir.path().join("large.css");
        fs::write(&oversized, vec![b'a'; MAX_CSS_BYTES as usize + 1]).expect("write large");
        assert_eq!(read_valid_css(&oversized).unwrap_err(), "file-too-large");
    }

    #[test]
    fn css_history_is_unique_recent_first_and_bounded() {
        let mut entries: Vec<CustomCssHistoryEntry> = Vec::new();
        for index in 0..12 {
            touch_css_history(&mut entries, format!("/{index}.css"));
        }
        touch_css_history(&mut entries, "/5.css".to_string());
        assert_eq!(entries.len(), 10);
        assert_eq!(entries[0].path, "/5.css");
        assert_eq!(
            entries
                .iter()
                .filter(|entry| entry.path == "/5.css")
                .count(),
            1
        );
    }
}
