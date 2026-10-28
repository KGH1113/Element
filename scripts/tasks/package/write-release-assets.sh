#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/mod-layout.sh
source "$TASK_DIR/../../lib/mod-layout.sh"

# ImplResourcePack.update.json: what the in-game updater reads from the latest
# release. Each archive must hold a complete version folder for it.
require_command unzip
version="$(mod_version)"
runtime="ImplResourcePack/Runtime/versions/$version"
pairs=()
for platform in $IMPL_RESOURCEPACK_PACKAGE_PLATFORMS; do
  archive="$(package_zip "$platform")"
  require_file "$archive"
  listing="$(unzip -Z1 "$archive")"
  for entry in ImplResourcePack/Info.json "ImplResourcePack/Runtime/launchers/$version/ImplResourcePack.Launcher.dll" \
    ImplResourcePack/Runtime/state.json "$runtime/Info.json" "$runtime/ImplResourcePack.dll" \
    "$runtime/ImplResourcePack.Launcher.dll" "$runtime/ImplResourcePack.Updater.dll" "$runtime/ipc/manifest.json"; do
    grep -Fxq "$entry" <<< "$listing" || fail "$archive is missing $entry."
  done
  pairs+=("$platform=$archive")
done

mkdir -p "$(dirname "$IMPL_RESOURCEPACK_UPDATE_MANIFEST_PATH")"
node "$IMPL_RESOURCEPACK_MOD_INFO_TOOL" manifest "$IMPL_RESOURCEPACK_MOD_INFO" "$IMPL_RESOURCEPACK_UPDATE_MANIFEST_PATH" "${pairs[@]}"
printf 'Update manifest: %s\n' "$IMPL_RESOURCEPACK_UPDATE_MANIFEST_PATH"
