//! A render surface owns frozen data and never touches the live overlay stores.
//! Each surface draws the game's frames into its own alpha video, apart from the
//! game and from the other surface, so the game never waits for a frame.
use parking_lot::Mutex;
use serde_json::{json, Value};
use std::{
    collections::HashMap,
    path::{Path, PathBuf},
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc,
    },
    time::{Duration, Instant},
};
use tauri::{AppHandle, Emitter, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder};
use tokio::sync::{mpsc, oneshot};

pub mod capture;
mod encoder;
mod layout;
mod track;
const LABEL: &str = "dmnote-render";
type Ack = oneshot::Sender<Result<Value, String>>;

#[derive(Default)]
pub struct RenderService {
    pub live_input: Mutex<()>,
    pub input_gated: AtomicBool,
    session: Mutex<Option<Session>>,
    waiters: Mutex<HashMap<String, Ack>>,
    last_contact: Mutex<Option<Instant>>,
}

struct Session {
    id: String,
    surfaces: Vec<Surface>,
    directory: PathBuf,
    video: VideoSettings,
    tracks: Vec<Track>,
    phase: Phase,
    /// The smallest output frame the game may ask for next.
    next_frame: u64,
    /// Why a viewer stopped taking frames; finish reports it.
    failure: Option<String>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum Phase {
    Rendering,
    Finishing,
    Finished,
}

/// How the game wants the videos.
struct VideoSettings {
    fps: u32,
    ffmpeg: PathBuf,
}

#[derive(Clone)]
struct Surface {
    label: String,
    config: Arc<Value>,
}

/// One viewer's worker: the frames it still has to draw, and its FFmpeg.
struct Track {
    requests: Option<mpsc::UnboundedSender<Arc<Value>>>,
    worker: Option<tauri::async_runtime::JoinHandle<Result<track::Video, String>>>,
    encoder: encoder::Killer,
}

impl RenderService {
    pub fn touch(&self) {
        *self.last_contact.lock() = Some(Instant::now());
    }

    pub fn start_watchdog(app: AppHandle) {
        tauri::async_runtime::spawn(async move {
            loop {
                tokio::time::sleep(Duration::from_secs(1)).await;
                let service = app.state::<Self>();
                let expired = service
                    .last_contact
                    .lock()
                    .as_ref()
                    .is_some_and(|time| time.elapsed() > Duration::from_secs(15));
                // Closing the videos needs no game; the game is waiting for them.
                let finishing = service
                    .session
                    .lock()
                    .as_ref()
                    .is_some_and(|session| session.phase == Phase::Finishing);
                if service.input_gated.load(Ordering::Acquire) && expired && !finishing {
                    log::warn!("[render] IPC lease expired; releasing render input gate");
                    service.end(&app, None);
                }
            }
        });
    }

