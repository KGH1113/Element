//! A render surface owns frozen data and never touches the live overlay stores.
use parking_lot::Mutex;
use serde_json::{json, Value};
use std::{
    collections::HashMap,
    path::PathBuf,
    sync::atomic::{AtomicBool, Ordering},
    time::{Duration, Instant},
};
use tauri::{AppHandle, Emitter, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder};
use tokio::sync::oneshot;

pub mod capture;
mod layout;
const LABEL: &str = "dmnote-render";
type Ack = oneshot::Sender<Result<Value, String>>;

#[derive(Default)]
pub struct RenderService {
    pub live_input: Mutex<()>,
    pub input_gated: AtomicBool,
    session: Mutex<Option<Session>>,
    waiter: Mutex<Option<Ack>>,
    last_contact: Mutex<Option<Instant>>,
}

struct Session {
    id: String,
    config: Value,
    directory: PathBuf,
    next_frame: u64,
    rgba: bool,
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
                if service.input_gated.load(Ordering::Acquire) && expired {
                    log::warn!("[render] IPC lease expired; releasing render input gate");
                    service.end(&app, None);
                }
            }
        });
    }

    pub async fn begin(&self, app: &AppHandle, mut request: Value) -> Result<Value, String> {
        let id = request["sessionId"]
            .as_str()
            .ok_or("render-invalid-session")?
            .to_string();
        if id.len() != 32 || !id.bytes().all(|byte| byte.is_ascii_hexdigit()) {
            return Err("render-invalid-session".into());
        }
        let placement = layout::prepare(app, &mut request)?;
        let rgba = cfg!(target_os = "macos") && request["frameFormat"].as_str() == Some("rgba");
        let width = request["width"]
            .as_u64()
            .filter(|size| (1..=8192).contains(size))
            .ok_or("render-invalid-size")?;
        let height = request["height"]
            .as_u64()
            .filter(|size| (1..=8192).contains(size))
            .ok_or("render-invalid-size")?;
        let viewer = request["viewerKind"].as_str().unwrap_or("hand");
        if !["hand", "foot"].contains(&viewer) {
            return Err("render-invalid-viewer".into());
        }
        if !capture::supported() {
            return Err("render-platform-unsupported".into());
        }
        {
            let _input = self.live_input.lock();
            if self.input_gated.swap(true, Ordering::AcqRel) {
                return Err("render-busy".into());
            }
            app.state::<crate::state::AppState>().release_live_keys(app);
        }
        self.touch();
        let prepare = (|| -> Result<Value, String> {
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
            let directory = app
                .path()
                .app_cache_dir()
                .map_err(|e| e.to_string())?
                .join("render")
                .join(&id);
            std::fs::create_dir_all(&directory).map_err(|e| e.to_string())?;
            let mut config = request.clone();
            config["snapshot"] = bootstrap;
            config["pluginStorage"] = Value::Object(storage);
            config["assetUrls"] = freeze_local_assets(&config["snapshot"])?;
            *self.session.lock() = Some(Session {
                id: id.clone(),
                config: config.clone(),
                directory: directory.clone(),
                next_frame: 0,
                rgba,
            });
            Ok(json!({"width":width, "height":height, "layout":placement, "frameFormat":if rgba { "rgba" } else { "png" }, "frameDirectory":directory, "sessionId":id}))
        })();
        let result = match prepare {
            Ok(value) => value,
            Err(error) => {
                self.end(app, Some(&id));
                return Err(error);
            }
        };
        if let Err(error) = self.create_surface(app).await {
            self.end(app, Some(&id));
            return Err(error);
        }
        Ok(result)
    }

    async fn create_surface(&self, app: &AppHandle) -> Result<Value, String> {
        let config = self
            .session
            .lock()
            .as_ref()
            .ok_or("render-no-session")?
            .config
            .clone();
        if let Some(old) = app.get_webview_window(LABEL) {
            old.destroy().map_err(|e| e.to_string())?;
        }
        let (sender, receiver) = oneshot::channel();
        *self.waiter.lock() = Some(sender);
        let window =
            WebviewWindowBuilder::new(app, LABEL, WebviewUrl::App("render/index.html".into()))
                .title("ImplDmNote Render")
                .inner_size(
                    config["surfaceWidth"].as_f64().unwrap_or(config["width"].as_f64().unwrap()),
                    config["surfaceHeight"].as_f64().unwrap_or(config["height"].as_f64().unwrap()),
                )
                .decorations(false)
                .transparent(true)
                .shadow(false)
                .skip_taskbar(true)
                .focused(false)
                .focusable(false)
                .visible(false)
                .incognito(true)
                .initialization_script(format!("window.__dmnRenderConfig={config};"))
                .build()
                .map_err(|e| e.to_string())?;
        window
            .set_zoom(crate::compute_compensating_zoom())
            .map_err(|e| e.to_string())?;
        await_ack(receiver).await
    }

    pub async fn reset(&self, app: &AppHandle, request: Value) -> Result<Value, String> {
        {
            let mut sessions = self.session.lock();
            let session = sessions.as_mut().ok_or("render-no-session")?;
            require_id(session, &request)?;
            session.config["initialOutputTimeUs"] = request["initialOutputTimeUs"].clone();
            session.next_frame = 0;
        }
        self.touch();
        self.create_surface(app).await
    }

    pub async fn frame(&self, app: &AppHandle, request: Value) -> Result<Value, String> {
        let index = request["frameIndex"]
            .as_u64()
            .ok_or("render-invalid-frame")?;
        let directory;
        let (width, height, rgba);
        {
            let sessions = self.session.lock();
            let session = sessions.as_ref().ok_or("render-no-session")?;
            require_id(session, &request)?;
            if index != session.next_frame {
                return Err("render-frame-out-of-sequence".into());
            }
            directory = session.directory.clone();
            width = session.config["width"].as_u64().unwrap() as u32;
            height = session.config["height"].as_u64().unwrap() as u32;
            rgba = session.rgba;
        }
        self.touch();
        let window = app
            .get_webview_window(LABEL)
            .ok_or("render-surface-missing")?;
        let (sender, receiver) = oneshot::channel();
        *self.waiter.lock() = Some(sender);
        window
            .eval(format!("void window.__dmnRenderFrame({request});"))
            .map_err(|e| e.to_string())?;
        let mut result = await_ack(receiver).await?;
        let pixels = capture::frame(&window, width, height, rgba).await?;
        let path = directory.join(if rgba { "frame.rgba" } else { "frame.png" });
        let temporary = directory.join("frame.partial");
        std::fs::write(&temporary, pixels).map_err(|e| e.to_string())?;
        if path.exists() {
            std::fs::remove_file(&path).map_err(|e| e.to_string())?;
        }
        std::fs::rename(temporary, &path).map_err(|e| e.to_string())?;
        {
            let mut sessions = self.session.lock();
            let session = sessions.as_mut().ok_or("render-no-session")?;
            require_id(session, &request)?;
            session.next_frame += 1;
        }
        result["framePath"] = json!(path);
        Ok(result)
    }

    pub fn acknowledge(
        &self,
        window: &WebviewWindow,
        result: Value,
        error: Option<String>,
    ) -> Result<(), String> {
        if window.label() != LABEL {
            return Err("render-wrong-surface".into());
        }
        if let Some(sender) = self.waiter.lock().take() {
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
        if let Some(sender) = self.waiter.lock().take() {
            let _ = sender.send(Err("render-session-ended".into()));
        }
        if let Some(window) = app.get_webview_window(LABEL) {
            let _ = window.destroy();
        }
        if let Some(session) = session {
            let _ = std::fs::remove_dir_all(session.directory);
        }
        app.state::<crate::state::AppState>().release_live_keys(app);
        self.input_gated.store(false, Ordering::Release);
        *self.last_contact.lock() = None;
        let _ = app.emit("render:mode", json!({"active":false}));
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
