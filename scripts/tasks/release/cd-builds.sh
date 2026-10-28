#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/cd-builds.sh
source "$TASK_DIR/../../lib/cd-builds.sh"

# cd-builds.sh <env file>: downloads what CD built on main from the sources at
# HEAD (both ImplDmNote apps and the Windows in-game viewer), takes the viewer
# as this build's, and writes the package overrides that use the apps.
env_file="$1"
artifacts=("$CD_MAC_APP" "$CD_WINDOWS_APP" "$CD_WINDOWS_VIEWER")
destination="$IMPL_RESOURCEPACK_PROJECT_ROOT/build/release/cd"
version="$(node "$TASK_DIR/../version/version.mjs" check)"
cd "$IMPL_RESOURCEPACK_PROJECT_ROOT"
stamp="$(release_source_stamp_at HEAD)"

# The newest main run (a merge or a manual run) built from these sources.
read -r state run <<< "$(cd_find_run release_source_stamp_at "$stamp" main "${artifacts[@]}")" || true
if [ "${state:-}" = "running" ]; then
  printf 'Waiting for CD run %s to finish…\n' "$run"
  gh run watch "$run" --exit-status >/dev/null || fail "CD run $run failed. See why with: gh run view $run --log-failed"
  cd_has_artifacts "$run" "${artifacts[@]}" || fail "CD run $run finished without all of its builds (${artifacts[*]})."
elif [ "${state:-}" != "ready" ]; then
  fail "No CD run on main has downloadable builds of the sources at HEAD (builds expire after 90 days). Start one with: gh workflow run $CD_WORKFLOW --ref main"
fi
printf 'Using the builds from CD run %s\n' "$run"

assert_non_root_path "$destination"
rm -rf "$destination"
mkdir -p "$destination/macos" "$destination/windows" "$destination/viewer"
cd_download "$run" "$CD_MAC_APP" "$destination/macos"
cd_download "$run" "$CD_WINDOWS_APP" "$destination/windows"
cd_download "$run" "$CD_WINDOWS_VIEWER" "$destination/viewer"

# The artifact is a ditto archive, which keeps the app's signature and modes.
require_file "$destination/macos/ImplDmNote.app.zip"
ditto -x -k "$destination/macos/ImplDmNote.app.zip" "$destination/macos"
app="$destination/macos/ImplDmNote.app"
require_dir "$app"
codesign --verify --deep --strict "$app" || fail "The macOS ImplDmNote from CD has a broken signature: $app"
app_version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app/Contents/Info.plist")"
[ "$app_version" = "$version" ] || fail "The macOS ImplDmNote from CD is $app_version, expected $version."

windows_zip="$destination/windows/ImplDmNote-v$version-windows-x64-portable.zip"
require_file "$windows_zip"

# CD ran the smoke probe before uploading the library; preflight checked the
# sources here are HEAD's.
require_file "$destination/viewer/ImplGameWebView.dll"
windows_webview_adopt "$destination/viewer/ImplGameWebView.dll"

{
  printf 'export IMPL_RESOURCEPACK_DMNOTE_MAC_APP=%q\n' "$app"
  printf 'export IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP=%q\n' "$windows_zip"
} > "$env_file"
