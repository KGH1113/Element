#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/dmnote-app.sh
source "$TASK_DIR/../../lib/dmnote-app.sh"

# Each release archive carries the ImplDmNote built from the committed app
# sources; the mod opens it with the game. `impl-dm-note.sh macos` prepares
# only the macOS app (for a local install).
only="${1:-}"
if [ "${IMPL_RESOURCEPACK_SKIP_DMNOTE:-0}" = "1" ]; then
  printf 'Warning: packaging without ImplDmNote (IMPL_RESOURCEPACK_SKIP_DMNOTE=1). Players open it themselves.\n' >&2
  exit 0
fi

if [ "$only" != "macos" ] && ! packages_platform macos; then
  printf 'Skipping the macOS ImplDmNote (not packaged in this run).\n'
elif dmnote_mac_current; then
  printf 'Using the macOS ImplDmNote build for the current sources: %s\n' "$DMNOTE_MAC_APP"
else
  is_macos || fail "The macOS ImplDmNote is built with Xcode tools. Package on macOS, or set IMPL_RESOURCEPACK_DMNOTE_MAC_APP to a built ImplDmNote.app."
  require_command npm
  stamp="$(dmnote_source_stamp)"
  (cd "$IMPL_RESOURCEPACK_PROJECT_ROOT" && npm run desktop:build:mac)
  require_dir "$DMNOTE_MAC_APP"
  # Uncommitted app changes leave no stamp, so the next package builds again.
  if [ -n "$stamp" ]; then
    printf '%s\n' "$stamp" > "$DMNOTE_MAC_STAMP"
  else
    rm -f "$DMNOTE_MAC_STAMP"
  fi
fi

if [ "$only" = "macos" ] || ! packages_platform windows; then
  exit 0
elif dmnote_windows_current; then
  printf 'Using the Windows ImplDmNote build: %s\n' "$(dmnote_windows_zip)"
else
  fail "The Windows ImplDmNote archive is missing or older than the committed app sources ($(dmnote_windows_zip)). Run ./scripts/run.sh dmnote-windows-build (builds on IMPL_RESOURCEPACK_WINDOWS_HOST), set IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP to the desktop CI portable zip, or set IMPL_RESOURCEPACK_SKIP_DMNOTE=1 to package without ImplDmNote."
fi