    pub async fn begin(&self, app: &AppHandle, request: Value) -> Result<Value, String> {
        let id = request["sessionId"]
            .as_str()
            .ok_or("render-invalid-session")?
            .to_string();
        if id.len() != 32 || !id.bytes().all(|byte| byte.is_ascii_hexdigit()) {
            return Err("render-invalid-session".into());
        }
        let video = video_settings(&request)?;
        let configs = layout::prepare_surfaces(&request)?;
        for config in &configs {
            for dimension in ["width", "height"] {
                config[dimension]
                    .as_u64()
                    .filter(|size| (1..=8192).contains(size))
                    .ok_or("render-invalid-size")?;
            }
        }
        if !capture::supported() {
            return Err("render-platform-unsupported".into());
        }
        let root = app
            .path()
            .app_cache_dir()
            .map_err(|e| e.to_string())?
            .join("render");
        let directory = root.join(&id);
        {
            let _input = self.live_input.lock();
            if self.input_gated.swap(true, Ordering::AcqRel) {
                return Err("render-busy".into());
            }
            app.state::<crate::state::AppState>().release_live_keys(app);
            // Reserve ownership before preparing assets so end/disconnect can cancel setup.
            *self.session.lock() = Some(Session::new(id.clone(), directory.clone(), video));
        }
        crate::services::native_viewers::notify_changed(app);
        self.touch();
        // Videos of renders that never ended, such as when the game quit mid-render.
        remove_stale_sessions(&root, &id);
        let prepare = (|| -> Result<(), String> {
            let state = app.state::<crate::state::AppState>();
            let bootstrap =
                serde_json::to_value(state.bootstrap_payload()).map_err(|e| e.to_string())?;
            let mut storage = serde_json::Map::new();
            for key in state
                .store
                .get_all_plugin_keys()
                .map_err(|e| e.to_string())?
            {
                if let Some(name) = key.strip_prefix("plugin_data_") {
                    if let Some(value) = state
                        .store
                        .get_plugin_data(&key)
                        .map_err(|e| e.to_string())?
                    {
                        storage.insert(name.into(), value);
                    }
                }
            }
            std::fs::create_dir_all(&directory).map_err(|e| e.to_string())?;
            let assets = freeze_local_assets(&bootstrap)?;
            let mut surfaces = Vec::new();
            for mut config in configs {
                let viewer = config["viewerKind"].as_str().unwrap().to_string();
                if let Some(config) = config.as_object_mut() {
                    config.remove("ffmpegPath");
                }
                config["snapshot"] = bootstrap.clone();
                config["pluginStorage"] = Value::Object(storage.clone());
                config["assetUrls"] = assets.clone();
                surfaces.push(Surface {
                    label: format!("{LABEL}-{viewer}"),
                    config: Arc::new(config),
                });
            }
            if let Err(error) = self.install_prepared_surfaces(&id, surfaces) {
                let _ = std::fs::remove_dir_all(&directory);
                return Err(error);
            }
            Ok(())
        })();
        if let Err(error) = prepare {
            self.end(app, Some(&id));
            return Err(error);
        }
        if let Err(error) = self.create_surfaces(app).await {
            self.end(app, Some(&id));
            return Err(error);
        }
        // Described as created: an explicit surface takes its page's own viewport.
        let descriptors = match self
            .describe_surfaces(&id)
            .and_then(|descriptors| self.start_tracks(app, &id).map(|()| descriptors))
        {
            Ok(descriptors) => descriptors,
            Err(error) => {
                self.end(app, Some(&id));
                return Err(error);
            }
        };
        // Include the selected viewer descriptor alongside the explicit surface list.
        let mut result = descriptors[0].clone();
        result["surfaces"] = json!(descriptors);
        result["sessionId"] = json!(id);
        Ok(result)
    }

    fn describe_surfaces(&self, id: &str) -> Result<Vec<Value>, String> {
        let sessions = self.session.lock();
        let session = sessions
            .as_ref()
            .filter(|session| session.id == id)
            .ok_or("render-session-ended")?;
        Ok(session
            .surfaces
            .iter()
            .map(|surface| {
                let config = &surface.config;
                json!({"viewerKind":config["viewerKind"], "width":config["width"],
                    "height":config["height"], "layout":config["layout"],
                    "viewport":config["viewport"]})
            })
            .collect())
    }

