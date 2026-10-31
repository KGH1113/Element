//! Describes OBS pages rendered directly by the game's native webviews.

use serde::Serialize;
use serde_json::{json, Value};
use std::sync::atomic::Ordering;
use tauri::{AppHandle, Emitter, Manager};

use super::render::RenderService;
use crate::models::{AppStoreData, KeyViewerKind, OverlayWindowLayout};

#[derive(Debug, Serialize)]
pub struct NativeViewerError {
    code: &'static str,
    message: &'static str,
    retryable: bool,
}

impl NativeViewerError {
    fn new(code: &'static str, message: &'static str, retryable: bool) -> Self {
        Self {
            code,
            message,
            retryable,
        }
    }
}

/// Tells the game to describe its pages again. The event carries no data:
/// descriptors include the OBS token and are sent only over the game IPC.
pub fn notify_changed(app: &AppHandle) {
    let _ = app.emit("native-viewers:changed", ());
}

/// The game owns rendering; describing pages never starts OBS or closes windows.
pub fn describe(app: &AppHandle, session_id: &str) -> Result<Value, NativeViewerError> {
    validate_session_id(session_id).map_err(|_| {
        NativeViewerError::new("invalid-session", "Invalid in-game viewer session.", false)
    })?;
    if app
        .state::<RenderService>()
        .input_gated
        .load(Ordering::Acquire)
    {
        return Err(NativeViewerError::new(
            "replay-render-active",
            "A replay render is using ElementNote. Try again after it finishes.",
            true,
        ));
    }
    let state = app.state::<crate::state::AppState>();
    if !state
        .store
        .with_state(|store| store.in_game_viewers_enabled)
    {
        return Err(NativeViewerError::new(
            "in-game-disabled",
            "Show inside ADOFAI is off in ElementNote.",
            true,
        ));
    }
    if !state.is_obs_mode_active() {
        return Err(NativeViewerError::new(
            "obs-required",
            "Enable OBS mode in ElementNote to use the native in-game viewers.",
            true,
        ));
    }
    let url = state.obs_bridge.local_http_url().ok_or_else(|| {
        NativeViewerError::new(
            "obs-not-ready",
            "The OBS viewer service is starting. Try again shortly.",
            true,
        )
    })?;
    let surfaces = state.store.with_state(|store| native_surfaces(store, &url));
    Ok(json!({ "sessionId": session_id, "surfaces": surfaces }))
}

fn native_surfaces(store: &AppStoreData, base_url: &tauri::Url) -> Vec<Value> {
    let mut surfaces = Vec::with_capacity(2);
    for (kind, viewer, selected, layout) in [
        (
            KeyViewerKind::Hand,
            "hand",
            &store.selected_viewer_tabs.hand,
            &store.overlay_windows.hand,
        ),
        (
            KeyViewerKind::Foot,
            "foot",
            &store.selected_viewer_tabs.foot,
            &store.overlay_windows.foot,
        ),
    ] {
        if !store
            .tabs
            .iter()
            .any(|tab| tab.viewer_kind == kind && &tab.id == selected)
        {
            continue;
        }
        let mut url = base_url.clone();
        url.query_pairs_mut().append_pair("viewer", viewer);
        let (width, height) = native_viewport_size(layout);
        // Dimensions are the source CSS viewport. The native host fits them
        // within 430x270 and sets browser page zoom, preserving CSS layout.
        surfaces.push(json!({
            "viewerKind": viewer,
            "url": url.as_str(),
            "width": width,
            "height": height,
        }));
    }
    surfaces
}

fn native_viewport_size(layout: &OverlayWindowLayout) -> (u32, u32) {
    if let Some(bounds) = layout.bounds.as_ref().filter(|_| layout.bounds_are_logical) {
        if bounds.width.is_finite()
            && bounds.height.is_finite()
            && bounds.width > 0.0
            && bounds.height > 0.0
        {
            return (
                bounds.width.round().clamp(1.0, 4096.0) as u32,
                bounds.height.round().clamp(1.0, 4096.0) as u32,
            );
        }
    }
    // Legacy physical bounds cannot identify the old monitor's scale factor.
    (860, 320)
}

fn validate_session_id(id: &str) -> Result<(), String> {
    if id.len() == 32 && id.bytes().all(|byte| byte.is_ascii_hexdigit()) {
        Ok(())
    } else {
        Err("Invalid in-game viewer session.".into())
    }
}

#[cfg(test)]
mod tests {
    use super::{native_surfaces, native_viewport_size, validate_session_id};
    use crate::models::{AppStoreData, OverlayBounds, OverlayWindowLayout};

    #[test]
    fn session_id_requires_a_bounded_hex_identifier() {
        assert!(validate_session_id("0123456789abcdef0123456789abcdef").is_ok());
        assert!(validate_session_id("../other-session").is_err());
    }

    #[test]
    fn native_pages_use_the_selected_viewers_and_authenticated_loopback_url() {
        let mut store = AppStoreData::default();
        // The global editor selection must not make both pages display feet.
        store.selected_key_type = store.selected_viewer_tabs.foot.clone();
        let base = tauri::Url::parse("http://127.0.0.1:34999/?token=test-token").unwrap();
        let surfaces = native_surfaces(&store, &base);
        assert_eq!(surfaces.len(), 2);
        for (surface, viewer) in surfaces.iter().zip(["hand", "foot"]) {
            assert_eq!(surface["viewerKind"], viewer);
            let url = tauri::Url::parse(surface["url"].as_str().unwrap()).unwrap();
            assert_eq!(url.host_str(), Some("127.0.0.1"));
            assert_eq!(url.port(), Some(34999));
            let query = url
                .query_pairs()
                .collect::<std::collections::HashMap<_, _>>();
            assert_eq!(
                query.get("token").map(|value| value.as_ref()),
                Some("test-token")
            );
            assert_eq!(
                query.get("viewer").map(|value| value.as_ref()),
                Some(viewer)
            );
            assert_eq!(surface["width"], 860);
            assert_eq!(surface["height"], 320);
        }
        store.tabs.clear();
        assert!(native_surfaces(&store, &base).is_empty());
    }

    #[test]
    fn native_viewport_uses_bounded_logical_sizes() {
        let mut layout = OverlayWindowLayout {
            bounds: Some(OverlayBounds {
                x: 0.0,
                y: 0.0,
                width: 1720.0,
                height: 640.0,
            }),
            bounds_are_logical: true,
            ..Default::default()
        };
        assert_eq!(native_viewport_size(&layout), (1720, 640));
        layout.bounds.as_mut().unwrap().width = 8000.0;
        assert_eq!(native_viewport_size(&layout), (4096, 640));
        layout.bounds.as_mut().unwrap().height = f64::NAN;
        assert_eq!(native_viewport_size(&layout), (860, 320));
        layout.bounds.as_mut().unwrap().height = 640.0;
        layout.bounds_are_logical = false;
        assert_eq!(native_viewport_size(&layout), (860, 320));
    }
}
