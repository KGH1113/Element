#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/mod-layout.sh
source "$TASK_DIR/../../lib/mod-layout.sh"
# shellcheck source=../../lib/cd-builds.sh
source "$TASK_DIR/../../lib/cd-builds.sh"

# A zip with this build, its Windows in-game viewer, ImplDmNote and
# Install.ps1 for the Windows PC. Nothing here connects to that PC: copy the
# zip there, unzip it and run Install.ps1 with the game closed. What cannot be
# built here comes from CD (.github/workflows/cd.yml).
require_command zip
output="$IMPL_RESOURCEPACK_PROJECT_ROOT/build/ImplResourcePack-windows-install.zip"

warn() {
  printf 'Warning: %s\n' "$*" >&2
}

# fetch_windows_viewer: a viewer library for the current sources, from CD
# when this machine has none.
fetch_windows_viewer() {
  local download
  windows_webview_ready && return 0
  if ! windows_webview_committed; then
    warn "The Windows in-game viewer has uncommitted changes, and CD builds only committed ones."
    return 1
  fi
  download="$(mktemp -d)"
  if cd_fetch windows_webview_source_stamp_at "$(windows_webview_source_stamp_at HEAD)" \
    "$CD_WINDOWS_VIEWER" ImplGameWebView.dll "$download/ImplGameWebView.dll"; then
    windows_webview_adopt "$download/ImplGameWebView.dll"
    rm -rf "$download"
    return 0
  fi
  rm -rf "$download"
  return 1
}

# fetch_windows_dmnote: a Windows ImplDmNote for the committed app sources,
# from CD when there is none here. Saved where `package` looks for it.
fetch_windows_dmnote() {
  local stamp archive
  dmnote_windows_current && return 0
  stamp="$(dmnote_source_stamp)"
  if [ -z "$stamp" ]; then
    warn "ImplDmNote has uncommitted changes, and CD builds only committed ones."
    return 1
  fi
  archive="$(dmnote_windows_zip)"
  cd_fetch dmnote_source_stamp_at "$stamp" "$CD_WINDOWS_APP" "${archive##*/}" "$archive" || return 1
  printf '%s\n' "$stamp" > "$archive.sources"
}

fetch_windows_viewer ||
  fail "No Windows in-game viewer for the current sources. Get one from CD as said above, or build it on the Windows PC: ./scripts/run.sh windows-webview-check"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
mod="$stage/ImplResourcePack"
# A test build: marked as development, so it never replaces itself with a release.
stage_mod "$mod" windows "" development
if [ "${IMPL_RESOURCEPACK_SKIP_DMNOTE:-0}" = "1" ]; then
  printf 'Leaving ImplDmNote out (IMPL_RESOURCEPACK_SKIP_DMNOTE=1). Install.ps1 keeps the copy that PC has.\n'
elif fetch_windows_dmnote; then
  stage_dmnote_windows "$(mod_runtime_dir "$mod")"
  printf 'Added the Windows ImplDmNote: %s\n' "$(dmnote_windows_zip)"
else
  warn "This zip has no ImplDmNote. Install.ps1 keeps the copy that PC already has."
fi
cp "$IMPL_RESOURCEPACK_PROJECT_ROOT/scripts/windows/install-mod.ps1" "$stage/Install.ps1"

mkdir -p "$(dirname "$output")"
rm -f "$output"
(cd "$stage" && zip -qr "$output" Install.ps1 ImplResourcePack)
cat <<DONE
Made $output
On the Windows PC, close ADOFAI, unzip it and run in that folder:
  powershell -ExecutionPolicy Bypass -File .\\Install.ps1
DONE
