//! Match a live overlay to the game's content rectangle in desktop coordinates.
use serde_json::{json, Value};
use tauri::{AppHandle, Manager};

#[derive(Clone, Copy, Debug)]
pub struct Rect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

// One isolated surface per visible viewer preserves each CSS viewport and avoids
// capturing a large, mostly transparent rectangle between the two live windows.
pub fn prepare_surfaces(app: &AppHandle, request: &Value) -> Result<Vec<Value>, String> {
    let automatic = request["automaticPlacement"].as_bool() == Some(true);
    let viewers = if automatic && request["includeVisibleViewers"].as_bool() == Some(true) {
        let mut visible = Vec::new();
        for kind in ["hand", "foot"] {
            if let Some(window) = app.get_webview_window(&format!("{kind}-overlay")) {
                if window.is_visible().map_err(|e| e.to_string())? {
                    visible.push(kind);
                }
            }
        }
        select_viewers(
            true,
            request["viewerKind"].as_str().unwrap_or("hand"),
            &visible,
        )?
    } else {
        select_viewers(false, request["viewerKind"].as_str().unwrap_or("hand"), &[])?
    };
    viewers
        .into_iter()
        .map(|viewer| {
            let mut config = request.clone();
            config["viewerKind"] = json!(viewer);
            config["layout"] = prepare(app, &mut config)?;
            Ok(config)
        })
        .collect()
}

fn select_viewers<'a>(
    include_visible: bool,
    selected: &'a str,
    visible: &[&'a str],
) -> Result<Vec<&'a str>, String> {
    if include_visible {
        if visible.is_empty() {
            return Err("render-overlay-window-missing: Show a hand or foot ImplDmNote overlay before rendering.".into());
        }
        Ok(visible.to_vec())
    } else if ["hand", "foot"].contains(&selected) {
        Ok(vec![selected])
    } else {
        Err("render-invalid-viewer".into())
    }
}

pub fn prepare(app: &AppHandle, request: &mut Value) -> Result<Value, String> {
    if request["automaticPlacement"].as_bool() != Some(true) {
        return Ok(Value::Null);
    }
    let viewer = request["viewerKind"].as_str().unwrap_or("hand");
    let overlay = app.get_webview_window(&format!("{viewer}-overlay")).ok_or(
        "render-overlay-window-missing: Show the selected ImplDmNote overlay before rendering.",
    )?;
    let scale = overlay.scale_factor().map_err(|e| e.to_string())?;
    let position = overlay.inner_position().map_err(|e| e.to_string())?;
    let size = overlay.inner_size().map_err(|e| e.to_string())?;
    // macOS desktop window metadata is in points; Windows uses physical pixels.
    let units = if cfg!(target_os = "macos") {
        scale
    } else {
        1.0
    };
    let bounds = Rect {
        x: position.x as f64 / units,
        y: position.y as f64 / units,
        width: size.width as f64 / units,
        height: size.height as f64 / units,
    };
    let pid = request["gameProcessId"]
        .as_u64()
        .ok_or("render-game-process-missing")? as u32;
    let game = game_rect(pid).ok_or("render-game-window-missing: The game's window bounds could not be read. Keep the game window visible or use manual placement.")?;
    #[cfg(target_os = "macos")]
    let game = {
        let mut game = game;
        // CGWindow bounds include the title bar. Unity reports the drawable aspect
        // ratio, so align its content to the bottom of the native window.
        let aspect = request["gameViewportWidth"].as_f64().unwrap_or(0.0)
            / request["gameViewportHeight"].as_f64().unwrap_or(0.0);
        if !aspect.is_finite() || aspect <= 0.0 {
            return Err("render-game-viewport-invalid".into());
        }
        let height = (game.width / aspect).min(game.height);
        game.y += game.height - height;
        game.height = height;
        game
    };
    let (width, height, layout) = map(
        bounds,
        game,
        request["outputWidth"].as_f64().unwrap_or(0.0),
        request["outputHeight"].as_f64().unwrap_or(0.0),
    )?;
    // Preserve the live CSS viewport. Capture pixels at their final video size.
    request["surfaceWidth"] = json!(size.width as f64 / scale);
    request["surfaceHeight"] = json!(size.height as f64 / scale);
    request["width"] = json!(width);
    request["height"] = json!(height);
    Ok(layout)
}

fn map(
    overlay: Rect,
    game: Rect,
    video_width: f64,
    video_height: f64,
) -> Result<(u32, u32, Value), String> {
    if [overlay.x, overlay.y, game.x, game.y]
        .iter()
        .any(|v| !v.is_finite())
        || [
            overlay.width,
            overlay.height,
            game.width,
            game.height,
            video_width,
            video_height,
        ]
        .iter()
        .any(|v| !v.is_finite() || *v <= 0.0)
    {
        return Err("render-window-bounds-invalid".into());
    }
    let width = (overlay.width / game.width * video_width).round().max(1.0);
    let height = (overlay.height / game.height * video_height)
        .round()
        .max(1.0);
    if width > 8192.0 || height > 8192.0 {
        return Err("render-overlay-too-large".into());
    }
    Ok((
        width as u32,
        height as u32,
        json!({
            "left": (overlay.x - game.x) / game.width,
            "top": (overlay.y - game.y) / game.height, "scale": 1.0
        }),
    ))
}

