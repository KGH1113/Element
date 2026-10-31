use crate::services::native_viewers::{self, NativeViewerError};
use serde_json::Value;
use tauri::AppHandle;

#[tauri::command]
pub async fn dmnote_native_describe(
    app: AppHandle,
    session_id: String,
) -> Result<Value, NativeViewerError> {
    native_viewers::describe(&app, &session_id)
}
