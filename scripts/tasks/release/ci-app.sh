#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/desktop-ci.sh
source "$TASK_DIR/../../lib/desktop-ci.sh"

# ci-app.sh <env file>: downloads the ImplDmNote that Desktop CI built on main
# from the app sources at HEAD, and writes the package overrides that use it.
env_file="$1"
artifacts=("$DESKTOP_CI_MAC_APP" "$DESKTOP_CI_WINDOWS_APP")
destination="$IMPL_RESOURCEPACK_PROJECT_ROOT/build/release/ci"
version="$(node "$TASK_DIR/../version/version.mjs" check)"
cd "$IMPL_RESOURCEPACK_PROJECT_ROOT"
stamp="$(dmnote_source_stamp_at HEAD)"

# The newest main run (a merge or a manual dispatch) built from these sources.
read -r state run <<< "$(desktop_ci_find_run "$stamp" main "${artifacts[@]}")" || true
if [ "${state:-}" = "running" ]; then
  printf 'Waiting for Desktop CI run %s to finish…\n' "$run"
  gh run watch "$run" --exit-status >/dev/null || fail "Desktop CI run $run failed. Fix main and merge again: gh run view $run --log-failed"
  desktop_ci_has_artifacts "$run" "${artifacts[@]}" || fail "Desktop CI run $run finished without both ImplDmNote builds."
elif [ "${state:-}" != "ready" ]; then
  fail "No Desktop CI run on main has downloadable ImplDmNote builds for the app sources at HEAD (builds expire after 90 days). Start one with: gh workflow run $DESKTOP_CI_WORKFLOW --ref main"
fi
printf 'Using ImplDmNote from Desktop CI run %s\n' "$run"

assert_non_root_path "$destination"
rm -rf "$destination"
mkdir -p "$destination/macos" "$destination/windows"
desktop_ci_download "$run" "$DESKTOP_CI_MAC_APP" "$destination/macos"
desktop_ci_download "$run" "$DESKTOP_CI_WINDOWS_APP" "$destination/windows"

# The artifact is a ditto archive, which keeps the app's signature and modes.
require_file "$destination/macos/ImplDmNote.app.zip"
ditto -x -k "$destination/macos/ImplDmNote.app.zip" "$destination/macos"
app="$destination/macos/ImplDmNote.app"
require_dir "$app"
codesign --verify --deep --strict "$app" || fail "The CI macOS ImplDmNote has a broken signature: $app"
app_version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app/Contents/Info.plist")"
[ "$app_version" = "$version" ] || fail "The CI macOS ImplDmNote is $app_version, expected $version."

windows_zip="$destination/windows/ImplDmNote-v$version-windows-x64-portable.zip"
require_file "$windows_zip"

{
  printf 'export IMPL_RESOURCEPACK_DMNOTE_MAC_APP=%q\n' "$app"
  printf 'export IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP=%q\n' "$windows_zip"
} > "$env_file"
