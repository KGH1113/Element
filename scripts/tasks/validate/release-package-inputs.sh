#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command zip
require_command unzip
require_command node
node "$TASK_DIR/../version/version.mjs" check >/dev/null
for platform in $ELEMENT_PACKAGE_PLATFORMS; do
  case "$platform" in
    macos | windows) ;;
    *) fail "Unknown package platform: $platform (use macos and/or windows)." ;;
  esac
done
# The macOS archive needs its native libraries and app, built with Xcode tools.
is_macos || fail "Build release packages on macOS; the Windows parts come from the Windows host."
require_command ditto
"$TASK_DIR/local-build-inputs.sh"

