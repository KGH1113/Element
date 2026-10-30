#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/mod-layout.sh
source "$TASK_DIR/../../lib/mod-layout.sh"

# Element.update.json: what the in-game updater reads from the latest
# release. Each archive must hold a complete version folder for it.
require_command unzip
version="$(mod_version)"
runtime="Element/Runtime/versions/$version"
pairs=()
for platform in $ELEMENT_PACKAGE_PLATFORMS; do
  archive="$(package_zip "$platform")"
  require_file "$archive"
  listing="$(unzip -Z1 "$archive")"
  for entry in Element/Info.json "Element/Runtime/launchers/$version/Element.Launcher.dll" \
    Element/Runtime/state.json "$runtime/Info.json" "$runtime/Element.dll" \
    "$runtime/Element.Launcher.dll" "$runtime/Element.Updater.dll" "$runtime/ipc/manifest.json"; do
    grep -Fxq "$entry" <<< "$listing" || fail "$archive is missing $entry."
  done
  pairs+=("$platform=$archive")
done

mkdir -p "$(dirname "$ELEMENT_UPDATE_MANIFEST_PATH")"
node "$ELEMENT_MOD_INFO_TOOL" manifest "$ELEMENT_MOD_INFO" "$ELEMENT_UPDATE_MANIFEST_PATH" "${pairs[@]}"
printf 'Update manifest: %s\n' "$ELEMENT_UPDATE_MANIFEST_PATH"
