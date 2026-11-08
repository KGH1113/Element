//! Size each render surface from the viewers and scale the game asks for.
use serde_json::{json, Value};

// One isolated surface per viewer preserves each CSS viewport and avoids
// capturing a large, mostly transparent rectangle between the two viewers.
pub fn prepare_surfaces(request: &Value) -> Result<Vec<Value>, String> {
    let surfaces = request
        .get("surfaces")
        .and_then(Value::as_array)
        .ok_or("render-invalid-viewer")?;
    explicit_surfaces(request, surfaces)
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
        let (surface_width, surface_height) =
            (viewport("surfaceWidth")?, viewport("surfaceHeight")?);
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

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn explicit_surfaces_scale_each_viewport_without_windows() {
        let request = json!({
            "sessionId": "0".repeat(32),
            "surfaces": [
                {"viewerKind": "hand", "scale": 1.5, "surfaceWidth": 300.0, "surfaceHeight": 200.0},
                {"viewerKind": "foot", "scale": 2.0, "surfaceWidth": 120.0, "surfaceHeight": 90.5}
            ]
        });
        let configs = explicit_surfaces(&request, request["surfaces"].as_array().unwrap()).unwrap();
        assert_eq!(configs.len(), 2);
        assert_eq!(configs[0]["viewerKind"], "hand");
        assert_eq!(
            (configs[0]["width"].as_u64(), configs[0]["height"].as_u64()),
            (Some(450), Some(300))
        );
        assert_eq!(
            (configs[1]["width"].as_u64(), configs[1]["height"].as_u64()),
            (Some(240), Some(181))
        );
        assert!(configs[0].get("surfaces").is_none());
        assert!(configs[0]["layout"].is_null());
    }
    #[test]
    fn explicit_surfaces_reject_duplicates_and_bad_sizes() {
        let surface = |kind: &str, scale: f64, width: f64| json!({"viewerKind": kind, "scale": scale, "surfaceWidth": width, "surfaceHeight": 100.0});
        let request = json!({});
        let check = |surfaces: Vec<Value>| explicit_surfaces(&request, &surfaces);
        assert!(check(vec![]).is_err());
        assert!(check(vec![
            surface("hand", 1.0, 100.0),
            surface("hand", 1.0, 100.0)
        ])
        .is_err());
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
