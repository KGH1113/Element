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
# shellcheck source=windows-webview.sh
source "${BASH_SOURCE[0]%/*}/windows-webview.sh"

# Native libraries per target: a local install carries the host's own, the
# release package every platform's, and a Windows install only Windows'.
runtime_payload_names() {
  local target="${1:-local}"
  RUNTIME_PAYLOAD_NAMES=(ImplResourcePack.dll)
  if [ "$target" != "windows" ] && is_macos; then
    RUNTIME_PAYLOAD_NAMES+=(libImplTrackpad.dylib native/libImplGameWebView.dylib)
  fi
  if [ "$target" = "windows" ] || { [ "$target" = "package" ] && windows_webview_ready; }; then
    RUNTIME_PAYLOAD_NAMES+=(native/ImplGameWebView.dll)
  fi
}

# Replace runtime files by rename, never truncate a DLL/dylib mapped by a
# running game. Existing processes retain their version until restart.
# copy_runtime_payload <destination> [local|package|windows]
copy_runtime_payload() {
  local destination="$1"
  local name temporary destination_dir
  runtime_payload_names "${2:-local}"
  local names=("${RUNTIME_PAYLOAD_NAMES[@]}")
  for name in "${names[@]}"; do
    require_file "$IMPL_RESOURCEPACK_BUILD_OUTPUT/$name"
  done
  for name in "${names[@]}"; do
    destination_dir="$(dirname "$destination/$name")"
    mkdir -p "$destination_dir"
    temporary="$(mktemp "$destination_dir/.${name##*/}.XXXXXX")"
    if ! cp -p "$IMPL_RESOURCEPACK_BUILD_OUTPUT/$name" "$temporary"; then
      rm -f "$temporary"
      return 1
    fi
    mv -f "$temporary" "$destination/$name"
  done
}

# copy_core_payload <destination> [local|package|windows]
copy_core_payload() {
  local destination="$1"

  require_file "$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/Info.json"
  mkdir -p "$destination"
  cp "$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/Info.json" "$destination/"
  copy_runtime_payload "$destination" "${2:-local}"
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
