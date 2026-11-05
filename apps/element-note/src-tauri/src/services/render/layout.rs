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
    if let Some(surfaces) = request.get("surfaces").and_then(Value::as_array) {
        return explicit_surfaces(request, surfaces);
    }
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

/// The game names each viewer and how many output pixels one CSS pixel takes.
/// It places the frames itself, so no desktop window is read. The viewport is
/// only a first guess: the page reports the one it lays out once it is ready.
fn explicit_surfaces(request: &Value, surfaces: &[Value]) -> Result<Vec<Value>, String> {
    if surfaces.is_empty() || surfaces.len() > 2 {
        return Err("render-invalid-viewer".into());
    }
    let mut seen: Vec<&str> = Vec::new();
    let mut configs = Vec::new();
    for surface in surfaces {
        let viewer = surface["viewerKind"]
            .as_str()
            .filter(|kind| ["hand", "foot"].contains(kind) && !seen.contains(kind))
            .ok_or("render-invalid-viewer")?;
        seen.push(viewer);
        let scale = surface["scale"]
            .as_f64()
            .filter(|scale| scale.is_finite() && *scale > 0.0 && *scale <= 16.0)
            .ok_or("render-invalid-size")?;
        let viewport = |name: &str| {
            surface[name]
                .as_f64()
                .filter(|size| size.is_finite() && (1.0..=4096.0).contains(size))
                .ok_or("render-invalid-size")
        };
        let (surface_width, surface_height) = (viewport("surfaceWidth")?, viewport("surfaceHeight")?);
        let (width, height) = scaled_size(surface_width, surface_height, scale)?;
        let mut config = request.clone();
        if let Some(object) = config.as_object_mut() {
            object.remove("surfaces");
        }
        config["viewerKind"] = json!(viewer);
        config["scale"] = json!(scale);
        config["surfaceWidth"] = json!(surface_width);
        config["surfaceHeight"] = json!(surface_height);
        config["width"] = json!(width);
        config["height"] = json!(height);
        config["layout"] = Value::Null;
        configs.push(config);
    }
    Ok(configs)
}

/// Output pixels for a CSS viewport at `scale` output pixels per CSS pixel.
pub fn scaled_size(width: f64, height: f64, scale: f64) -> Result<(u32, u32), String> {
    let width = (width * scale).round().max(1.0);
    let height = (height * scale).round().max(1.0);
    if !width.is_finite() || !height.is_finite() || width > 8192.0 || height > 8192.0 {
        return Err("render-overlay-too-large".into());
    }
    Ok((width as u32, height as u32))
}

fn select_viewers<'a>(
    include_visible: bool,
    selected: &'a str,
    visible: &[&'a str],
) -> Result<Vec<&'a str>, String> {
    if include_visible {
        if visible.is_empty() {
            return Err("render-overlay-window-missing: Show a hand or foot ElementNote overlay before rendering.".into());
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
        "render-overlay-window-missing: Show the selected ElementNote overlay before rendering.",
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
    #[test]
    fn explicit_surfaces_scale_each_viewport_without_windows() {
        let request = json!({
            "sessionId": "0".repeat(32),
            "frameFormat": "rgba",
            "surfaces": [
                {"viewerKind": "hand", "scale": 1.5, "surfaceWidth": 300.0, "surfaceHeight": 200.0},
                {"viewerKind": "foot", "scale": 2.0, "surfaceWidth": 120.0, "surfaceHeight": 90.5}
            ]
        });
        let configs = explicit_surfaces(&request, request["surfaces"].as_array().unwrap()).unwrap();
        assert_eq!(configs.len(), 2);
        assert_eq!(configs[0]["viewerKind"], "hand");
        assert_eq!((configs[0]["width"].as_u64(), configs[0]["height"].as_u64()), (Some(450), Some(300)));
        assert_eq!((configs[1]["width"].as_u64(), configs[1]["height"].as_u64()), (Some(240), Some(181)));
        assert_eq!(configs[1]["frameFormat"], "rgba");
        assert!(configs[0].get("surfaces").is_none());
        assert!(configs[0]["layout"].is_null());
    }
    #[test]
    fn explicit_surfaces_reject_duplicates_and_bad_sizes() {
        let surface = |kind: &str, scale: f64, width: f64| {
            json!({"viewerKind": kind, "scale": scale, "surfaceWidth": width, "surfaceHeight": 100.0})
        };
        let request = json!({});
        let check = |surfaces: Vec<Value>| explicit_surfaces(&request, &surfaces);
        assert!(check(vec![]).is_err());
        assert!(check(vec![surface("hand", 1.0, 100.0), surface("hand", 1.0, 100.0)]).is_err());
        assert!(check(vec![surface("eye", 1.0, 100.0)]).is_err());
        assert!(check(vec![surface("hand", 0.0, 100.0)]).is_err());
        assert!(check(vec![surface("hand", f64::NAN, 100.0)]).is_err());
        assert!(check(vec![surface("hand", 1.0, 5000.0)]).is_err());
        assert_eq!(
            check(vec![surface("hand", 16.0, 4096.0)]).unwrap_err(),
            "render-overlay-too-large"
        );
    }
}
