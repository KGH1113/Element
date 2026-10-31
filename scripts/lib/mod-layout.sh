#!/usr/bin/env bash
# The installed mod layout (docs/auto-update.md): user data at the root, the
# launcher UMM starts under Runtime/launchers/<v>/, and everything a version
# runs from under Runtime/versions/<v>/. Installs, the Windows test install
# and release packages all stage it the same way.

if [ "${ELEMENT_MOD_LAYOUT_LOADED:-0}" = "1" ]; then
  return 0
fi
ELEMENT_MOD_LAYOUT_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"
# shellcheck source=guards.sh
source "${BASH_SOURCE[0]%/*}/guards.sh"
# shellcheck source=artifacts.sh
source "${BASH_SOURCE[0]%/*}/artifacts.sh"
# shellcheck source=ipc-bundle.sh
source "${BASH_SOURCE[0]%/*}/ipc-bundle.sh"

ELEMENT_MOD_INFO="$ELEMENT_PROJECT_ROOT/Element/Info.json"
ELEMENT_MOD_INFO_TOOL="${BASH_SOURCE[0]%/*}/mod-info.mjs"

mod_version() {
  node "$ELEMENT_MOD_INFO_TOOL" version "$ELEMENT_MOD_INFO"
}

# mod_runtime_dir <mod folder>: this build's version folder inside it.
mod_runtime_dir() {
  printf '%s/Runtime/versions/%s\n' "$1" "$(mod_version)"
}

# stage_mod <mod folder> <payload target> [font platform] [development]
# Writes the version folder, its launcher, the root Info.json, the shared IPC
# bundle and a fresh Runtime/state.json. User settings at the root stay.
# "development" marks the version so it never replaces itself with a release.
stage_mod() {
  local destination="$1" target="$2" fonts="${3:-}" development="${4:-}"
  local version runtime
  require_command node
  version="$(mod_version)"
  runtime="$destination/Runtime/versions/$version"

  copy_core_payload "$runtime" "$target"
  copy_assets "$runtime" "$fonts"
  rm -f "$runtime/.update.json"
  if [ "$development" = "development" ]; then
    : > "$runtime/.development"
  else
    rm -f "$runtime/.development"
  fi

  mkdir -p "$destination/Runtime/launchers/$version"
  cp "$ELEMENT_BUILD_OUTPUT/Element.Launcher.dll" "$destination/Runtime/launchers/$version/"
  copy_ipc_bundle "$destination"
  cp "$ELEMENT_PROJECT_ROOT/LICENSE.md" "$ELEMENT_PROJECT_ROOT/THIRD_PARTY_NOTICES.md" "$destination/"
  node "$ELEMENT_MOD_INFO_TOOL" launcher "$ELEMENT_MOD_INFO" "$destination/Info.json"
  node "$ELEMENT_MOD_INFO_TOOL" state "$version" "$destination/Runtime/state.json"
  rm -f "$destination/Runtime/state.json.bak"
}

# remove_legacy_payload <mod folder>: what the mod kept at its root before
# versioned runtimes. The launcher removes it too; an install does it at once.
remove_legacy_payload() {
  local destination="$1" name
  rm -f "$destination/Element.dll" "$destination/Element.pdb" "$destination/libElementTrackpad.dylib"
  rm -f "$destination"/Element.dll.*.cache
  for name in native Assets ElementNote; do
    if [ -e "$destination/$name" ]; then
      safe_remove_tree "$destination/$name" "$destination"
    fi
  done
}

# remove_other_versions <mod folder>: a local install starts a fresh state, so
# versions downloaded or installed before it do not linger.
remove_other_versions() {
  local destination="$1" version folder
  version="$(mod_version)"
  for folder in "$destination"/Runtime/versions/* "$destination"/Runtime/launchers/*; do
    [ -d "$folder" ] || continue
    [ "$(basename "$folder")" = "$version" ] || safe_remove_tree "$folder" "$destination"
  done
}
