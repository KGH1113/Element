fn main() {
    let _ = std::env::set_current_dir(std::path::Path::new(env!("CARGO_MANIFEST_DIR")));

    generate_permissions();
    #[cfg(target_os = "macos")]
    maybe_build_macos_dock_helper();
    build_tauri();
}

/// commands/ 디렉토리의 `#[tauri::command]` 함수명을 스캔하여
/// permissions/element-note-allow-all.json 자동 생성
fn generate_permissions() {
    use std::fs;
    use std::path::Path;

    let commands_dir = Path::new("src/commands");
    println!("cargo:rerun-if-changed=src/commands");

    let mut command_names: Vec<String> = Vec::new();

    scan_commands_dir(commands_dir, &mut command_names);

    fn scan_commands_dir(dir: &Path, names: &mut Vec<String>) {
        let entries = match fs::read_dir(dir) {
            Ok(e) => e,
            Err(err) => {
                println!("cargo:warning=commands 디렉토리 읽기 실패: {err}");
                return;
            }
        };

        for entry in entries.filter_map(Result::ok) {
            let path = entry.path();
            if path.is_dir() {
                scan_commands_dir(&path, names);
                continue;
            }
            if path.extension().map(|e| e != "rs").unwrap_or(true) {
                continue;
            }
            if path.file_name().map(|n| n == "mod.rs").unwrap_or(false) {
                continue;
            }

            let content = match fs::read_to_string(&path) {
                Ok(c) => c,
                Err(_) => continue,
            };

            // #[tauri::command] 또는 #[tauri::command(...)] 다음 줄의 pub fn / pub async fn 이름 추출
            let lines: Vec<&str> = content.lines().collect();
            for (i, line) in lines.iter().enumerate() {
                let trimmed = line.trim();
                if trimmed.starts_with("#[tauri::command") {
                    for next_line in lines.iter().skip(i + 1) {
                        let next = next_line.trim();
                        if next.is_empty() || next.starts_with("//") || next.starts_with('#') {
                            continue;
                        }
                        if let Some(name) = extract_fn_name(next) {
                            names.push(name);
                        }
                        break;
                    }
                }
            }
        }
    }

    command_names.sort();

    let allow_json: Vec<String> = command_names
        .iter()
        .map(|n| format!("          \"{}\"", n))
        .collect();

    let json = format!(
        r#"{{
  "default": null,
  "permission": [
    {{
      "identifier": "element-note-allow-all",
      "description": "Full ElementNote command access for renderer",
      "commands": {{
        "allow": [
{}
        ],
        "deny": []
      }}
    }}
  ]
}}"#,
        allow_json.join(",\n")
    );

    let perm_path = Path::new("permissions/element-note-allow-all.json");
    // 기존 내용과 동일하면 스킵 (불필요한 재빌드 방지)
    if let Ok(existing) = fs::read_to_string(perm_path) {
        if existing == json {
            return;
        }
    }

    if let Err(err) = fs::write(perm_path, &json) {
        println!("cargo:warning=permissions 파일 쓰기 실패: {err}");
    }
}

/// `pub fn name(` 또는 `pub async fn name(` 에서 함수명 추출
fn extract_fn_name(line: &str) -> Option<String> {
    let rest = line
        .strip_prefix("pub async fn ")
        .or_else(|| line.strip_prefix("pub fn "))?;
    rest.split('(').next().map(|s| s.trim().to_string())
}

/// 빌드 프로필에 따라 tauri-build를 실행합니다.
/// 릴리즈 빌드에서는 Windows 관리자 권한을 요청하는 manifest를 적용하고,
/// 개발 서버(tauri dev)에서는 기본 설정을 사용합니다.
fn build_tauri() {
    #[cfg(target_os = "windows")]
    {
        println!("cargo:rerun-if-changed=app.release.manifest");
        let profile = std::env::var("PROFILE").unwrap_or_default();
        if profile == "release" {
            let manifest_path = std::path::Path::new("app.release.manifest");
            if manifest_path.exists() {
                let manifest =
                    std::fs::read_to_string(manifest_path).expect("app.release.manifest 읽기 실패");
                tauri_build::try_build(tauri_build::Attributes::new().windows_attributes(
                    tauri_build::WindowsAttributes::new().app_manifest(manifest),
                ))
                .expect("tauri 빌드 실패");
                return;
            }
        }
    }
    tauri_build::build();
}

#[cfg(target_os = "macos")]
fn maybe_build_macos_dock_helper() {
    use std::fs;
    use std::os::unix::fs::PermissionsExt;
    use std::path::PathBuf;
    use std::process::Command;

    let helper_src = PathBuf::from("helper/DockHelper/main.swift");
    let helper_info = PathBuf::from("helper/DockHelper/Info.plist");
    let legacy_helper_bundle = PathBuf::from("target/element-note-helper/ElementNoteDockHelper.app");
    let helper_bundle = PathBuf::from("target/element-note-helper/ElementNote.app");
    let helper_contents = helper_bundle.join("Contents");
    let helper_macos = helper_contents.join("MacOS");
    let helper_resources = helper_contents.join("Resources");
    let helper_exec = helper_macos.join("ElementNoteDockHelper");
    let helper_bundle_info = helper_contents.join("Info.plist");
    let helper_icon = helper_resources.join("icon.icns");
    let helper_module_cache = PathBuf::from("target/element-note-helper/module-cache");
    let source_icon = PathBuf::from("icons/icon.icns");

    println!("cargo:rerun-if-changed={}", helper_src.display());
    println!("cargo:rerun-if-changed={}", helper_info.display());
    println!("cargo:rerun-if-changed={}", source_icon.display());

    if legacy_helper_bundle.exists() {
        fs::remove_dir_all(&legacy_helper_bundle)
            .expect("failed to remove legacy macOS Dock helper bundle");
    }
    if helper_bundle.exists() {
        fs::remove_dir_all(&helper_bundle)
            .expect("failed to remove existing macOS Dock helper bundle");
    }

    fs::create_dir_all(&helper_macos).expect("failed to create helper MacOS dir");
    fs::create_dir_all(&helper_resources).expect("failed to create helper Resources dir");
    fs::create_dir_all(&helper_module_cache).expect("failed to create helper module cache dir");

    let status = Command::new("xcrun")
        .args(["--sdk", "macosx", "swiftc"])
        .arg(&helper_src)
        .args(["-O", "-framework", "AppKit", "-o"])
        .arg(&helper_exec)
        .env("CLANG_MODULE_CACHE_PATH", &helper_module_cache)
        .env("SWIFT_MODULECACHE_PATH", &helper_module_cache)
        .status();

    let status = status.expect("failed to invoke swiftc for helper build");
    assert!(
        status.success(),
        "swiftc helper build failed with status {status}"
    );

    fs::set_permissions(&helper_exec, fs::Permissions::from_mode(0o755))
        .expect("failed to set helper executable permissions");
    fs::copy(&helper_info, &helper_bundle_info).expect("failed to copy helper Info.plist");
    fs::copy(&source_icon, &helper_icon).expect("failed to copy helper icon");
}
