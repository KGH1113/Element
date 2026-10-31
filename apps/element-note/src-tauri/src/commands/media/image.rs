use rfd::FileDialog;
use serde::Serialize;
use std::fs;
use std::path::{Path, PathBuf};
use tauri::Manager;
use uuid::Uuid;

use crate::errors::CmdResult;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ImageLoadResponse {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub image_path: Option<String>,
}

/// 로컬 이미지 파일을 선택해서 앱 데이터 디렉토리로 복사한 뒤 경로를 반환합니다.
/// 저장소에는 base64 대신 파일 경로만 저장해 직렬화/역직렬화 비용을 줄입니다.
/// GIF도 원본 형식과 바이트를 그대로 보존합니다.
#[tauri::command]
pub fn image_load(app: tauri::AppHandle) -> CmdResult<ImageLoadResponse> {
    let picked = FileDialog::new()
        .add_filter(
            "Images",
            &[
                "png", "jpg", "jpeg", "webp", "gif", "bmp", "svg", "ico", "avif",
            ],
        )
        .pick_file();

    let Some(path) = picked else {
        return Ok(ImageLoadResponse {
            success: false,
            error: None,
            image_path: None,
        });
    };

    let extension = normalize_image_extension(path.extension().and_then(|value| value.to_str()));
    let images_dir = app.path().app_data_dir()?.join("images");
    fs::create_dir_all(&images_dir)?;
    let imported_path = copy_image_to_app_data(&path, &images_dir, &extension)?;

    Ok(ImageLoadResponse {
        success: true,
        error: None,
        image_path: Some(imported_path.to_string_lossy().to_string()),
    })
}

fn copy_image_to_app_data(
    source_path: &Path,
    images_dir: &Path,
    extension: &str,
) -> CmdResult<PathBuf> {
    let destination_path = images_dir.join(format!("{}.{}", Uuid::new_v4(), extension));
    fs::copy(source_path, &destination_path)?;
    Ok(destination_path)
}

fn normalize_image_extension(extension: Option<&str>) -> String {
    match extension
        .unwrap_or_default()
        .trim()
        .to_ascii_lowercase()
        .as_str()
    {
        "jpg" => "jpg".to_string(),
        "jpeg" => "jpeg".to_string(),
        "webp" => "webp".to_string(),
        "gif" => "gif".to_string(),
        "bmp" => "bmp".to_string(),
        "svg" => "svg".to_string(),
        "ico" => "ico".to_string(),
        "avif" => "avif".to_string(),
        "png" => "png".to_string(),
        _ => "png".to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::{copy_image_to_app_data, normalize_image_extension};
    use std::fs;
    use uuid::Uuid;

    #[test]
    fn gif_extension_is_kept_without_format_conversion() {
        assert_eq!(normalize_image_extension(Some("GIF")), "gif");
    }

    #[test]
    fn copied_gif_keeps_extension_and_original_bytes() {
        let test_root = std::env::temp_dir().join(format!("dmnote-gif-{}", Uuid::new_v4()));
        let images_dir = test_root.join("images");
        fs::create_dir_all(&images_dir).unwrap();
        let source = test_root.join("animated.GIF");
        let original = b"GIF89a\x01\x00\x01\x00fixture";
        fs::write(&source, original).unwrap();

        let copied = copy_image_to_app_data(&source, &images_dir, "gif").unwrap();

        assert_eq!(
            copied.extension().and_then(|value| value.to_str()),
            Some("gif")
        );
        assert_eq!(fs::read(copied).unwrap(), original);
        fs::remove_dir_all(test_root).unwrap();
    }

    #[test]
    fn unknown_extension_uses_existing_png_fallback() {
        assert_eq!(normalize_image_extension(Some("unknown")), "png");
        assert_eq!(normalize_image_extension(None), "png");
    }
}
