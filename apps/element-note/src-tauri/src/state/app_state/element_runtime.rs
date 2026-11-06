use super::*;
use crate::models::{KeyViewerKind, OverlayBounds};

impl AppState {
    pub(crate) fn reset_foot_position(&self, app: &AppHandle) -> Result<OverlayBounds> {
        let window = self.ensure_foot_overlay(app)?;
        window.center()?;
        let scale = window.scale_factor()?;
        let position = window.outer_position()?;
        let size = window.inner_size()?;
        let bounds = OverlayBounds {
            x: position.x as f64 / scale,
            y: position.y as f64 / scale,
            width: size.width as f64 / scale,
            height: size.height as f64 / scale,
        };
        self.store.update_deferred(|store| {
            store.overlay_windows.foot.bounds = Some(bounds.clone());
            store.overlay_windows.foot.bounds_are_logical = true;
        })?;
        Ok(bounds)
    }

    pub fn release_live_keys(&self, app: &AppHandle) {
        for keyboard in [&self.keyboard, &self.foot_keyboard] {
            let (mode, keys) = keyboard.current_mode_and_pressed_keys();
            keyboard.clear_active_keys();
            for key in keys {
                publish_event(
                    app,
                    "keys:state",
                    json!({"mode": mode, "key": key, "state":"UP", "eventAgeMs":0}),
                );
            }
        }
    }

    pub(crate) fn refresh_viewer_keyboards(&self) {
        let snapshot = self.store.snapshot();
        let keys = viewer_runtime_mappings(&snapshot.keys, &snapshot.key_positions);
        self.keyboard
            .update_mappings_and_set_mode(keys.clone(), snapshot.selected_viewer_tabs.hand);
        self.foot_keyboard
            .update_mappings_and_set_mode(keys, snapshot.selected_viewer_tabs.foot);
    }

    pub(super) fn ensure_foot_overlay(&self, app: &AppHandle) -> Result<WebviewWindow> {
        if let Some(window) = app.get_webview_window("foot-overlay") {
            return Ok(window);
        }
        let snapshot = self.store.snapshot();
        let bounds = snapshot
            .overlay_windows
            .foot
            .bounds
            .clone()
            .unwrap_or_else(|| {
                let hand = snapshot.overlay_windows.hand.bounds.as_ref();
                OverlayBounds {
                    x: hand.map_or(32.0, |b| b.x),
                    y: hand.map_or(260.0, |b| b.y + b.height + 24.0),
                    width: 860.0,
                    height: 200.0,
                }
            });
        let window = WebviewWindowBuilder::new(
            app,
            "foot-overlay",
            WebviewUrl::App("overlay/index.html".into()),
        )
        .title("ElementNote - Foot")
        .decorations(false)
        .transparent(true)
        .resizable(false)
        .maximizable(false)
        .shadow(false)
        .focused(false)
        .visible(false)
        .skip_taskbar(true)
        .always_on_top(snapshot.always_on_top)
        .inner_size(bounds.width.max(1.0), bounds.height.max(1.0))
        .position(bounds.x, bounds.y)
        .build()?;
        window.set_ignore_cursor_events(snapshot.overlay_locked)?;
        #[cfg(target_os = "macos")]
        apply_macos_overlay_fullscreen_behavior(&window, snapshot.always_on_top);
        if snapshot.overlay_visible {
            show_overlay_window(&window, snapshot.always_on_top)?;
        }
        let handle = app.clone();
        window.on_window_event(move |event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                let state = handle.state::<AppState>();
                if !state.shutdown_started.load(Ordering::SeqCst) {
                    api.prevent_close();
                    if let Err(error) = state.set_overlay_visibility(&handle, false) {
                        log::warn!("failed to hide viewer windows: {error:#}");
                    }
                }
            }
            if matches!(
                event,
                tauri::WindowEvent::Moved(_) | tauri::WindowEvent::Resized(_)
            ) {
                if let Some(window) = handle.get_webview_window("foot-overlay") {
                    if let (Ok(position), Ok(size), Ok(scale)) = (
                        window.outer_position(),
                        window.inner_size(),
                        window.scale_factor(),
                    ) {
                        let bounds = OverlayBounds {
                            x: position.x as f64 / scale,
                            y: position.y as f64 / scale,
                            width: size.width as f64 / scale,
                            height: size.height as f64 / scale,
                        };
                        let _ = handle.state::<AppState>().store.update_deferred(|store| {
                            store.overlay_windows.foot.bounds = Some(bounds);
                            store.overlay_windows.foot.bounds_are_logical = true;
                        });
                    }
                }
            }
        });
        Ok(window)
    }

    #[allow(clippy::too_many_arguments)] // Mirrors the native overlay resize command options.
    pub(crate) fn resize_viewer_overlay(
        &self,
        app: &AppHandle,
        kind: KeyViewerKind,
        width: f64,
        height: f64,
        anchor: Option<String>,
        left: Option<f64>,
        top: Option<f64>,
        delta_x: Option<f64>,
        delta_y: Option<f64>,
    ) -> Result<OverlayBounds> {
        if kind == KeyViewerKind::Hand {
            let bounds =
                self.resize_overlay(app, width, height, anchor, left, top, delta_x, delta_y)?;
            self.store.update_deferred(|store| {
                store.overlay_windows.hand.bounds = Some(bounds.clone());
                store.overlay_windows.hand.last_content_top_offset = top;
                store.overlay_windows.hand.bounds_are_logical = true;
            })?;
            return Ok(bounds);
        }
        let window = self.ensure_foot_overlay(app)?;
        let scale = window.scale_factor()?;
        let position = window.outer_position()?;
        let size = window.inner_size()?;
        let width = clamp_overlay_dimension(width);
        let height = clamp_overlay_dimension(height);
        let current_width = size.width as f64 / scale;
        let current_height = size.height as f64 / scale;
        let mut x = position.x as f64 / scale;
        let mut y = position.y as f64 / scale;
        let anchor =
            anchor.unwrap_or_else(|| self.store.snapshot().overlay_resize_anchor.as_str().into());
        if anchor.contains("right") {
            x += current_width - width;
        }
        if anchor.contains("bottom") {
            y += current_height - height;
        }
        if anchor == "center" {
            x += (current_width - width) / 2.0;
            y += (current_height - height) / 2.0;
        }
        if anchor == "fixed-position" {
            x += delta_x.unwrap_or(0.0);
            y += delta_y.unwrap_or(0.0);
        }
        window.set_size(LogicalSize::new(width, height))?;
        window.set_position(LogicalPosition::new(x, y))?;
        let bounds = OverlayBounds {
            x,
            y,
            width,
            height,
        };
        self.store.update_deferred(|store| {
            store.overlay_windows.foot.bounds = Some(bounds.clone());
            store.overlay_windows.foot.last_content_top_offset = top;
            store.overlay_windows.foot.bounds_are_logical = true;
        })?;
        publish_event(
            app,
            "overlay:resized",
            json!({"viewerKind":"foot", "x":x,"y":y,"width":width,"height":height}),
        );
        Ok(bounds)
    }
}
