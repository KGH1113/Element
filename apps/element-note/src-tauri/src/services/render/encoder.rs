//! One FFmpeg process per render surface, turning its frames into an alpha video.
use super::capture::Format;
use parking_lot::Mutex;
use std::{
    ffi::OsString,
    io::Write,
    path::{Path, PathBuf},
    process::{Child, ChildStdin, Command, ExitStatus, Stdio},
    sync::Arc,
    thread::JoinHandle,
    time::{Duration, Instant},
};
use tokio::sync::mpsc;

/// Frames waiting for FFmpeg. Small, so capture and encoding overlap without
/// holding many raw frames in memory.
const QUEUE: usize = 4;
const LOG_TAIL: usize = 600;

pub struct Settings<'a> {
    pub ffmpeg: &'a Path,
    pub format: Format,
    pub width: u32,
    pub height: u32,
    pub fps: u32,
    pub output: &'a Path,
    pub log: &'a Path,
}

/// FFmpeg's arguments. QuickTime Animation keeps alpha losslessly and stores a
/// mostly still key viewer in very little space, and it encodes fast.
pub fn arguments(settings: &Settings) -> Vec<OsString> {
    let size = format!("{}x{}", settings.width, settings.height);
    let fps = settings.fps.to_string();
    let mut arguments: Vec<String> = ["-hide_banner", "-loglevel", "error", "-nostdin", "-y"]
        .map(String::from)
        .to_vec();
    match settings.format {
        Format::Rgba => arguments.extend(
            [
                "-f",
                "rawvideo",
                "-pix_fmt",
                "rgba",
                "-s",
                &size,
                "-framerate",
                &fps,
                "-i",
                "pipe:0",
            ]
            .map(String::from),
        ),
        Format::Png => {
            arguments.extend(
                [
                    "-f",
                    "image2pipe",
                    "-framerate",
                    &fps,
                    "-c:v",
                    "png",
                    "-i",
                    "pipe:0",
                ]
                .map(String::from),
            );
            // A PNG carries its own size; the video keeps the size the game crops by.
            arguments.extend([
                "-vf".into(),
                format!("scale={}:{}", settings.width, settings.height),
            ]);
        }
    }
    arguments.extend(["-an", "-c:v", "qtrle", "-pix_fmt", "argb"].map(String::from));
    let mut arguments: Vec<OsString> = arguments.into_iter().map(OsString::from).collect();
    arguments.push(settings.output.as_os_str().to_owned());
    arguments
}

/// What the encoder did, for the render's timing log.
#[derive(Default, Clone, Copy)]
pub struct Written {
    pub frames: u64,
    pub writing: Duration,
}

/// Kills the FFmpeg process from another thread, such as when a render ends early.
#[derive(Clone)]
pub struct Killer(Arc<Mutex<Option<Child>>>);

impl Killer {
    pub fn kill(&self) {
        if let Some(child) = self.0.lock().as_mut() {
            if matches!(child.try_wait(), Ok(None)) {
                let _ = child.kill();
            }
        }
    }

    /// A worker without an FFmpeg process, for tests of the session.
    #[cfg(test)]
    pub fn detached() -> Self {
        Self(Arc::new(Mutex::new(None)))
    }
}

pub struct Encoder {
    frames: Option<mpsc::Sender<Arc<Vec<u8>>>>,
    writer: Option<JoinHandle<Result<Written, String>>>,
    child: Killer,
    log: PathBuf,
}

impl Encoder {
    pub fn start(settings: &Settings) -> Result<Self, String> {
        let log = std::fs::File::create(settings.log).map_err(|e| e.to_string())?;
        let mut command = Command::new(settings.ffmpeg);
        command
            .args(arguments(settings))
            .stdin(Stdio::piped())
            .stdout(Stdio::null())
            .stderr(Stdio::from(log));
        #[cfg(target_os = "windows")]
        {
            use std::os::windows::process::CommandExt;
            const CREATE_NO_WINDOW: u32 = 0x0800_0000;
            command.creation_flags(CREATE_NO_WINDOW);
        }
        let mut child = command
            .spawn()
            .map_err(|e| format!("render-ffmpeg-unavailable: {e}"))?;
        let stdin = child.stdin.take().ok_or("render-ffmpeg-unavailable")?;
        let child = Killer(Arc::new(Mutex::new(Some(child))));
        let (frames, receiver) = mpsc::channel(QUEUE);
        let log = settings.log.to_path_buf();
        let writer = {
            let child = child.clone();
            let log = log.clone();
            std::thread::Builder::new()
                .name("render-encoder".into())
                .spawn(move || write(stdin, receiver, &child, &log))
                .map_err(|e| e.to_string())?
        };
        Ok(Self {
            frames: Some(frames),
            writer: Some(writer),
            child,
            log,
        })
    }