#[cfg(target_os = "macos")]
fn game_rect(pid: u32) -> Option<Rect> {
    use objc::{class, msg_send, runtime::Object, sel, sel_impl};
    use std::ffi::{c_void, CString};
    #[link(name = "CoreGraphics", kind = "framework")]
    extern "C" {
        fn CGWindowListCopyWindowInfo(options: u32, relative: u32) -> *mut Object;
    }
    #[link(name = "CoreFoundation", kind = "framework")]
    extern "C" {
        fn CFRelease(value: *const c_void);
    }
    unsafe fn number(dictionary: *mut Object, name: &str) -> f64 {
        let name = CString::new(name).unwrap();
        let key: *mut Object = msg_send![class!(NSString), stringWithUTF8String:name.as_ptr()];
        let value: *mut Object = msg_send![dictionary, objectForKey:key];
        if value.is_null() {
            return 0.0;
        }
        msg_send![value, doubleValue]
    }
    unsafe {
        let windows = CGWindowListCopyWindowInfo(1, 0); // onscreen metadata only, no screen capture
        if windows.is_null() {
            return None;
        }
        let count: usize = msg_send![windows, count];
        let mut result: Option<Rect> = None;
        for index in 0..count {
            let window: *mut Object = msg_send![windows, objectAtIndex:index];
            if number(window, "kCGWindowOwnerPID") as u32 != pid
                || number(window, "kCGWindowLayer") != 0.0
            {
                continue;
            }
            let key: *mut Object =
                msg_send![class!(NSString), stringWithUTF8String:c"kCGWindowBounds".as_ptr()];
            let bounds: *mut Object = msg_send![window, objectForKey:key];
            if bounds.is_null() {
                continue;
            }
            let rect = Rect {
                x: number(bounds, "X"),
                y: number(bounds, "Y"),
                width: number(bounds, "Width"),
                height: number(bounds, "Height"),
            };
            if rect.width > 0.0
                && rect.height > 0.0
                && result.is_none_or(|old| old.width * old.height < rect.width * rect.height)
            {
                result = Some(rect);
            }
        }
        CFRelease(windows.cast());
        result
    }
}

#[cfg(target_os = "windows")]
fn game_rect(pid: u32) -> Option<Rect> {
    use windows::{
        core::BOOL,
        Win32::{
            Foundation::{HWND, LPARAM},
            UI::WindowsAndMessaging::{
                EnumWindows, GetWindowInfo, GetWindowThreadProcessId, IsWindowVisible, WINDOWINFO,
            },
        },
    };
    struct Search {
        pid: u32,
        result: Option<Rect>,
    }
    unsafe extern "system" fn visit(window: HWND, data: LPARAM) -> BOOL {
        let search = &mut *(data.0 as *mut Search);
        let mut pid = 0;
        GetWindowThreadProcessId(window, Some(&mut pid));
        if pid == search.pid && IsWindowVisible(window).as_bool() {
            let mut info = WINDOWINFO {
                cbSize: std::mem::size_of::<WINDOWINFO>() as u32,
                ..Default::default()
            };
            if GetWindowInfo(window, &mut info).is_ok() {
                let r = info.rcClient;
                let rect = Rect {
                    x: r.left as f64,
                    y: r.top as f64,
                    width: (r.right - r.left) as f64,
                    height: (r.bottom - r.top) as f64,
                };
                if rect.width > 0.0
                    && rect.height > 0.0
                    && search
                        .result
                        .is_none_or(|old| old.width * old.height < rect.width * rect.height)
                {
                    search.result = Some(rect);
                }
            }
        }
        BOOL(1)
    }
    let mut search = Search { pid, result: None };
    unsafe {
        let _ = EnumWindows(Some(visit), LPARAM((&mut search as *mut Search) as isize));
    }
    search.result
}

#[cfg(not(any(target_os = "macos", target_os = "windows")))]
fn game_rect(_: u32) -> Option<Rect> {
    None
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn visible_viewers_include_feet_and_manual_selection_stays_explicit() {
        assert_eq!(
            select_viewers(true, "hand", &["hand", "foot"]).unwrap(),
            ["hand", "foot"]
        );
        assert_eq!(select_viewers(true, "hand", &["foot"]).unwrap(), ["foot"]);
        assert_eq!(select_viewers(true, "hand", &["hand"]).unwrap(), ["hand"]);
        assert!(select_viewers(true, "hand", &[])
            .unwrap_err()
            .contains("render-overlay-window-missing"));
        assert_eq!(
            select_viewers(false, "foot", &["hand", "foot"]).unwrap(),
            ["foot"]
        );
        assert!(select_viewers(false, "unknown", &[]).is_err());
    }
    #[test]
    fn placement_uses_game_content_and_output_size() {
        let game = Rect {
            x: -1600.0,
            y: 100.0,
            width: 1600.0,
            height: 900.0,
        };
        let overlay = Rect {
            x: -1200.0,
            y: 550.0,
            width: 400.0,
            height: 150.0,
        };
        let (width, height, layout) = map(overlay, game, 1920.0, 1080.0).unwrap();
        assert_eq!((width, height), (480, 180));
        assert_eq!(layout["left"], 0.25);
        assert_eq!(layout["top"], 0.5);
    }
    #[test]
    fn partially_offscreen_positions_stay_negative() {
        let game = Rect {
            x: 100.0,
            y: 50.0,
            width: 1000.0,
            height: 500.0,
        };
        let overlay = Rect {
            x: 50.0,
            y: 25.0,
            width: 200.0,
            height: 100.0,
        };
        let (_, _, layout) = map(overlay, game, 2000.0, 1000.0).unwrap();
        assert_eq!(layout["left"], -0.05);
        assert_eq!(layout["top"], -0.05);
    }
}
