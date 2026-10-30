#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/windows-host.sh
source "$TASK_DIR/../../lib/windows-host.sh"
# shellcheck source=../../lib/windows-webview.sh
source "$TASK_DIR/../../lib/windows-webview.sh"

# The release package serves Windows players too; never ship a stale or
# unverified library. The smoke probe moves the cursor and clicks on the
# Windows desktop, so packaging never starts it on its own.
if windows_webview_verified; then
  printf 'Using the Windows game WebView host that passed the smoke probe for the current sources.\n'
elif [ "${ELEMENT_SKIP_WINDOWS_WEBVIEW:-0}" = "1" ]; then
  printf 'Warning: packaging without the Windows in-game viewer (ELEMENT_SKIP_WINDOWS_WEBVIEW=1).\n' >&2
elif windows_webview_ready; then
  fail "The Windows in-game viewer library has not passed its smoke probe. Run ./scripts/run.sh windows-webview-check (it uses the Windows desktop for about a minute), or set ELEMENT_SKIP_WINDOWS_WEBVIEW=1 to package without it."
else
  fail "The Windows in-game viewer library is missing or older than its sources. Set ELEMENT_WINDOWS_HOST in .env and run ./scripts/run.sh windows-webview-check, or set ELEMENT_SKIP_WINDOWS_WEBVIEW=1 to package without it."
fi
