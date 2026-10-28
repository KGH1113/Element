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

assert_non_root_path "$IMPL_RESOURCEPACK_INSTALL_PATH"
mkdir -p "$IMPL_RESOURCEPACK_INSTALL_PATH"

if [ "${1:-}" = "--runtime-only" ]; then
  require_file "$IMPL_RESOURCEPACK_INSTALL_PATH/Info.json"
  copy_runtime_payload "$IMPL_RESOURCEPACK_INSTALL_PATH"
  printf 'Updated runtime at %s (restart the game to load it)\n' "$IMPL_RESOURCEPACK_INSTALL_PATH"
  exit 0
fi

copy_core_payload "$IMPL_RESOURCEPACK_INSTALL_PATH"
copy_assets "$IMPL_RESOURCEPACK_INSTALL_PATH"
copy_debug_symbols "$IMPL_RESOURCEPACK_INSTALL_PATH"
# Opt-in: a release build of ImplDmNote takes minutes, so a regular install
# leaves the app out and Auto open/close does nothing.
if [ "${IMPL_RESOURCEPACK_INSTALL_DMNOTE:-0}" = "1" ] && is_macos; then
  "$TASK_DIR/../package/impl-dm-note.sh" macos
  stage_dmnote_macos "$IMPL_RESOURCEPACK_INSTALL_PATH"
fi

printf 'Installed to %s\n' "$IMPL_RESOURCEPACK_INSTALL_PATH"
