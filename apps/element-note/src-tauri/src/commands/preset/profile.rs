//! Element's profiles, asked for by the game over its connection. Each Element
//! profile has its own key viewers: while another profile is in use they wait
//! in a "Save all" file beside the profile, and the store remembers which
//! profile the key viewers on screen belong to.

use std::path::{Path, PathBuf};

use serde::Deserialize;
use tauri::{AppHandle, Manager, WebviewWindow};

use crate::{
    commands::run_blocking,
    errors::{CmdResult, CommandError},
    state::{initial_state::initial_state, AppState},
};

use super::{
    load::{load_full_preset, read_full_preset, FullPresetLoad},
    save::{full_preset_of, write_full_preset},
};

/// What the profile coming into use shows.
#[derive(Debug, Deserialize, PartialEq)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ProfileKeyViewers {
    /// Its own key viewers, saved in this file.
    File { path: String },
    /// The key viewers a new install starts with.
    FirstStart,
    /// The key viewers on screen now.
    Keep,
}

/// The Element profile the key viewers on screen belong to, if Element has said.
#[tauri::command]
pub async fn profile_key_viewers_owner(app: AppHandle) -> CmdResult<Option<String>> {
    let state = app.state::<AppState>();
    Ok(state
        .store
        .with_state(|store| store.key_viewers_owner.clone()))
}

/// Saves the key viewers on screen to `path`, as "Save all" would.
#[tauri::command]
pub async fn profile_key_viewers_save(app: AppHandle, path: String) -> CmdResult<()> {
    let path = profile_file(&path)?;
    run_blocking(app, move |_, state| save_key_viewers(state, &path, false)).await
}

/// Puts `owner`'s key viewers on screen. The ones there now are saved to
/// `save` first, so the profile they belong to keeps them. Refused unless the
/// key viewers on screen are `expected`'s: the game planned the swap on that.
#[tauri::command]
pub async fn profile_key_viewers_swap(
    app: AppHandle,
    window: WebviewWindow,
    save: Option<String>,
    load: ProfileKeyViewers,
    owner: String,
    expected: Option<String>,
) -> CmdResult<()> {
    if owner.trim().is_empty() {
        return Err(CommandError::msg("profile-owner-missing"));
    }
    let save = save.as_deref().map(profile_file).transpose()?;
    let load = match load {
        ProfileKeyViewers::File { path } => Some(Some(profile_file(&path)?)),
        ProfileKeyViewers::FirstStart => Some(None),
        ProfileKeyViewers::Keep => None,
    };
    let window_label = window.label().to_string();
    run_blocking(app, move |app, state| {
        let current = state
            .store
            .with_state(|store| store.key_viewers_owner.clone());
        if current != expected {
            return Err(CommandError::msg("key-viewers-owner-changed"));
        }
        if let Some(path) = save.as_deref() {
            // The profile leaving keeps its press counts for when it comes back.
            save_key_viewers(state, path, true)?;
        }
        let Some(source) = load else {
            state
                .store
                .update(|store| store.key_viewers_owner = Some(owner))
                .map_err(|error| CommandError::msg(error.to_string()))?;
            return Ok(());
        };
        let preset = match source {
            Some(path) => read_full_preset(&path)?,
            None => full_preset_of(initial_state(&app.path().app_data_dir()?))?,
        };
        let result = load_full_preset(
            app,
            state,
            &window_label,
            preset,
            FullPresetLoad::Profile { owner },
        )?;
        match result.success {
            true => Ok(()),
            false => Err(CommandError::msg(
                result
                    .error
                    .unwrap_or_else(|| "profile-load-failed".to_string()),
            )),
        }
    })
    .await
}

fn save_key_viewers(state: &AppState, path: &Path, with_counts: bool) -> CmdResult<()> {
    let mut preset = full_preset_of(state.store.snapshot())?;
    if with_counts {
        preset.key_counters = Some(state.snapshot_key_counters());
    }
    write_full_preset(path, &preset)
}

/// Only whole paths to JSON files: the game names files in its own folder.
fn profile_file(path: &str) -> CmdResult<PathBuf> {
    let path = PathBuf::from(path);
    let is_json = path
        .extension()
        .is_some_and(|extension| extension.eq_ignore_ascii_case("json"));
    if !path.is_absolute() || !is_json {
        return Err(CommandError::msg("profile-path-invalid"));
    }
    Ok(path)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn profile_files_are_whole_json_paths() {
        let absolute = std::env::temp_dir().join("profile.elementnote.json");
        assert_eq!(profile_file(absolute.to_str().unwrap()).unwrap(), absolute);
        assert!(profile_file("profile.json").is_err());
        assert!(profile_file(std::env::temp_dir().join("profile.xml").to_str().unwrap()).is_err());
    }

    #[test]
    fn a_new_profile_gets_a_new_installs_key_viewers_as_a_readable_file() {
        let dir = std::env::temp_dir().join(format!(
            "elementnote-profile-first-start-{}",
            uuid::Uuid::new_v4()
        ));
        let initial = initial_state(&dir);
        let preset = full_preset_of(initial.clone()).unwrap();
        let path = dir.join("first-start.json");
        write_full_preset(&path, &preset).unwrap();
        let read = read_full_preset(&path).unwrap();

        let tabs = read.custom_tabs.unwrap();
        assert_eq!(tabs, initial.custom_tabs);
        assert_eq!(
            read.selected_viewer_tabs.unwrap(),
            initial.selected_viewer_tabs
        );
        assert_eq!(read.keys.unwrap(), initial.keys);
        assert!(
            read.embedded_local_fonts
                .is_some_and(|fonts| !fonts.is_empty()),
            "the bundled font travels inside the file"
        );
        let _ = std::fs::remove_dir_all(dir);
    }

    #[test]
    fn the_profile_coming_in_names_where_its_key_viewers_come_from() {
        let file: ProfileKeyViewers =
            serde_json::from_value(serde_json::json!({"kind": "file", "path": "/a.json"})).unwrap();
        assert_eq!(
            file,
            ProfileKeyViewers::File {
                path: "/a.json".to_string()
            }
        );
        let first: ProfileKeyViewers =
            serde_json::from_value(serde_json::json!({"kind": "firstStart"})).unwrap();
        assert_eq!(first, ProfileKeyViewers::FirstStart);
        let keep: ProfileKeyViewers =
            serde_json::from_value(serde_json::json!({"kind": "keep"})).unwrap();
        assert_eq!(keep, ProfileKeyViewers::Keep);
    }
}
