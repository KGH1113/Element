#!/usr/bin/env bash
# The Windows in-game viewer library in the build output: which sources it is
# built from, whether the built copy still matches them, and whether that exact
# build passed the desktop smoke probe.

if [ "${ELEMENT_WINDOWS_WEBVIEW_LOADED:-0}" = "1" ]; then
  return 0
fi
ELEMENT_WINDOWS_WEBVIEW_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"

WINDOWS_WEBVIEW_LIBRARY="$ELEMENT_BUILD_OUTPUT/native/ElementGameWebView.dll"
WINDOWS_WEBVIEW_STAMP="$WINDOWS_WEBVIEW_LIBRARY.sources"
WINDOWS_WEBVIEW_VERIFIED="$WINDOWS_WEBVIEW_LIBRARY.verified"
WINDOWS_WEBVIEW_SOURCES=(
  native/game-webview/GameWebView.h
  native/game-webview/ViewerScripts.h
  native/game-webview/GameWebViewWin.cpp
  native/game-webview/GameWebViewWinSmoke.cpp
  scripts/windows/build-game-webview.ps1
  scripts/windows/run-on-desktop.ps1
)
export WINDOWS_WEBVIEW_LIBRARY WINDOWS_WEBVIEW_STAMP WINDOWS_WEBVIEW_VERIFIED

windows_webview_source_hash() {
  (cd "$ELEMENT_PROJECT_ROOT" && shasum -a 256 "${WINDOWS_WEBVIEW_SOURCES[@]}") | shasum -a 256 | cut -d ' ' -f 1
}

# windows_webview_source_stamp_at <commit>: the sources at that commit, by git
# object, so a CD build matches whatever line endings its checkout used.
windows_webview_source_stamp_at() {
  local path
  for path in "${WINDOWS_WEBVIEW_SOURCES[@]}"; do
    git -C "$ELEMENT_PROJECT_ROOT" rev-parse "$1:$path"
  done | shasum -a 256 | cut -d ' ' -f 1
}

# True when the sources have no uncommitted changes, so a CD build of HEAD is
# a build of them.
windows_webview_committed() {
  [ -z "$(git -C "$ELEMENT_PROJECT_ROOT" status --porcelain -- "${WINDOWS_WEBVIEW_SOURCES[@]}")" ]
}

# windows_webview_adopt <library>: takes a library CD built from the current
# sources and passed the smoke probe as this build's.
windows_webview_adopt() {
  local temporary
  mkdir -p "$(dirname "$WINDOWS_WEBVIEW_LIBRARY")"
  temporary="$(mktemp "$(dirname "$WINDOWS_WEBVIEW_LIBRARY")/.ElementGameWebView.dll.XXXXXX")"
  cp "$1" "$temporary"
  mv -f "$temporary" "$WINDOWS_WEBVIEW_LIBRARY"
  windows_webview_source_hash > "$WINDOWS_WEBVIEW_STAMP"
  cp "$WINDOWS_WEBVIEW_STAMP" "$WINDOWS_WEBVIEW_VERIFIED"
}

# True when the built Windows library matches the current sources.
windows_webview_ready() {
  [ -f "$WINDOWS_WEBVIEW_LIBRARY" ] && [ -f "$WINDOWS_WEBVIEW_STAMP" ] &&
    [ "$(cat "$WINDOWS_WEBVIEW_STAMP")" = "$(windows_webview_source_hash)" ]
}

# True when the current build also passed the smoke probe. Release packages
# require this; local Windows installs for testing need only a current build.
windows_webview_verified() {
  windows_webview_ready && [ -f "$WINDOWS_WEBVIEW_VERIFIED" ] &&
    [ "$(cat "$WINDOWS_WEBVIEW_VERIFIED")" = "$(cat "$WINDOWS_WEBVIEW_STAMP")" ]
}