    pub fn killer(&self) -> Killer {
        self.child.clone()
    }

    /// Queues a frame; waits only while FFmpeg is behind.
    pub async fn write(&mut self, frame: Arc<Vec<u8>>) -> Result<(), String> {
        let sent = match &self.frames {
            Some(frames) => frames.send(frame).await.is_ok(),
            None => false,
        };
        if sent {
            return Ok(());
        }
        // The writer stopped, so FFmpeg failed: report its reason.
        Err(self
            .close()
            .await
            .err()
            .unwrap_or_else(|| "render-ffmpeg-stopped".into()))
    }

    /// Ends the video: every queued frame is written and FFmpeg has exited.
    pub async fn finish(mut self) -> Result<Written, String> {
        self.close().await
    }

    async fn close(&mut self) -> Result<Written, String> {
        self.frames = None;
        let Some(writer) = self.writer.take() else {
            return Err("render-ffmpeg-stopped".into());
        };
        let log = self.log.clone();
        tauri::async_runtime::spawn_blocking(move || {
            writer
                .join()
                .unwrap_or_else(|_| Err(failure("the writer panicked", &log)))
        })
        .await
        .map_err(|e| e.to_string())?
    }
}

impl Drop for Encoder {
    fn drop(&mut self) {
        // Unfinished: the render ended early, so the video is not needed.
        if self.writer.is_some() {
            self.child.kill();
        }
    }
}

fn write(
    mut stdin: ChildStdin,
    mut frames: mpsc::Receiver<Arc<Vec<u8>>>,
    child: &Killer,
    log: &Path,
) -> Result<Written, String> {
    let mut written = Written::default();
    while let Some(frame) = frames.blocking_recv() {
        let started = Instant::now();
        if let Err(error) = stdin.write_all(&frame) {
            drop(frames);
            drop(stdin);
            let _ = wait(child);
            return Err(failure(&error.to_string(), log));
        }
        written.writing += started.elapsed();
        written.frames += 1;
    }
    drop(stdin);
    let status = wait(child).map_err(|e| failure(&e.to_string(), log))?;
    if status.success() {
        Ok(written)
    } else {
        Err(failure(&status.to_string(), log))
    }
}

// Polls, so a kill from another thread is never blocked by a waiting lock.
fn wait(child: &Killer) -> std::io::Result<ExitStatus> {
    loop {
        let status = match child.0.lock().as_mut() {
            Some(child) => child.try_wait()?,
            None => return Err(std::io::Error::other("no FFmpeg process")),
        };
        if let Some(status) = status {
            return Ok(status);
        }
        std::thread::sleep(Duration::from_millis(5));
    }
}

