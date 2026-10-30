#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/mod-layout.sh
source "$TASK_DIR/../../lib/mod-layout.sh"
# shellcheck source=../../lib/dmnote-app.sh
source "$TASK_DIR/../../lib/dmnote-app.sh"

# stage.sh <macos|windows>: one OS's mod folder with only its native
# libraries, fonts and ElementNote build, laid out as a fresh install.
platform="${1:-}"
case "$platform" in
  macos) payload=macos fonts=Mac ;;
  windows) payload=windows-package fonts=Windows ;;
  *) fail "Usage: stage.sh <macos|windows>" ;;
esac

stage="$(package_stage "$platform")"
assert_non_root_path "$ELEMENT_PACKAGE_BUILD_ROOT"
assert_non_root_path "$stage"
mkdir -p "$ELEMENT_PACKAGE_BUILD_ROOT"
if [ -e "$stage" ]; then
  safe_remove_tree "$stage" "$ELEMENT_PACKAGE_BUILD_ROOT"
fi
mkdir -p "$stage"

stage_mod "$stage" "$payload" "$fonts"
if [ "${ELEMENT_SKIP_DMNOTE:-0}" != "1" ]; then
  "stage_dmnote_$platform" "$(mod_runtime_dir "$stage")"
fi

printf 'Staged %s package at %s\n' "$platform" "$stage"
