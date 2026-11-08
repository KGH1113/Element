//! One key viewer's video: draws each requested frame on its page, captures it
//! and hands it to FFmpeg, apart from the game and from the other viewer.
use super::{capture, encoder::Encoder, RenderService};
use serde_json::{json, Value};
use std::{
    path::PathBuf,
    sync::Arc,
    time::{Duration, Instant},
};
use tauri::{AppHandle, Manager};
use tokio::sync::mpsc;

/// The game skips a frame only when it repeats one; a far jump is a bug, and
/// would otherwise write minutes of repeated frames.
const MAX_GAP_SECONDS: u64 = 600;

/// What one requested output frame writes to the video.
#[derive(Debug, PartialEq, Eq)]
pub struct Step {
    /// Consecutive index for the page, which refuses frames out of sequence.
    pub page_frame: u64,
    /// Copies of the previous picture for output frames the game skipped.
    pub repeats: u64,
    /// Copies of the new picture; more than one only before the first picture.
    pub copies: u64,
}

/// Maps the game's output frame indices onto a video of consecutive frames.
pub struct FramePlan {
    next: u64,
    page_frames: u64,
    max_gap: u64,
}

impl FramePlan {
    pub fn new(fps: u32) -> Self {
        Self {
            next: 0,
            page_frames: 0,
            max_gap: u64::from(fps) * MAX_GAP_SECONDS,
        }
    }

    pub fn step(&mut self, frame: u64) -> Result<Step, String> {
        if frame < self.next {
            return Err("render-frame-out-of-sequence".into());
        }
        let gap = frame - self.next;
        if gap > self.max_gap {
            return Err("render-frame-gap-too-large".into());
        }
        let first = self.page_frames == 0;
        let step = Step {
            page_frame: self.page_frames,
            // Frames before the first picture show the first picture.
            repeats: if first { 0 } else { gap },
            copies: if first { gap + 1 } else { 1 },
        };
        self.next = frame + 1;
        self.page_frames += 1;
        Ok(step)
    }
}

pub struct Surface {
    pub label: String,
    pub viewer: String,
    pub width: u32,
    pub height: u32,
    pub path: PathBuf,
}

/// The finished video of one viewer.
#[derive(Debug)]
pub struct Video {
    pub viewer: String,
    pub path: PathBuf,
    pub width: u32,
    pub height: u32,
    pub frames: u64,
}

impl Video {
    pub fn describe(&self) -> Value {
        json!({"viewerKind": self.viewer, "path": self.path, "width": self.width,
            "height": self.height, "frames": self.frames})
    }
}

pub async fn run(
    app: AppHandle,
    surface: Surface,
    fps: u32,
    mut requests: mpsc::UnboundedReceiver<Arc<Value>>,
    mut encoder: Encoder,
) -> Result<Video, String> {
    let started = Instant::now();
    let mut plan = FramePlan::new(fps);
    let mut timing = Timing::default();
    let mut previous: Option<Arc<Vec<u8>>> = None;
    let mut warned_size = false;
    while let Some(request) = requests.recv().await {
        let frame = request["frameIndex"]
            .as_u64()
            .ok_or("render-invalid-frame")?;
        let step = plan.step(frame)?;
        let service = app.state::<RenderService>();
        let drawing = Instant::now();
        let mut page_request = request.as_ref().clone();
        page_request["frameIndex"] = json!(step.page_frame);
        if let Some(object) = page_request.as_object_mut() {
            object.remove("sessionId");
        }
        let ack = service.draw(&app, &surface.label, &page_request).await?;
        if ack["frameIndex"] != json!(step.page_frame)
            || ack["outputTimeUs"] != request["outputTimeUs"]
        {
            return Err("render-wrong-frame-ack".into());
        }
        let window = app
            .get_webview_window(&surface.label)
            .ok_or("render-surface-missing")?;
        let capturing = Instant::now();
        let pixels = capture::frame(&window, surface.width, surface.height).await?;
        let captured = Instant::now();
        if capture::FORMAT == capture::Format::Png && !warned_size {
            if let Some(size) =
                capture::png_size(&pixels).filter(|size| *size != (surface.width, surface.height))
            {
                warned_size = true;
                log::warn!(
                    "[render] {} captures {}x{} instead of {}x{}; FFmpeg scales it",
                    surface.viewer,
                    size.0,
                    size.1,
                    surface.width,
                    surface.height
                );
            }
        }
        let pixels = Arc::new(pixels);
        if let Some(previous) = &previous {
            for _ in 0..step.repeats {
                encoder.write(previous.clone()).await?;
            }
        }
        for _ in 0..step.copies {
            encoder.write(pixels.clone()).await?;
        }
        previous = Some(pixels);
        timing.add(
            drawing,
            capturing,
            captured,
            Instant::now(),
            step.repeats + step.copies - 1,
        );
        // The game may wait on the last frames; work in progress keeps the lease.
        service.touch();
    }
    let written = encoder.finish().await?;
    log::info!(
        "[render] {}: {} frames ({} drawn, {} repeated) in {:.1}s; page {}, capture {}, encoder wait {}, FFmpeg writes {:.1}s",
        surface.viewer,
        written.frames,
        timing.drawn,
        timing.repeated,
        started.elapsed().as_secs_f64(),
        timing.page,
        timing.capture,
        timing.queue,
        written.writing.as_secs_f64()
    );
    Ok(Video {
        viewer: surface.viewer,
        path: surface.path,
        width: surface.width,
        height: surface.height,
        frames: written.frames,
    })
}

