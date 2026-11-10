#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

# The profile open and save windows. Windows calls comdlg32 from C#.
if ! is_macos; then
  exit 0
fi

require_command xcrun
require_command codesign
require_file "$ELEMENT_PROJECT_ROOT/native/macos/FileDialog.mm"

output_dir="$ELEMENT_BUILD_OUTPUT/native"
output="$output_dir/libElementFileDialog.dylib"
mkdir -p "$output_dir"
# A running game may have the previous build mapped; replace it by rename.
temporary="$(mktemp "$output_dir/.libElementFileDialog.dylib.XXXXXX")"
trap 'rm -f "$temporary"' EXIT

xcrun clang++ -std=c++17 -fobjc-arc -fvisibility=hidden -dynamiclib \
  -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -O2 -Wall -Wextra -Werror \
  -framework AppKit -framework UniformTypeIdentifiers \
  -Wl,-install_name,@rpath/libElementFileDialog.dylib \
  "$ELEMENT_PROJECT_ROOT/native/macos/FileDialog.mm" \
  -o "$temporary"
codesign --force --sign - "$temporary"
mv -f "$temporary" "$output"