    /// Starts each viewer's FFmpeg and worker, now that its page has its final size.
    fn start_tracks(&self, app: &AppHandle, id: &str) -> Result<(), String> {
        let mut sessions = self.session.lock();
        let session = sessions
            .as_mut()
            .filter(|session| session.id == id)
            .ok_or("render-session-ended")?;
        let mut tracks: Vec<Track> = Vec::new();
        for surface in &session.surfaces {
            let started = (|| -> Result<Track, String> {
                let config = &surface.config;
                let viewer = config["viewerKind"]
                    .as_str()
                    .ok_or("render-invalid-viewer")?
                    .to_string();
                let size = |name: &str| {
                    config[name]
                        .as_u64()
                        .and_then(|size| u32::try_from(size).ok())
                        .ok_or("render-invalid-size")
                };
                let (width, height) = (size("width")?, size("height")?);
                let path = session.directory.join(format!("{viewer}.mov"));
                let encoder = encoder::Encoder::start(&encoder::Settings {
                    ffmpeg: &session.video.ffmpeg,
                    format: capture::FORMAT,
                    width,
                    height,
                    fps: session.video.fps,
                    output: &path,
                    log: &session.directory.join(format!("{viewer}.ffmpeg.log")),
                })?;
                let killer = encoder.killer();
                let (requests, receiver) = mpsc::unbounded_channel();
                let surface = track::Surface {
                    label: surface.label.clone(),
                    viewer,
                    width,
                    height,
                    path,
                };
                let worker = tauri::async_runtime::spawn(track::run(
                    app.clone(),
                    surface,
                    session.video.fps,
                    receiver,
                    encoder,
                ));
                Ok(Track {
                    requests: Some(requests),
                    worker: Some(worker),
                    encoder: killer,
                })
            })();
            match started {
                Ok(track) => tracks.push(track),
                Err(error) => {
                    tracks.iter().for_each(Track::stop);
                    return Err(error);
                }
            }
        }
        session.tracks = tracks;
        Ok(())
    }

    /// Sizes an explicit surface to the viewport its page laid out, the one the
    /// in-game host crops by, keeping the caller's output pixels per CSS pixel.
    fn fit_surface(
        &self,
        id: &str,
        label: &str,
        window: &WebviewWindow,
        scale: f64,
        layout: &Value,
    ) -> Result<(), String> {
        let size = |name: &str| {
            layout[name]
                .as_f64()
                .filter(|size| size.is_finite() && (1.0..=4096.0).contains(size))
        };
        let resize = {
            let mut sessions = self.session.lock();
            let session = sessions
                .as_mut()
                .filter(|session| session.id == id)
                .ok_or("render-session-ended")?;
            let surface = session
                .surfaces
                .iter_mut()
                .find(|surface| surface.label == label)
                .ok_or("render-surface-missing")?;
            let config = Arc::make_mut(&mut surface.config);
            let requested = (
                config["surfaceWidth"].as_f64().unwrap_or(1.0),
                config["surfaceHeight"].as_f64().unwrap_or(1.0),
            );
            // A page that published no layout keeps the viewport the game asked for.
            let (width, height, padding) = match (size("width"), size("height")) {
                (Some(width), Some(height)) => {
                    let padding = layout["padding"]
                        .as_f64()
                        .filter(|padding| padding.is_finite())
                        .unwrap_or(0.0)
                        .clamp(0.0, (width.min(height) - 1.0) / 2.0);
                    (width, height, padding)
                }
                _ => (requested.0, requested.1, 0.0),
            };
            let (pixels_width, pixels_height) = layout::scaled_size(width, height, scale)?;
            config["surfaceWidth"] = json!(width);
            config["surfaceHeight"] = json!(height);
            config["width"] = json!(pixels_width);
            config["height"] = json!(pixels_height);
            config["viewport"] = json!({"width":width, "height":height, "padding":padding});
            (requested != (width, height)).then_some((width, height))
        };
        if let Some((width, height)) = resize {
            window
                .set_size(tauri::LogicalSize::new(width, height))
                .map_err(|e| e.to_string())?;
        }
        Ok(())
    }

    fn install_prepared_surfaces(&self, id: &str, surfaces: Vec<Surface>) -> Result<(), String> {
        let _input = self.live_input.lock();
        let mut sessions = self.session.lock();
        let session = sessions
            .as_mut()
            .filter(|session| session.id == id)
            .ok_or("render-session-ended")?;
        if !self.input_gated.load(Ordering::Acquire) {
            return Err("render-session-ended".into());
        }
        session.surfaces = surfaces;
        Ok(())
    }

