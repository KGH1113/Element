#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/mod-layout.sh
source "$TASK_DIR/../../lib/mod-layout.sh"
# shellcheck source=../../lib/desktop-ci.sh
source "$TASK_DIR/../../lib/desktop-ci.sh"

# A zip with this build, its ImplDmNote and Install.ps1 for the Windows PC.
# Nothing here connects to that PC: copy the zip there, unzip it and run
# Install.ps1 with the game closed.
require_command zip
windows_webview_ready || fail "Build the Windows in-game viewer first: ./scripts/run.sh windows-webview-check"
output="$IMPL_RESOURCEPACK_PROJECT_ROOT/build/ImplResourcePack-windows-install.zip"

warn() {
  printf 'Warning: %s\n' "$*" >&2
}

# fetch_windows_dmnote: makes a Windows ImplDmNote for the committed app
# sources available, downloading Desktop CI's when there is none here.
# Says why when there is none to get.
fetch_windows_dmnote() {
  local stamp state run archive download
  dmnote_windows_current && return 0
  stamp="$(dmnote_source_stamp)"
  if [ -z "$stamp" ]; then
    warn "ImplDmNote has uncommitted changes, and Desktop CI builds only committed ones."
    return 1
  fi
  if ! command -v gh >/dev/null 2>&1; then
    warn "The Windows ImplDmNote comes from Desktop CI, which needs the GitHub CLI (gh)."
    return 1
  fi
  read -r state run <<< "$(desktop_ci_find_run "$stamp" "" "$DESKTOP_CI_WINDOWS_APP")" || true
  case "${state:-}" in
    ready) ;;
    running)
      warn "Desktop CI run $run is still building ImplDmNote for these sources. Run windows-install again when it finishes."
      return 1
      ;;
    *)
      warn "Desktop CI has not built ImplDmNote from the committed app sources yet. Push them (a pull request or main), then run windows-install again once Desktop CI finishes."
      return 1
      ;;
  esac
  archive="$(dmnote_windows_zip)"
  download="$(mktemp -d)"
  if ! desktop_ci_download "$run" "$DESKTOP_CI_WINDOWS_APP" "$download" || [ ! -f "$download/${archive##*/}" ]; then
    rm -rf "$download"
    warn "Could not download ${archive##*/} from Desktop CI run $run."
    return 1
  fi
  mkdir -p "$(dirname "$archive")"
  mv -f "$download/${archive##*/}" "$archive"
  rm -rf "$download"
  # Saved where `package` looks, stamped like a local build of the same sources.
  printf '%s\n' "$stamp" > "$archive.sources"
  printf 'Downloaded the Windows ImplDmNote from Desktop CI run %s\n' "$run"
}

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
