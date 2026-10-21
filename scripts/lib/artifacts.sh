#!/usr/bin/env bash

if [ "${IMPL_RESOURCEPACK_ARTIFACTS_LOADED:-0}" = "1" ]; then
  return 0
fi
IMPL_RESOURCEPACK_ARTIFACTS_LOADED=1

IMPL_RESOURCEPACK_ARTIFACTS_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=context.sh
source "$IMPL_RESOURCEPACK_ARTIFACTS_LIB_DIR/context.sh"
# shellcheck source=guards.sh
source "$IMPL_RESOURCEPACK_ARTIFACTS_LIB_DIR/guards.sh"
# shellcheck source=ipc-bundle.sh
source "${BASH_SOURCE[0]%/*}/ipc-bundle.sh"

# Replace runtime files by rename, never truncate a DLL/dylib mapped by a
# running game. Existing processes retain their version until restart.
copy_runtime_payload() {
  local destination="$1"
  local name temporary
  local names=(ImplResourcePack.dll)
  if is_macos; then
    names+=(libImplTrackpad.dylib)
  fi
  for name in "${names[@]}"; do
    require_file "$IMPL_RESOURCEPACK_BUILD_OUTPUT/$name"
  done
  for name in "${names[@]}"; do
    temporary="$(mktemp "$destination/.$name.XXXXXX")"
    if ! cp -p "$IMPL_RESOURCEPACK_BUILD_OUTPUT/$name" "$temporary"; then
      rm -f "$temporary"
      return 1
    fi
    mv -f "$temporary" "$destination/$name"
  done
}

copy_core_payload() {
  local destination="$1"

  require_file "$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/Info.json"
  mkdir -p "$destination"
  cp "$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/Info.json" "$destination/"
  copy_runtime_payload "$destination"
  cp "$IMPL_RESOURCEPACK_PROJECT_ROOT/THIRD_PARTY_NOTICES.md" "$destination/"
  cp "$IMPL_RESOURCEPACK_PROJECT_ROOT/LICENSE.md" "$destination/"
  copy_ipc_bundle "$destination"
}

copy_assets() {
  local destination="$1"
  local source="$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/Assets"
  local target="$destination/Assets"

  if [ ! -d "$source" ]; then
    return 0
  fi

  if [ -e "$target" ]; then
    safe_remove_tree "$target" "$destination"
  fi
  cp -R "$source" "$target"
}

copy_debug_symbols() {
  local destination="$1"

  if [ -f "$IMPL_RESOURCEPACK_BUILD_OUTPUT/ImplResourcePack.pdb" ]; then
    cp "$IMPL_RESOURCEPACK_BUILD_OUTPUT/ImplResourcePack.pdb" "$destination/"
  fi
}