/// Average and longest time of one stage per drawn frame.
#[derive(Default)]
struct Stage {
    total: Duration,
    longest: Duration,
    count: u32,
}

impl Stage {
    fn add(&mut self, time: Duration) {
        self.total += time;
        self.longest = self.longest.max(time);
        self.count += 1;
    }
}

impl std::fmt::Display for Stage {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let average = self.total.as_secs_f64() * 1000.0 / f64::from(self.count.max(1));
        write!(
            f,
            "{average:.2}/{:.2} ms",
            self.longest.as_secs_f64() * 1000.0
        )
    }
}

#[derive(Default)]
struct Timing {
    page: Stage,
    capture: Stage,
    queue: Stage,
    drawn: u64,
    repeated: u64,
}

impl Timing {
    fn add(
        &mut self,
        drawing: Instant,
        capturing: Instant,
        captured: Instant,
        queued: Instant,
        repeated: u64,
    ) {
        self.page.add(capturing - drawing);
        self.capture.add(captured - capturing);
        self.queue.add(queued - captured);
        self.drawn += 1;
        self.repeated += repeated;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn consecutive_frames_draw_once_each() {
        let mut plan = FramePlan::new(60);
        for frame in 0..3 {
            assert_eq!(
                plan.step(frame).unwrap(),
                Step {
                    page_frame: frame,
                    repeats: 0,
                    copies: 1
                }
            );
        }
    }

    #[test]
    fn skipped_frames_repeat_the_previous_picture() {
        let mut plan = FramePlan::new(60);
        plan.step(0).unwrap();
        assert_eq!(
            plan.step(3).unwrap(),
            Step {
                page_frame: 1,
                repeats: 2,
                copies: 1
            }
        );
        assert_eq!(
            plan.step(4).unwrap(),
            Step {
                page_frame: 2,
                repeats: 0,
                copies: 1
            }
        );
    }

    #[test]
    fn frames_before_the_first_picture_show_it() {
        let mut plan = FramePlan::new(60);
        assert_eq!(
            plan.step(2).unwrap(),
            Step {
                page_frame: 0,
                repeats: 0,
                copies: 3
            }
        );
    }

    #[test]
    fn frames_never_go_back_or_jump_far() {
        let mut plan = FramePlan::new(60);
        plan.step(5).unwrap();
        assert_eq!(plan.step(5).unwrap_err(), "render-frame-out-of-sequence");
        assert_eq!(plan.step(4).unwrap_err(), "render-frame-out-of-sequence");
        assert_eq!(
            plan.step(6 + 60 * MAX_GAP_SECONDS + 1).unwrap_err(),
            "render-frame-gap-too-large"
        );
        assert!(plan.step(6 + 60 * MAX_GAP_SECONDS).is_ok());
    }
}
