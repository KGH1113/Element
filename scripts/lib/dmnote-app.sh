#!/usr/bin/env bash
# The ElementNote builds a mod package or install carries: the macOS app bundle
# (built here from the working tree) and the Windows portable archive (built
# from committed sources by CD or the Windows host). A build is current
# when the app sources it stamps are the ones it would be built from now.

if [ "${ELEMENT_DMNOTE_APP_LOADED:-0}" = "1" ]; then
  return 0
fi
ELEMENT_DMNOTE_APP_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"
# shellcheck source=guards.sh
source "${BASH_SOURCE[0]%/*}/guards.sh"

DMNOTE_APP_ROOT="$ELEMENT_PROJECT_ROOT/apps/element-note"
DMNOTE_MAC_APP="${ELEMENT_DMNOTE_MAC_APP:-$DMNOTE_APP_ROOT/src-tauri/target/release/bundle/macos/ElementNote.app}"
DMNOTE_MAC_STAMP="$DMNOTE_MAC_APP.sources"
# Inputs of an app build besides apps/element-note itself.
DMNOTE_SOURCE_PATHS=(apps/element-note package.json package-lock.json)
export DMNOTE_APP_ROOT DMNOTE_MAC_APP DMNOTE_MAC_STAMP

dmnote_version() {
  sed -n 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$DMNOTE_APP_ROOT/package.json" | head -n 1
}

dmnote_windows_zip() {
  printf '%s\n' "${ELEMENT_DMNOTE_WINDOWS_ZIP:-$DMNOTE_APP_ROOT/dist/portable/ElementNote-v$(dmnote_version)-windows-x64-portable.zip}"
}

# Prints the committed source identity, or nothing when the app sources have
# uncommitted changes (then no stamp can describe them).
dmnote_source_stamp() {
  if [ -n "$(git -C "$ELEMENT_PROJECT_ROOT" status --porcelain -- "${DMNOTE_SOURCE_PATHS[@]}")" ]; then
    return 0
  fi
  dmnote_source_stamp_at HEAD
}

# dmnote_source_stamp_at <commit>: the app source identity at that commit, so
# a CI build of an older commit counts when the app has not changed since.
dmnote_source_stamp_at() {
  local path
  for path in "${DMNOTE_SOURCE_PATHS[@]}"; do
    git -C "$ELEMENT_PROJECT_ROOT" rev-parse "$1:$path"
  done | shasum -a 256 | cut -d ' ' -f 1
}

# dmnote_worktree_stamp: the app sources as they are on disk, committed or
# not, in the same form as dmnote_source_stamp (equal when nothing is
# uncommitted). The macOS app builds from the working tree, so its stamp is this.
dmnote_worktree_stamp() {
  local stamp index tree path
  stamp="$(dmnote_source_stamp)"
  if [ -n "$stamp" ]; then
    printf '%s\n' "$stamp"
    return 0
  fi
  # A scratch copy of git's index: its file cache means only changed files are hashed.
  index="$(mktemp -d)"
  cp "$(git -C "$ELEMENT_PROJECT_ROOT" rev-parse --absolute-git-dir)/index" "$index/index"
  if ! tree="$(cd "$ELEMENT_PROJECT_ROOT" &&
    GIT_INDEX_FILE="$index/index" git add -A -- "${DMNOTE_SOURCE_PATHS[@]}" &&
    GIT_INDEX_FILE="$index/index" git write-tree)"; then
    rm -rf "$index"
    return 1
  fi
  rm -rf "$index"
  for path in "${DMNOTE_SOURCE_PATHS[@]}"; do
    git -C "$ELEMENT_PROJECT_ROOT" rev-parse "$tree:$path"
  done | shasum -a 256 | cut -d ' ' -f 1
}

# dmnote_build_current <build> <stamp file> <stamp>
dmnote_build_current() {
  [ -n "$3" ] && [ -e "$1" ] && [ -f "$2" ] && [ "$(cat "$2")" = "$3" ]
}

# ensure_dmnote_macos: a macOS app built from the app sources as they are now.
# The last build is reused until they change; ELEMENT_DMNOTE_MAC_APP
# is used as it is.
ensure_dmnote_macos() {
  local stamp
  if [ -n "${ELEMENT_DMNOTE_MAC_APP:-}" ]; then
    require_dir "$DMNOTE_MAC_APP"
    printf 'Using the macOS ElementNote at %s\n' "$DMNOTE_MAC_APP"
    return 0
  fi
  stamp="$(dmnote_worktree_stamp)"
  if dmnote_build_current "$DMNOTE_MAC_APP" "$DMNOTE_MAC_STAMP" "$stamp"; then
    printf 'The macOS ElementNote is already built from the current sources: %s\n' "$DMNOTE_MAC_APP"
    return 0
  fi
  is_macos || fail "The macOS ElementNote is built with Xcode tools. Run this on macOS, or set ELEMENT_DMNOTE_MAC_APP to a built ElementNote.app."
  require_command npm
  printf 'Building the macOS ElementNote: its sources changed since the last build. This takes a few minutes.\n'
  (cd "$ELEMENT_PROJECT_ROOT" && npm run desktop:build:mac)
  require_dir "$DMNOTE_MAC_APP"
  # The stamp is from before the build, so edits made meanwhile build again.
  printf '%s\n' "$stamp" > "$DMNOTE_MAC_STAMP"
}

# An archive passed in explicitly (for example from CD) is trusted.
dmnote_windows_current() {
  [ -n "${ELEMENT_DMNOTE_WINDOWS_ZIP:-}" ] && [ -f "$ELEMENT_DMNOTE_WINDOWS_ZIP" ] && return 0
  dmnote_build_current "$(dmnote_windows_zip)" "$(dmnote_windows_zip).sources" "$(dmnote_source_stamp)"
}

# stage_dmnote_macos <mod folder>: ditto keeps the executable bit and code
# signature through the zip; the mod unpacks it with ditto as well. An
# unchanged app keeps its old zip, so the mod does not install it again.
stage_dmnote_macos() {
  local destination="$1/ElementNote/macos" temporary
  require_command ditto
  require_dir "$DMNOTE_MAC_APP"
  mkdir -p "$destination"
  temporary="$(mktemp "$destination/.ElementNote.app.zip.XXXXXX")"
  ditto -c -k --keepParent "$DMNOTE_MAC_APP" "$temporary"
  if cmp -s "$temporary" "$destination/ElementNote.app.zip"; then
    rm -f "$temporary"
  else
    chmod 644 "$temporary"
    mv -f "$temporary" "$destination/ElementNote.app.zip"
  fi
}

# stage_dmnote_windows <mod folder>: the portable archive holds one folder
# with element-note.exe and its notices.
stage_dmnote_windows() {
  local destination="$1/ElementNote/windows" archive unpacked folder
  archive="$(dmnote_windows_zip)"
  require_command unzip
  require_file "$archive"
  unpacked="$(mktemp -d)"
  # Windows PowerShell's Compress-Archive writes backslash separators; unzip
  # converts them and reports that warning with exit status 1.
  unzip -q "$archive" -d "$unpacked" || [ "$?" -eq 1 ]
  folder="$(dirname "$(find "$unpacked" -name element-note.exe -type f | head -n 1)")"
  if [ ! -f "$folder/element-note.exe" ]; then
    rm -rf "$unpacked"
    fail "The ElementNote archive has no element-note.exe: $archive"
  fi
  mkdir -p "$destination"
  # Kept file times keep the mod's install stamp, so a repeat install of the
  # same app does not copy it again.
  cp -Rp "$folder/." "$destination/"
  rm -rf "$unpacked"
}
