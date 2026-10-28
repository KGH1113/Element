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

assert_non_root_path "$IMPL_RESOURCEPACK_INSTALL_PATH"
mkdir -p "$IMPL_RESOURCEPACK_INSTALL_PATH"
runtime="$(mod_runtime_dir "$IMPL_RESOURCEPACK_INSTALL_PATH")"

if [ "${1:-}" = "--runtime-only" ]; then
  require_dir "$runtime"
  copy_runtime_payload "$runtime"
  printf 'Updated runtime at %s (restart the game to load it)\n' "$runtime"
  exit 0
fi

# A local build never replaces itself with a release. Set
# IMPL_RESOURCEPACK_INSTALL_UPDATABLE=1 to try the updater on this install.
development=development
if [ "${IMPL_RESOURCEPACK_INSTALL_UPDATABLE:-0}" = "1" ]; then
  development=""
fi
remove_legacy_payload "$IMPL_RESOURCEPACK_INSTALL_PATH"
remove_other_versions "$IMPL_RESOURCEPACK_INSTALL_PATH"
stage_mod "$IMPL_RESOURCEPACK_INSTALL_PATH" local "" "$development"
copy_debug_symbols "$runtime"
# Opt-in: a release build of ImplDmNote takes minutes, so a regular install
# leaves the app out and Auto open/close does nothing.
if [ "${IMPL_RESOURCEPACK_INSTALL_DMNOTE:-0}" = "1" ] && is_macos; then
  "$TASK_DIR/../package/impl-dm-note.sh" macos
  stage_dmnote_macos "$runtime"
fi

printf 'Installed %s to %s\n' "$(mod_version)" "$IMPL_RESOURCEPACK_INSTALL_PATH"
