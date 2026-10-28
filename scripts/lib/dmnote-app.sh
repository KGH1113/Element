#!/usr/bin/env bash
# The ImplDmNote builds a mod package carries: the macOS app bundle (built
# here) and the Windows portable archive (built on the Windows host or taken
# from desktop CI). A build is current when it was made from the committed
# app sources it stamps.

if [ "${IMPL_RESOURCEPACK_DMNOTE_APP_LOADED:-0}" = "1" ]; then
  return 0
fi
IMPL_RESOURCEPACK_DMNOTE_APP_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"
# shellcheck source=guards.sh
source "${BASH_SOURCE[0]%/*}/guards.sh"

DMNOTE_APP_ROOT="$IMPL_RESOURCEPACK_PROJECT_ROOT/apps/impl-dm-note"
DMNOTE_MAC_APP="${IMPL_RESOURCEPACK_DMNOTE_MAC_APP:-$DMNOTE_APP_ROOT/src-tauri/target/release/bundle/macos/ImplDmNote.app}"
DMNOTE_MAC_STAMP="$DMNOTE_MAC_APP.sources"
# Inputs of an app build besides apps/impl-dm-note itself.
DMNOTE_SOURCE_PATHS=(apps/impl-dm-note package.json package-lock.json)
export DMNOTE_APP_ROOT DMNOTE_MAC_APP DMNOTE_MAC_STAMP

dmnote_version() {
  sed -n 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$DMNOTE_APP_ROOT/package.json" | head -n 1
}

dmnote_windows_zip() {
  printf '%s\n' "${IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP:-$DMNOTE_APP_ROOT/dist/portable/ImplDmNote-v$(dmnote_version)-windows-x64-portable.zip}"
}

# Prints the committed source identity, or nothing when the app sources have
# uncommitted changes (then no stamp can describe them).
dmnote_source_stamp() {
  local path
  if [ -n "$(git -C "$IMPL_RESOURCEPACK_PROJECT_ROOT" status --porcelain -- "${DMNOTE_SOURCE_PATHS[@]}")" ]; then
    return 0
  fi
  for path in "${DMNOTE_SOURCE_PATHS[@]}"; do
    git -C "$IMPL_RESOURCEPACK_PROJECT_ROOT" rev-parse "HEAD:$path"
  done | shasum -a 256 | cut -d ' ' -f 1
}

# dmnote_build_current <build> <stamp file>
dmnote_build_current() {
  local stamp
  stamp="$(dmnote_source_stamp)"
  [ -n "$stamp" ] && [ -e "$1" ] && [ -f "$2" ] && [ "$(cat "$2")" = "$stamp" ]
}

dmnote_mac_current() {
  [ -n "${IMPL_RESOURCEPACK_DMNOTE_MAC_APP:-}" ] || dmnote_build_current "$DMNOTE_MAC_APP" "$DMNOTE_MAC_STAMP"
}

# An archive passed in explicitly (for example from desktop CI) is trusted.
dmnote_windows_current() {
  [ -n "${IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP:-}" ] && [ -f "$IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP" ] && return 0
  dmnote_build_current "$(dmnote_windows_zip)" "$(dmnote_windows_zip).sources"
}

# stage_dmnote_macos <mod folder>: ditto keeps the executable bit and code
# signature through the zip; the mod unpacks it with ditto as well.
stage_dmnote_macos() {
  local destination="$1/ImplDmNote/macos"
  require_command ditto
  require_dir "$DMNOTE_MAC_APP"
  mkdir -p "$destination"
  rm -f "$destination/ImplDmNote.app.zip"
  ditto -c -k --keepParent "$DMNOTE_MAC_APP" "$destination/ImplDmNote.app.zip"
}

# stage_dmnote_windows <mod folder>: the portable archive holds one folder
# with impl-dm-note.exe and its notices.
stage_dmnote_windows() {
  local destination="$1/ImplDmNote/windows" archive unpacked folder
  archive="$(dmnote_windows_zip)"
  require_command unzip
  require_file "$archive"
  unpacked="$(mktemp -d)"
  unzip -q "$archive" -d "$unpacked"
  folder="$(dirname "$(find "$unpacked" -name impl-dm-note.exe -type f | head -n 1)")"
  if [ ! -f "$folder/impl-dm-note.exe" ]; then
    rm -rf "$unpacked"
    fail "The ImplDmNote archive has no impl-dm-note.exe: $archive"
  fi
  mkdir -p "$destination"
  cp -R "$folder/." "$destination/"
  rm -rf "$unpacked"
}
