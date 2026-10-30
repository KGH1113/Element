#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"

if ! is_macos; then
  exit 0
fi

mkdir -p "$ELEMENT_BUILD_OUTPUT"
xcrun clang++ -std=c++17 -fobjc-arc -fvisibility=hidden -dynamiclib \
  -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -Wall -Wextra -Werror \
  -framework AppKit "$ELEMENT_PROJECT_ROOT/native/macos/Trackpad.mm" \
  -o "$ELEMENT_BUILD_OUTPUT/libElementTrackpad.dylib"
codesign --force --sign - "$ELEMENT_BUILD_OUTPUT/libElementTrackpad.dylib"