    async fn create_surfaces(&self, app: &AppHandle) -> Result<Value, String> {
        let (id, surfaces) = {
            let sessions = self.session.lock();
            let session = sessions.as_ref().ok_or("render-no-session")?;
            (session.id.clone(), session.surfaces.clone())
        };
        for surface in surfaces {
            self.require_active(&id)?;
            let config = surface.config;
            if let Some(old) = app.get_webview_window(&surface.label) {
                old.destroy().map_err(|e| e.to_string())?;
            }
            let (sender, receiver) = oneshot::channel();
            self.waiters.lock().insert(surface.label.clone(), sender);
            let window = WebviewWindowBuilder::new(
                app,
                &surface.label,
                WebviewUrl::App("render/index.html".into()),
            )
            .title("ElementNote Render")
            .inner_size(
                config["surfaceWidth"]
                    .as_f64()
                    .unwrap_or(config["width"].as_f64().unwrap()),
                config["surfaceHeight"]
                    .as_f64()
                    .unwrap_or(config["height"].as_f64().unwrap()),
            )
            .decorations(false)
            .transparent(true)
            .shadow(false)
            .skip_taskbar(true)
            .focused(false)
            .focusable(false)
            .visible(false)
            .incognito(true)
            .initialization_script(format!("window.__dmnRenderConfig={};", config.as_ref()))
            .build()
            .map_err(|e| e.to_string())?;
            // End may arrive while native construction runs. Never leave a second
            // private viewer behind after cancelling the first viewer's setup.
            if let Err(error) = self.require_active(&id) {
                let _ = window.destroy();
                return Err(error);
            }
            window
                .set_zoom(crate::compute_compensating_zoom())
                .map_err(|e| e.to_string())?;
            let ready = await_ack(receiver).await?;
            self.require_active(&id)?;
            if let Some(scale) = config["scale"].as_f64() {
                self.fit_surface(&id, &surface.label, &window, scale, &ready["layout"])?;
            }
        }
        Ok(json!({"ready":true}))
    }

    fn require_active(&self, id: &str) -> Result<(), String> {
        if self
            .session
            .lock()
            .as_ref()
            .is_some_and(|session| session.id == id)
        {
            Ok(())
        } else {
            Err("render-session-ended".into())
        }
    }

    /// Hands a frame to every viewer and returns at once; the game does not wait
    /// for it. A failed viewer is reported by finish, so later frames are ignored.
    pub fn frame(&self, request: Value) -> Result<(), String> {
        self.session
            .lock()
            .as_mut()
            .ok_or("render-no-session")?
            .queue(request)?;
        self.touch();
        Ok(())
    }

    /// Draws one frame on a surface's page and waits until the page drew it.
    async fn draw(&self, app: &AppHandle, label: &str, request: &Value) -> Result<Value, String> {
        let window = app
            .get_webview_window(label)
            .ok_or("render-surface-missing")?;
        let (sender, receiver) = oneshot::channel();
        self.waiters.lock().insert(label.to_string(), sender);
        window
            .eval(format!("void window.__dmnRenderFrame({request});"))
            .map_err(|e| e.to_string())?;
        await_ack(receiver).await
    }

    /// Waits until every frame the game asked for is in its viewer's video, then
    /// closes the videos and describes them.
    pub async fn finish(&self, request: Value) -> Result<Value, String> {
        let workers = self
            .session
            .lock()
            .as_mut()
            .ok_or("render-no-session")?
            .begin_finishing(&request)?;
        self.touch();
        let mut videos = Vec::new();
        let mut failure = None;
        for worker in workers {
            match worker
                .await
                .map_err(|e| e.to_string())
                .and_then(|video| video)
            {
                Ok(video) => videos.push(video.describe()),
                Err(error) => {
                    failure.get_or_insert(error);
                }
            }
        }
        let stopped = {
            let mut sessions = self.session.lock();
            let session = sessions
                .as_mut()
                .filter(|session| request["sessionId"].as_str() == Some(&session.id))
                .ok_or("render-session-ended")?;
            session.phase = Phase::Finished;
            session.failure.clone()
        };
        self.touch();
        if let Some(error) = failure.or(stopped) {
            log::warn!("[render] key viewer videos failed: {error}");
            return Err(error);
        }
        Ok(json!({"videos": videos}))
    }

