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
    let mut progress = Progress::new(started);
    let mut previous: Option<Arc<Vec<u8>>> = None;
    let mut warned_size = false;
    loop {
        let waiting = Instant::now();
        let Some(request) = requests.recv().await else {
            break;
        };
        timing.idle += waiting.elapsed();
        timing.backlog = timing.backlog.max(requests.len());
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
        let capture::Frame {
            pixels,
            dispatched,
            returned,
        } = capture::frame(&window, surface.width, surface.height).await?;
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
        let bytes = pixels.len();
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
        timing.add(Sample {
            forwarded: milliseconds(&request["mainWindowWaitMs"]),
            script: milliseconds(&ack["timing"]["scriptMs"]),
            paint: milliseconds(&ack["timing"]["paintMs"]),
            drawing,
            capturing,
            dispatched,
            returned,
            captured,
            written: Instant::now(),
            bytes,
            repeated: step.repeats + step.copies - 1,
        });
        progress.report(&surface.viewer, timing.drawn, requests.len());
        // The game may wait on the last frames; work in progress keeps the lease.
        service.touch();
    }
    let written = encoder.finish().await?;
    log::info!(
        "[render] {}: {} frames ({} drawn, {} repeated) in {:.1}s, idle {:.1}s waiting for the game; at most {} frames queued here\n{}  FFmpeg writes   : {:.1}s",
        surface.viewer,
        written.frames,
        timing.drawn,
        timing.repeated,
        started.elapsed().as_secs_f64(),
        timing.idle.as_secs_f64(),
        timing.backlog,
        timing,
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

/// How often a viewer logs how far it got while the game renders.
const PROGRESS_INTERVAL: Duration = Duration::from_secs(5);

/// A duration in milliseconds from a page's timing, or zero when it sent none.
fn milliseconds(value: &Value) -> Duration {
    value
        .as_f64()
        .filter(|ms| ms.is_finite() && *ms >= 0.0)
        .map_or(Duration::ZERO, |ms| Duration::from_secs_f64(ms / 1000.0))
}

/// One stage's time per drawn frame, kept whole for its percentiles.
#[derive(Default)]
struct Stage {
    micros: Vec<u32>,
}

impl Stage {
    fn add(&mut self, time: Duration) {
        self.micros
            .push(u32::try_from(time.as_micros()).unwrap_or(u32::MAX));
    }
}

impl std::fmt::Display for Stage {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let mut sorted = self.micros.clone();
        sorted.sort_unstable();
        let Some(&longest) = sorted.last() else {
            return write!(f, "-");
        };
        let ms = |micros: u32| f64::from(micros) / 1000.0;
        let at = |share: f64| ms(sorted[((sorted.len() - 1) as f64 * share).round() as usize]);
        let total: u64 = sorted.iter().map(|&micros| u64::from(micros)).sum();
        write!(
            f,
            "avg {:.2}, p50 {:.2}, p95 {:.2}, p99 {:.2}, max {:.2} ms",
            total as f64 / 1000.0 / sorted.len() as f64,
            at(0.5),
            at(0.95),
            at(0.99),
            ms(longest)
        )
    }
}

/// When one drawn frame passed each step.
struct Sample {
    /// Waited in the main window behind earlier frames before reaching here.
    forwarded: Duration,
    /// The page's own time: applying the keys, then letting it paint.
    script: Duration,
    paint: Duration,
    drawing: Instant,
    capturing: Instant,
    dispatched: Instant,
    returned: Instant,
    captured: Instant,
    written: Instant,
    bytes: usize,
    repeated: u64,
}

/// Where a viewer's time went, stage by stage, for the log at the end.
#[derive(Default)]
struct Timing {
    forward: Stage,
    script: Stage,
    paint: Stage,
    /// The page's time minus its own work: the eval and the ack crossing over.
    round_trip: Stage,
    dispatch: Stage,
    webview: Stage,
    read: Stage,
    encoder: Stage,
    bytes: u64,
    drawn: u64,
    repeated: u64,
    /// Waiting for the game's next frame.
    idle: Duration,
    /// Most frames queued for this viewer at once.
    backlog: usize,
}

impl Timing {
    fn add(&mut self, sample: Sample) {
        let page = sample.capturing - sample.drawing;
        self.forward.add(sample.forwarded);
        self.script.add(sample.script);
        self.paint.add(sample.paint);
        self.round_trip
            .add(page.saturating_sub(sample.script + sample.paint));
        self.dispatch.add(sample.dispatched - sample.capturing);
        self.webview.add(sample.returned - sample.dispatched);
        self.read.add(sample.captured - sample.returned);
        self.encoder.add(sample.written - sample.captured);
        self.bytes += sample.bytes as u64;
        self.drawn += 1;
        self.repeated += sample.repeated;
    }
}

impl std::fmt::Display for Timing {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let rows = [
            ("main window wait", &self.forward),
            ("page script", &self.script),
            ("page paint", &self.paint),
            ("page round trip", &self.round_trip),
            ("capture dispatch", &self.dispatch),
            ("capture webview", &self.webview),
            ("capture read", &self.read),
            ("encoder wait", &self.encoder),
        ];
        for (name, stage) in rows {
            writeln!(f, "  {name:<16}: {stage}")?;
        }
        writeln!(
            f,
            "  {:<16}: {:.1} KB",
            "captured image",
            self.bytes as f64 / 1024.0 / self.drawn.max(1) as f64
        )
    }
}

/// Logs a viewer's pace now and then, to show whether it keeps up with the game.
struct Progress {
    last: Instant,
    drawn: u64,
}

impl Progress {
    fn new(started: Instant) -> Self {
        Self {
            last: started,
            drawn: 0,
        }
    }

    fn report(&mut self, viewer: &str, drawn: u64, queued: usize) {
        let elapsed = self.last.elapsed();
        if elapsed < PROGRESS_INTERVAL {
            return;
        }
        log::info!(
            "[render] {viewer}: {drawn} drawn, {:.1} frames/s over the last {:.1}s, {queued} queued",
            (drawn - self.drawn) as f64 / elapsed.as_secs_f64(),
            elapsed.as_secs_f64()
        );
        self.last = Instant::now();
        self.drawn = drawn;
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
