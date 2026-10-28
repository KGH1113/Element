use tauri::{AppHandle, Manager, State};

use crate::cursor::{get_macos_cursor_settings, rgb_to_hex};
use crate::errors::{CmdResult, CommandError};
use crate::launch::LaunchContext;
use crate::state::AppState;

#[tauri::command]
pub fn window_minimize(app: AppHandle) -> CmdResult<()> {
    if let Some(window) = app.get_webview_window("main") {
        window.minimize()?;
    }
    Ok(())
}

#[tauri::command]
pub fn window_close(app: AppHandle) -> CmdResult<()> {
    if let Some(main) = app.get_webview_window("main") {
        main.close()?;
    }
    Ok(())
}

#[tauri::command]
pub fn app_open_external(_app: AppHandle, url: String) -> CmdResult<()> {
    validate_external_url(&url)?;
    Ok(open::that(url)?)
}

fn validate_external_url(value: &str) -> CmdResult<()> {
    let parsed =
        reqwest::Url::parse(value).map_err(|_| CommandError::msg("invalid external URL"))?;
    match parsed.scheme() {
        "https" | "http" | "mailto" => Ok(()),
        _ => Err(CommandError::msg("external URL scheme is not allowed")),
    }
}

#[tauri::command]
pub fn app_restart(app: AppHandle) -> CmdResult<()> {
    app.request_restart();
    Ok(())
}

#[tauri::command]
pub fn window_show_main(app: AppHandle) -> CmdResult<()> {
    crate::state::app_state::show_main_window(&app)?;
    Ok(())
}

#[tauri::command]
pub fn app_launch_context(launch: State<'_, LaunchContext>) -> CmdResult<LaunchContext> {
    Ok(*launch)
}

#[tauri::command]
pub fn app_quit(app: AppHandle, state: State<'_, AppState>) -> CmdResult<()> {
    state.request_shutdown(app);
    Ok(())
}

#[tauri::command]
pub fn window_open_devtools_all(app: AppHandle) -> CmdResult<()> {
    if let Some(main) = app.get_webview_window("main") {
        main.open_devtools();
        let _ = main.show();
    }
    for label in ["hand-overlay", "foot-overlay"] {
        if let Some(overlay) = app.get_webview_window(label) {
            overlay.open_devtools();
            let _ = overlay.show();
        }
    }
    Ok(())
}

/// macOS 시스템 커서 설정 반환 (크기, 색상)
/// 비-macOS 플랫폼에서는 기본값 반환
#[derive(Debug, Clone, serde::Serialize)]
pub struct CursorSettingsResponse {
    /// 커서 크기 배율 (1.0 ~ 4.0)
    pub size: f64,
    /// 커서 기본 픽셀 크기 (24px - macOS 기본 커서 크기)
    pub base_size: u32,
    /// 커서 채우기 색상 (HEX)
    pub fill_color: String,
    /// 커서 테두리 색상 (HEX)
    pub outline_color: String,
    /// macOS 플랫폼 여부
    pub is_macos: bool,
}

#[tauri::command]
pub fn get_cursor_settings() -> CursorSettingsResponse {
    let settings = get_macos_cursor_settings();

    let fill_color = settings
        .fill_color
        .map(rgb_to_hex)
        .unwrap_or_else(|| "#000000".to_string());

    let outline_color = settings
        .outline_color
        .map(rgb_to_hex)
        .unwrap_or_else(|| "#FFFFFF".to_string());

    CursorSettingsResponse {
        size: settings.size,
        base_size: 24,
        fill_color,
        outline_color,
        #[cfg(target_os = "macos")]
        is_macos: true,
        #[cfg(not(target_os = "macos"))]
        is_macos: false,
    }
}

#[cfg(test)]
mod tests {
    use super::validate_external_url;

    #[test]
    fn accepts_supported_external_url_schemes() {
        for url in [
            "https://example.com/release",
            "http://localhost:3400/docs",
            "mailto:maintainer@example.com",
        ] {
            assert!(validate_external_url(url).is_ok(), "{url}");
        }
    }

    #[test]
    fn rejects_unsupported_or_invalid_external_urls() {
        for url in [
            "file:///tmp/private.txt",
            "javascript:alert(1)",
            "tauri://localhost",
            "not a url",
            "",
        ] {
            assert!(validate_external_url(url).is_err(), "{url}");
        }
    }
}
