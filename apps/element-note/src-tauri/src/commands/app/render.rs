use crate::services::render::RenderService;
use serde_json::{json, Value};
use tauri::{AppHandle, State, WebviewWindow};

#[tauri::command]
pub fn dmnote_render_touch(service: State<'_, RenderService>) {
    service.touch();
}

#[tauri::command]
pub async fn dmnote_render_begin(
    app: AppHandle,
    service: State<'_, RenderService>,
    request: Value,
) -> Result<Value, String> {
    service.begin(&app, request).await
}

#[tauri::command]
pub async fn dmnote_render_reset(
    app: AppHandle,
    service: State<'_, RenderService>,
    request: Value,
) -> Result<Value, String> {
    service.reset(&app, request).await
}

#[tauri::command]
pub async fn dmnote_render_frame(
    app: AppHandle,
    service: State<'_, RenderService>,
    request: Value,
) -> Result<Value, String> {
    service.frame(&app, request).await
}

#[tauri::command]
pub fn dmnote_render_end(
    app: AppHandle,
    service: State<'_, RenderService>,
    session_id: Option<String>,
) -> Value {
    service.end(&app, session_id.as_deref());
    json!({"ended":true})
}

#[tauri::command]
pub fn dmnote_render_ack(
    window: WebviewWindow,
    service: State<'_, RenderService>,
    result: Value,
    error: Option<String>,
) -> Result<(), String> {
    service.acknowledge(&window, result, error)
}
