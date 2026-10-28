#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/windows-host.sh
source "$TASK_DIR/../../lib/windows-host.sh"
# shellcheck source=../../lib/windows-webview.sh
source "$TASK_DIR/../../lib/windows-webview.sh"

require_windows_host
for source in "${WINDOWS_WEBVIEW_SOURCES[@]}"; do
  require_file "$IMPL_RESOURCEPACK_PROJECT_ROOT/$source"
done
# Hash first: an edit made during the remote build leaves the stamp stale.
hash="$(windows_webview_source_hash)"

windows_powershell <<PS
New-Item -ItemType Directory -Force (Join-Path \$HOME '$WINDOWS_REMOTE_DIR/src') | Out-Null
PS
(cd "$IMPL_RESOURCEPACK_PROJECT_ROOT" && windows_upload "$WINDOWS_REMOTE_DIR/src" "${WINDOWS_WEBVIEW_SOURCES[@]}")
windows_powershell <<PS
\$dir = Join-Path \$HOME '$WINDOWS_REMOTE_DIR'
& (Join-Path \$dir 'src/build-game-webview.ps1') -Source (Join-Path \$dir 'src') -Output (Join-Path \$dir 'out')
PS
# A new library has not passed the smoke probe yet.
rm -f "$WINDOWS_WEBVIEW_VERIFIED"
windows_download "$WINDOWS_REMOTE_DIR/out/ImplGameWebView.dll" "$WINDOWS_WEBVIEW_LIBRARY"
printf '%s\n' "$hash" >"$WINDOWS_WEBVIEW_STAMP"
printf 'Built Windows game WebView host: %s\n' "$WINDOWS_WEBVIEW_LIBRARY"
