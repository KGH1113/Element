#!/usr/bin/env bash
# The Windows in-game viewer library in the build output: which sources it is
# built from, whether the built copy still matches them, and whether that exact
# build passed the desktop smoke probe.

if [ "${IMPL_RESOURCEPACK_WINDOWS_WEBVIEW_LOADED:-0}" = "1" ]; then
  return 0
fi
IMPL_RESOURCEPACK_WINDOWS_WEBVIEW_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"

WINDOWS_WEBVIEW_LIBRARY="$IMPL_RESOURCEPACK_BUILD_OUTPUT/native/ImplGameWebView.dll"
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
  (cd "$IMPL_RESOURCEPACK_PROJECT_ROOT" && shasum -a 256 "${WINDOWS_WEBVIEW_SOURCES[@]}") | shasum -a 256 | cut -d ' ' -f 1
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
