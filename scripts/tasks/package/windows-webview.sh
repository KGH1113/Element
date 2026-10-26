#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/windows-host.sh
source "$TASK_DIR/../../lib/windows-host.sh"
# shellcheck source=../../lib/windows-webview.sh
source "$TASK_DIR/../../lib/windows-webview.sh"

# The release package serves Windows players too; never ship a stale library.
if [ -n "$IMPL_RESOURCEPACK_WINDOWS_HOST" ]; then
  bash "$TASK_DIR/../build/game-webview-windows.sh"
elif windows_webview_ready; then
  printf 'Using the Windows game WebView host built from the current sources.\n'
elif [ "${IMPL_RESOURCEPACK_SKIP_WINDOWS_WEBVIEW:-0}" = "1" ]; then
  printf 'Warning: packaging without the Windows in-game viewer (IMPL_RESOURCEPACK_SKIP_WINDOWS_WEBVIEW=1).\n' >&2
else
  fail "The Windows in-game viewer library is missing or older than its sources. Set IMPL_RESOURCEPACK_WINDOWS_HOST in .env and run ./scripts/run.sh windows-webview-check, or set IMPL_RESOURCEPACK_SKIP_WINDOWS_WEBVIEW=1 to package without it."
fi
