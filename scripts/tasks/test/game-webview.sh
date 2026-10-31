#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

bash "$TASK_DIR/../build/game-webview.sh"
if ! is_macos; then
  exit 0
fi

library="$ELEMENT_BUILD_OUTPUT/native/libElementGameWebView.dylib"
architectures="$(xcrun lipo "$library" -archs)"
for architecture in arm64 x86_64; do
  case " $architectures " in
    *" $architecture "*) ;;
    *)
      printf 'Missing %s architecture in %s\n' "$architecture" "$library" >&2
      exit 1
      ;;
  esac
done
codesign --verify --strict "$library"
printf 'Verified universal macOS game WebView host: %s\n' "$library"

smoke_source="$ELEMENT_PROJECT_ROOT/native/game-webview/GameWebViewSmoke.mm"
require_file "$smoke_source"
smoke="$(mktemp "$ELEMENT_BUILD_OUTPUT/native/.GameWebViewSmoke.XXXXXX")"
trap 'rm -f "$smoke"' EXIT
xcrun clang++ -std=c++17 -fobjc-arc -mmacosx-version-min=11.0 -O2 -Wall -Wextra -Werror \
  -framework AppKit -framework WebKit -framework QuartzCore "$smoke_source" -o "$smoke"
"$smoke" "$library"