fn failure(reason: &str, log: &Path) -> String {
    let output = std::fs::read_to_string(log).unwrap_or_default();
    let output = output.trim();
    let start = output
        .char_indices()
        .rev()
        .nth(LOG_TAIL)
        .map_or(0, |(index, _)| index);
    format!("render-ffmpeg-failed: {reason}: {}", &output[start..])
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settings<'a>(format: Format, output: &'a Path) -> Settings<'a> {
        Settings {
            ffmpeg: Path::new("ffmpeg"),
            format,
            width: 645,
            height: 240,
            fps: 60,
            output,
            log: Path::new("hand.log"),
        }
    }

    fn joined(format: Format) -> String {
        arguments(&settings(format, Path::new("/tmp/hand.mov")))
            .iter()
            .map(|argument| argument.to_string_lossy().into_owned())
            .collect::<Vec<_>>()
            .join(" ")
    }

    #[test]
    fn raw_frames_are_read_at_their_size_and_rate() {
        assert_eq!(
            joined(Format::Rgba),
            "-hide_banner -loglevel error -nostdin -y -f rawvideo -pix_fmt rgba -s 645x240 \
             -framerate 60 -i pipe:0 -an -c:v qtrle -pix_fmt argb /tmp/hand.mov"
        );
    }

    #[test]
    fn png_frames_are_kept_at_the_size_the_game_crops_by() {
        assert_eq!(
            joined(Format::Png),
            "-hide_banner -loglevel error -nostdin -y -f image2pipe -framerate 60 -c:v png \
             -i pipe:0 -vf scale=645:240 -an -c:v qtrle -pix_fmt argb /tmp/hand.mov"
        );
    }

    #[test]
    fn a_failure_names_the_end_of_the_log() {
        let directory = std::env::temp_dir().join(format!("render-log-{}", std::process::id()));
        std::fs::create_dir_all(&directory).unwrap();
        let log = directory.join("hand.log");
        std::fs::write(&log, format!("{}\nlast line\n", "x".repeat(2000))).unwrap();
        let message = failure("exit status: 1", &log);
        assert!(message.starts_with("render-ffmpeg-failed: exit status: 1: "));
        assert!(message.ends_with("last line"));
        assert!(message.len() < 700);
        std::fs::remove_dir_all(directory).unwrap();
    }

    /// Feeds real frames through FFmpeg. Set ELEMENT_NOTE_TEST_FFMPEG to an
    /// FFmpeg with the qtrle encoder (and an ffprobe next to it or on PATH).
    #[test]
    fn real_ffmpeg_keeps_every_frame_and_alpha() {
        let Some(ffmpeg) = std::env::var_os("ELEMENT_NOTE_TEST_FFMPEG") else {
            return;
        };
        let directory = std::env::temp_dir().join(format!("render-encode-{}", std::process::id()));
        std::fs::create_dir_all(&directory).unwrap();
        let (output, log) = (directory.join("hand.mov"), directory.join("hand.log"));
        let (width, height) = (16u32, 8u32);
        let settings = Settings {
            ffmpeg: Path::new(&ffmpeg),
            format: Format::Rgba,
            width,
            height,
            fps: 60,
            output: &output,
            log: &log,
        };
        let runtime = tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap();
        let written = runtime.block_on(async {
            let mut encoder = Encoder::start(&settings).unwrap();
            for frame in 0..30u8 {
                // Left half: half-transparent red; right half: clear.
                let mut pixels = vec![0u8; (width * height * 4) as usize];
                for (index, pixel) in pixels.chunks_mut(4).enumerate() {
                    if (index as u32 % width) < width / 2 {
                        pixel.copy_from_slice(&[255, frame, 0, 128]);
                    }
                }
                encoder.write(Arc::new(pixels)).await.unwrap();
            }
            encoder.finish().await.unwrap()
        });
        assert_eq!(written.frames, 30);
        let decoded = Command::new(&ffmpeg)
            .args(["-v", "error", "-i"])
            .arg(&output)
            .args(["-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1"])
            .output()
            .unwrap();
        assert!(
            decoded.status.success(),
            "{}",
            String::from_utf8_lossy(&decoded.stderr)
        );
        let frame = (width * height * 4) as usize;
        assert_eq!(
            decoded.stdout.len(),
            frame * 30,
            "every frame is in the video"
        );
        let last = &decoded.stdout[frame * 29..];
        assert_eq!(&last[..4], &[255, 29, 0, 128], "color and alpha survive");
        assert_eq!(&last[frame - 4..], &[0, 0, 0, 0], "clear pixels stay clear");
        std::fs::remove_dir_all(directory).unwrap();
    }

    #[test]
    fn a_missing_ffmpeg_is_reported_at_start() {
        let directory = std::env::temp_dir().join(format!("render-missing-{}", std::process::id()));
        std::fs::create_dir_all(&directory).unwrap();
        let (output, log) = (directory.join("hand.mov"), directory.join("hand.log"));
        let mut settings = settings(Format::Rgba, &output);
        settings.ffmpeg = Path::new("/nonexistent/ffmpeg");
        settings.log = &log;
        let error = Encoder::start(&settings).err().unwrap();
        assert!(error.starts_with("render-ffmpeg-unavailable"), "{error}");
        std::fs::remove_dir_all(directory).unwrap();
    }
}
