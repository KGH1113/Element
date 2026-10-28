#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/artifacts.sh
source "$TASK_DIR/../../lib/artifacts.sh"
# shellcheck source=../../lib/dmnote-app.sh
source "$TASK_DIR/../../lib/dmnote-app.sh"

# stage.sh <macos|windows>: one OS's mod folder with only its native
# libraries, fonts and ImplDmNote build.
platform="${1:-}"
case "$platform" in
  macos) payload=macos fonts=Mac ;;
  windows) payload=windows-package fonts=Windows ;;
  *) fail "Usage: stage.sh <macos|windows>" ;;
esac

stage="$(package_stage "$platform")"
assert_non_root_path "$IMPL_RESOURCEPACK_PACKAGE_BUILD_ROOT"
assert_non_root_path "$stage"
mkdir -p "$IMPL_RESOURCEPACK_PACKAGE_BUILD_ROOT"
if [ -e "$stage" ]; then
  safe_remove_tree "$stage" "$IMPL_RESOURCEPACK_PACKAGE_BUILD_ROOT"
fi
mkdir -p "$stage"

copy_core_payload "$stage" "$payload"
copy_assets "$stage" "$fonts"
if [ "${IMPL_RESOURCEPACK_SKIP_DMNOTE:-0}" != "1" ]; then
  "stage_dmnote_$platform" "$stage"
fi

printf 'Staged %s package at %s\n' "$platform" "$stage"