    pub fn acknowledge(
        &self,
        window: &WebviewWindow,
        result: Value,
        error: Option<String>,
    ) -> Result<(), String> {
        if !["dmnote-render-hand", "dmnote-render-foot"].contains(&window.label()) {
            return Err("render-wrong-surface".into());
        }
        if let Some(sender) = self.waiters.lock().remove(window.label()) {
            let _ = sender.send(match error {
                Some(message) => Err(message),
                None => Ok(result),
            });
        }
        Ok(())
    }

    pub fn end(&self, app: &AppHandle, id: Option<&str>) {
        let _input = self.live_input.lock();
        let session = {
            let mut sessions = self.session.lock();
            if id.is_some_and(|id| sessions.as_ref().is_some_and(|session| session.id != id)) {
                return;
            }
            sessions.take()
        };
        if let Some(session) = &session {
            session.tracks.iter().for_each(Track::stop);
        }
        for (_, sender) in self.waiters.lock().drain() {
            let _ = sender.send(Err("render-session-ended".into()));
        }
        for label in ["dmnote-render-hand", "dmnote-render-foot"] {
            if let Some(window) = app.get_webview_window(label) {
                let _ = window.destroy();
            }
        }
        if let Some(session) = session {
            let _ = std::fs::remove_dir_all(session.directory);
        }
        app.state::<crate::state::AppState>().release_live_keys(app);
        self.input_gated.store(false, Ordering::Release);
        *self.last_contact.lock() = None;
        let _ = app.emit("render:mode", json!({"active":false}));
        crate::services::native_viewers::notify_changed(app);
    }
}

impl Session {
    fn new(id: String, directory: PathBuf, video: VideoSettings) -> Self {
        Self {
            id,
            surfaces: Vec::new(),
            directory,
            video,
            tracks: Vec::new(),
            phase: Phase::Rendering,
            next_frame: 0,
            failure: None,
        }
    }

    fn queue(&mut self, request: Value) -> Result<(), String> {
        require_id(self, &request)?;
        if self.phase != Phase::Rendering {
            return Err("render-session-finishing".into());
        }
        if self.tracks.is_empty() {
            return Err("render-not-ready".into());
        }
        if self.failure.is_some() {
            return Ok(());
        }
        let frame = request["frameIndex"]
            .as_u64()
            .ok_or("render-invalid-frame")?;
        if frame < self.next_frame {
            return Err("render-frame-out-of-sequence".into());
        }
        self.next_frame = frame + 1;
        let request = Arc::new(request);
        let stopped = self.tracks.iter().any(|track| {
            track
                .requests
                .as_ref()
                .is_none_or(|requests| requests.send(request.clone()).is_err())
        });
        if stopped {
            self.failure = Some("render-surface-failed".into());
        }
        Ok(())
    }

