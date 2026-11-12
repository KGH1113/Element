use anyhow::Result;
use tauri::{AppHandle, LogicalSize, Manager, PhysicalPosition, Position, Size, WebviewWindow};

use crate::{models::MainWindowBounds, state::AppState};

const DEFAULT_WIDTH: f64 = 1120.0;
const DEFAULT_HEIGHT: f64 = 720.0;
const MIN_WIDTH: f64 = 860.0;
const MIN_HEIGHT: f64 = 560.0;

fn fitted_dimension(saved: Option<f64>, default: f64, minimum: f64, available: f64) -> (f64, f64) {
    let available = if available.is_finite() && available > 0.0 {
        available
    } else {
        default
    };
    let minimum = minimum.min(available);
    let desired = saved
        .filter(|size| size.is_finite() && *size > 0.0)
        .unwrap_or(default);
    (desired.clamp(minimum, available), minimum)
}

pub(crate) fn restore_bounds(app: &AppHandle, window: &WebviewWindow) -> Result<()> {
    let saved = app
        .state::<AppState>()
        .store
        .with_state(|data| data.main_window_bounds);
    let monitor = saved
        .and_then(|bounds| {
            app.available_monitors().ok()?.into_iter().find(|monitor| {
                let area = monitor.work_area();
                let x = i64::from(bounds.x) - i64::from(area.position.x);
                let y = i64::from(bounds.y) - i64::from(area.position.y);
                x >= 0
                    && y >= 0
                    && x < i64::from(area.size.width)
                    && y < i64::from(area.size.height)
            })
        })
        .or_else(|| app.primary_monitor().ok().flatten());

    let (available_width, available_height) = monitor
        .as_ref()
        .map(|monitor| {
            let area = monitor
                .work_area()
                .size
                .to_logical::<f64>(monitor.scale_factor());
            // Keep the native frame and a little breathing room inside the usable screen.
            ((area.width - 32.0).max(1.0), (area.height - 48.0).max(1.0))
        })
        .unwrap_or((DEFAULT_WIDTH, DEFAULT_HEIGHT));
    let (width, min_width) = fitted_dimension(
        saved.map(|bounds| bounds.width),
        DEFAULT_WIDTH,
        MIN_WIDTH,
        available_width,
    );
    let (height, min_height) = fitted_dimension(
        saved.map(|bounds| bounds.height),
        DEFAULT_HEIGHT,
        MIN_HEIGHT,
        available_height,
    );
    window.set_max_size(None::<Size>)?;
    window.set_min_size(Some(Size::Logical(LogicalSize::new(min_width, min_height))))?;
    window.set_size(Size::Logical(LogicalSize::new(width, height)))?;

    if let Some(monitor) = monitor {
        let area = monitor.work_area();
        let size = window.outer_size()?;
        let origin_x = f64::from(area.position.x);
        let origin_y = f64::from(area.position.y);
        let remaining_width = (f64::from(area.size.width) - f64::from(size.width)).max(0.0);
        let remaining_height = (f64::from(area.size.height) - f64::from(size.height)).max(0.0);
        let x = saved.map_or(origin_x + remaining_width / 2.0, |bounds| {
            f64::from(bounds.x)
        });
        let y = saved.map_or(origin_y + remaining_height / 2.0, |bounds| {
            f64::from(bounds.y)
        });
        window.set_position(Position::Physical(PhysicalPosition::new(
            x.clamp(origin_x, origin_x + remaining_width).round() as i32,
            y.clamp(origin_y, origin_y + remaining_height).round() as i32,
        )))?;
    } else {
        window.center()?;
    }
    Ok(())
}

/// Save only at lifecycle boundaries, never on every move/resize frame.
/// A minimized or maximized window must not replace the user's normal size.
pub(crate) fn capture_bounds(app: &AppHandle) -> Result<()> {
    let Some(window) = app.get_webview_window("main") else {
        return Ok(());
    };
    if window.is_minimized()? || window.is_maximized()? || window.is_fullscreen()? {
        return Ok(());
    }
    let size = window
        .inner_size()?
        .to_logical::<f64>(window.scale_factor()?);
    if size.width <= 0.0 || size.height <= 0.0 {
        return Ok(());
    }
    let position = window.outer_position()?;
    let bounds = MainWindowBounds {
        x: position.x,
        y: position.y,
        width: size.width,
        height: size.height,
    };
    let state = app.state::<AppState>();
    if state
        .store
        .with_state(|data| data.main_window_bounds == Some(bounds))
    {
        return Ok(());
    }
    state
        .store
        .update(|data| data.main_window_bounds = Some(bounds))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn restores_user_size_without_exceeding_current_screen() {
        assert_eq!(
            fitted_dimension(Some(1440.0), DEFAULT_WIDTH, MIN_WIDTH, 1200.0),
            (1200.0, 860.0)
        );
        assert_eq!(
            fitted_dimension(Some(980.0), DEFAULT_WIDTH, MIN_WIDTH, 1600.0),
            (980.0, 860.0)
        );
    }

    #[test]
    fn small_screens_can_still_show_the_whole_window() {
        assert_eq!(
            fitted_dimension(None, DEFAULT_WIDTH, MIN_WIDTH, 768.0),
            (768.0, 768.0)
        );
        assert_eq!(
            fitted_dimension(None, DEFAULT_HEIGHT, MIN_HEIGHT, 452.0),
            (452.0, 452.0)
        );
    }

    #[test]
    fn invalid_saved_dimensions_fall_back_and_tiny_sizes_obey_minimum() {
        for invalid in [f64::NAN, f64::INFINITY, -20.0, 0.0] {
            assert_eq!(
                fitted_dimension(Some(invalid), DEFAULT_WIDTH, MIN_WIDTH, 1600.0),
                (1120.0, 860.0)
            );
        }
        assert_eq!(
            fitted_dimension(Some(200.0), DEFAULT_WIDTH, MIN_WIDTH, 1600.0),
            (860.0, 860.0)
        );
    }
}
