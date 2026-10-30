#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

if ! is_macos; then
  printf 'Game WebView host is macOS-only; skipping.\n'
  exit 0
fi

require_command xcrun
require_command codesign
require_file "$ELEMENT_PROJECT_ROOT/native/game-webview/GameWebView.mm"

output_dir="$ELEMENT_BUILD_OUTPUT/native"
output="$output_dir/libElementGameWebView.dylib"
mkdir -p "$output_dir"
temporary="$(mktemp "$output_dir/.libElementGameWebView.dylib.XXXXXX")"
trap 'rm -f "$temporary"' EXIT

xcrun clang++ -std=c++17 -fobjc-arc -fvisibility=hidden -dynamiclib \
  -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -O2 -Wall -Wextra -Werror \
  -framework AppKit -framework WebKit -framework QuartzCore \
  -Wl,-install_name,@rpath/libElementGameWebView.dylib \
  "$ELEMENT_PROJECT_ROOT/native/game-webview/GameWebView.mm" \
  -o "$temporary"
codesign --force --sign - "$temporary"
mv -f "$temporary" "$output"
