//! Captures the two live ImplDmNote viewers for the in-game overlay.
//! The caller requests one frame at a time, so capture cannot outrun the game.

use parking_lot::Mutex;
use serde_json::{json, Value};
use std::{fs, path::PathBuf, sync::atomic::Ordering};
use tauri::{AppHandle, Manager};

use super::render::{capture, RenderService};

#[derive(Default)]
pub struct LiveMirrorService {
    operation: tokio::sync::Mutex<()>,
    session: Mutex<Option<MirrorSession>>,
}

struct MirrorSession {
    id: String,
    directory: PathBuf,
}

impl LiveMirrorService {
    pub async fn frame(&self, app: &AppHandle, session_id: &str) -> Result<Value, String> {
        validate_session_id(session_id)?;
        let _operation = self.operation.lock().await;
        if app
            .state::<RenderService>()
            .input_gated
            .load(Ordering::Acquire)
        {
            return Err("A replay render is using ImplDmNote. Try again after it finishes.".into());
        }
        if app.state::<crate::state::AppState>().is_obs_mode_active() {
            return Err("The desktop viewers are unavailable while OBS mode is active.".into());
        }

        let directory = self.prepare_session(app, session_id)?;
        let mut frames = Vec::with_capacity(2);
        for (viewer, label) in [("hand", "hand-overlay"), ("foot", "foot-overlay")] {
            let Some(window) = app.get_webview_window(label) else {
                continue;
            };
            let size = window.inner_size().map_err(|error| error.to_string())?;
            if size.width == 0 || size.height == 0 || size.width > 4096 || size.height > 4096 {
                return Err(format!("The {viewer} viewer has an unsupported size."));
            }
            let rgba = cfg!(target_os = "macos");
            let (width, height) = if rgba {
                capture_size(size.width, size.height, 860, 540)
            } else {
                (size.width, size.height)
            };
            let pixels = capture::frame(&window, width, height, rgba).await?;
            let extension = if rgba { "rgba" } else { "png" };
            let path = directory.join(format!("{viewer}.{extension}"));
            let temporary = directory.join(format!("{viewer}.partial"));
            fs::write(&temporary, pixels).map_err(|error| error.to_string())?;
            if path.exists() {
                fs::remove_file(&path).map_err(|error| error.to_string())?;
            }
            fs::rename(&temporary, &path).map_err(|error| error.to_string())?;
            frames.push(json!({
                "viewerKind": viewer,
                "width": width,
                "height": height,
                "format": extension,
                "path": path,
            }));
        }
        if frames.is_empty() {
            return Err("Open a hand or foot viewer in ImplDmNote first.".into());
        }
        Ok(json!({ "sessionId": session_id, "frames": frames }))
    }

    pub async fn end(&self, session_id: Option<&str>) {
        let _operation = self.operation.lock().await;
        let mut session = self.session.lock();
        if session_id.is_some_and(|id| session.as_ref().is_some_and(|active| active.id != id)) {
            return;
        }
        if let Some(active) = session.take() {
            let _ = fs::remove_dir_all(active.directory);
        }
    }

    fn prepare_session(&self, app: &AppHandle, id: &str) -> Result<PathBuf, String> {
        let mut session = self.session.lock();
        if let Some(active) = session.as_ref() {
            if active.id == id {
                return Ok(active.directory.clone());
            }
        }
        if let Some(old) = session.take() {
            let _ = fs::remove_dir_all(old.directory);
        }
        let directory = app
            .path()
            .app_cache_dir()
            .map_err(|error| error.to_string())?
            .join("game-mirror")
            .join(id);
        fs::create_dir_all(&directory).map_err(|error| error.to_string())?;
        *session = Some(MirrorSession {
            id: id.to_string(),
            directory: directory.clone(),
        });
        Ok(directory)
    }
}

fn capture_size(width: u32, height: u32, max_width: u32, max_height: u32) -> (u32, u32) {
    let scale = (max_width as f64 / width as f64)
        .min(max_height as f64 / height as f64)
        .min(1.0);
    (
        (width as f64 * scale).round().max(1.0) as u32,
        (height as f64 * scale).round().max(1.0) as u32,
    )
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
    use super::{capture_size, validate_session_id};

    #[test]
    fn capture_fits_display_resolution_without_changing_aspect_ratio() {
        assert_eq!(capture_size(1720, 640, 860, 540), (860, 320));
        assert_eq!(capture_size(400, 200, 860, 540), (400, 200));
        assert_eq!(capture_size(860, 1080, 860, 540), (430, 540));
    }

    #[test]
    fn session_id_cannot_escape_the_cache_directory() {
        assert!(validate_session_id("0123456789abcdef0123456789abcdef").is_ok());
        assert!(validate_session_id("../other-session").is_err());
    }
}
