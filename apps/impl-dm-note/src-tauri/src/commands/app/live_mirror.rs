use crate::services::live_mirror::LiveMirrorService;
use serde_json::{json, Value};
use tauri::{AppHandle, State};

#[tauri::command]
pub async fn dmnote_mirror_frame(
    app: AppHandle,
    service: State<'_, LiveMirrorService>,
    session_id: String,
) -> Result<Value, String> {
    service.frame(&app, &session_id).await
}

#[tauri::command]
pub async fn dmnote_mirror_end(
    service: State<'_, LiveMirrorService>,
    session_id: Option<String>,
) -> Result<Value, String> {
    service.end(session_id.as_deref()).await;
    Ok(json!({ "ended": true }))
}