    /// Closes the frame queues; each worker drains its queue and its video.
    fn begin_finishing(
        &mut self,
        request: &Value,
    ) -> Result<Vec<tauri::async_runtime::JoinHandle<Result<track::Video, String>>>, String> {
        require_id(self, request)?;
        if self.phase != Phase::Rendering {
            return Err("render-session-finishing".into());
        }
        if self.tracks.is_empty() {
            return Err("render-not-ready".into());
        }
        self.phase = Phase::Finishing;
        Ok(self
            .tracks
            .iter_mut()
            .filter_map(|track| {
                track.requests = None;
                track.worker.take()
            })
            .collect())
    }
}

impl Track {
    fn stop(&self) {
        self.encoder.kill();
        if let Some(worker) = &self.worker {
            worker.abort();
        }
    }
}

fn video_settings(request: &Value) -> Result<VideoSettings, String> {
    let fps = request["fps"]
        .as_u64()
        .filter(|fps| (1..=1000).contains(fps))
        .ok_or("render-invalid-fps")? as u32;
    let ffmpeg = request["ffmpegPath"]
        .as_str()
        .map(PathBuf::from)
        .filter(|path| path.is_absolute() && path.is_file())
        .ok_or("render-ffmpeg-unavailable")?;
    Ok(VideoSettings { fps, ffmpeg })
}

fn remove_stale_sessions(root: &Path, current: &str) {
    let Ok(entries) = std::fs::read_dir(root) else {
        return;
    };
    for entry in entries.flatten() {
        if entry.file_name() != current {
            let _ = std::fs::remove_dir_all(entry.path());
        }
    }
}

fn require_id(session: &Session, request: &Value) -> Result<(), String> {
    if request["sessionId"].as_str() != Some(&session.id) {
        return Err("render-wrong-session".into());
    }
    Ok(())
}

async fn await_ack(receiver: oneshot::Receiver<Result<Value, String>>) -> Result<Value, String> {
    tokio::time::timeout(Duration::from_secs(20), receiver)
        .await
        .map_err(|_| "render-frame-timeout".to_string())?
        .map_err(|_| "render-surface-closed".to_string())?
}

fn freeze_local_assets(snapshot: &Value) -> Result<Value, String> {
    use base64::Engine;
    fn visit(value: &Value, paths: &mut Vec<String>) {
        match value {
            Value::String(text)
                if std::path::Path::new(text).is_absolute()
                    && std::path::Path::new(text).is_file() =>
            {
                paths.push(text.clone())
            }
            Value::Array(values) => values.iter().for_each(|value| visit(value, paths)),
            Value::Object(values) => values.values().for_each(|value| visit(value, paths)),
            _ => {}
        }
    }
    let mut paths = Vec::new();
    visit(snapshot, &mut paths);
    paths.sort();
    paths.dedup();
    let mut assets = HashMap::new();
    let mut total = 0;
    for path in paths {
        let data =
            std::fs::read(&path).map_err(|e| format!("render-asset-missing: {path}: {e}"))?;
        total += data.len();
        if total > 256 * 1024 * 1024 {
            return Err("render-assets-too-large".into());
        }
        let mime = match std::path::Path::new(&path)
            .extension()
            .and_then(|ext| ext.to_str())
            .unwrap_or("")
            .to_ascii_lowercase()
            .as_str()
        {
            "png" => "image/png",
            "jpg" | "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "svg" => "image/svg+xml",
            "woff" => "font/woff",
            "woff2" => "font/woff2",
            "ttf" => "font/ttf",
            "otf" => "font/otf",
            _ => "application/octet-stream",
        };
        assets.insert(
            path,
            format!(
                "data:{mime};base64,{}",
                base64::engine::general_purpose::STANDARD.encode(data)
            ),
        );
    }
    serde_json::to_value(assets).map_err(|e| e.to_string())
}

#[cfg(test)]
mod session_tests {
    use super::*;
    fn reserved(id: &str) -> Session {
        Session::new(
            id.into(),
            PathBuf::new(),
            VideoSettings {
                fps: 60,
                ffmpeg: PathBuf::new(),
            },
        )
    }
    fn frame(id: &str, index: u64) -> Value {
        json!({"sessionId": id, "frameIndex": index, "outputTimeUs": index * 1000, "events": []})
    }
    /// A session whose one viewer's frames land in the returned queue.
    fn rendering(id: &str) -> (Session, mpsc::UnboundedReceiver<Arc<Value>>) {
        let mut session = reserved(id);
        let (requests, receiver) = mpsc::unbounded_channel();
        session.tracks.push(Track {
            requests: Some(requests),
            worker: None,
            encoder: encoder::Killer::detached(),
        });
        (session, receiver)
    }
    #[test]
    fn canceled_setup_cannot_restore_session_or_input_gate() {
        let service = RenderService::default();
        *service.session.lock() = Some(reserved("old"));
        service.input_gated.store(true, Ordering::Release);
        // End has cleared the reservation before asset preparation completes.
        service.session.lock().take();
        service.input_gated.store(false, Ordering::Release);
        assert_eq!(
            service
                .install_prepared_surfaces("old", Vec::new())
                .unwrap_err(),
            "render-session-ended"
        );
        assert!(service.session.lock().is_none());
        assert!(!service.input_gated.load(Ordering::Acquire));
    }
    #[test]
    fn late_setup_cannot_replace_a_new_session() {
        let service = RenderService::default();
        *service.session.lock() = Some(reserved("new"));
        service.input_gated.store(true, Ordering::Release);
        assert!(service
            .install_prepared_surfaces("old", Vec::new())
            .is_err());
        assert_eq!(service.session.lock().as_ref().unwrap().id, "new");
        assert!(service.install_prepared_surfaces("new", Vec::new()).is_ok());
    }
    #[test]
    fn frames_reach_every_viewer_in_order_and_never_go_back() {
        let (mut session, mut queue) = rendering("a");
        assert_eq!(
            reserved("a").queue(frame("a", 0)).unwrap_err(),
            "render-not-ready"
        );
        session.queue(frame("a", 0)).unwrap();
        session.queue(frame("a", 2)).unwrap();
        assert_eq!(
            session.queue(frame("a", 2)).unwrap_err(),
            "render-frame-out-of-sequence"
        );
        assert_eq!(
            session.queue(frame("b", 3)).unwrap_err(),
            "render-wrong-session"
        );
        assert_eq!(queue.try_recv().unwrap()["frameIndex"], 0);
        assert_eq!(queue.try_recv().unwrap()["frameIndex"], 2);
        assert!(queue.try_recv().is_err());
    }
    #[test]
    fn a_stopped_viewer_fails_the_render_quietly() {
        let (mut session, queue) = rendering("a");
        drop(queue);
        session.queue(frame("a", 0)).unwrap();
        assert_eq!(session.failure.as_deref(), Some("render-surface-failed"));
        // Later frames are ignored; finish reports the failure.
        session.queue(frame("a", 1)).unwrap();
        assert_eq!(session.next_frame, 1);
    }
    #[test]
    fn finishing_closes_the_queues_and_refuses_more_frames() {
        let (mut session, mut queue) = rendering("a");
        session.queue(frame("a", 0)).unwrap();
        assert!(session
            .begin_finishing(&json!({"sessionId": "a"}))
            .unwrap()
            .is_empty());
        assert_eq!(session.phase, Phase::Finishing);
        assert_eq!(queue.try_recv().unwrap()["frameIndex"], 0);
        assert!(matches!(
            queue.try_recv(),
            Err(mpsc::error::TryRecvError::Disconnected)
        ));
        assert_eq!(
            session.queue(frame("a", 1)).unwrap_err(),
            "render-session-finishing"
        );
        assert_eq!(
            session
                .begin_finishing(&json!({"sessionId": "a"}))
                .unwrap_err(),
            "render-session-finishing"
        );
    }
    #[test]
    fn video_settings_need_a_rate_and_an_existing_ffmpeg() {
        let ffmpeg = std::env::current_exe().unwrap();
        let ffmpeg = ffmpeg.to_str().unwrap();
        assert!(video_settings(&json!({"fps": 60, "ffmpegPath": ffmpeg})).is_ok());
        assert_eq!(
            video_settings(&json!({"ffmpegPath": ffmpeg}))
                .err()
                .unwrap(),
            "render-invalid-fps"
        );
        assert_eq!(
            video_settings(&json!({"fps": 0, "ffmpegPath": ffmpeg}))
                .err()
                .unwrap(),
            "render-invalid-fps"
        );
        for path in [json!(null), json!("ffmpeg"), json!("/nonexistent/ffmpeg")] {
            assert_eq!(
                video_settings(&json!({"fps": 60, "ffmpegPath": path}))
                    .err()
                    .unwrap(),
                "render-ffmpeg-unavailable"
            );
        }
    }
}
