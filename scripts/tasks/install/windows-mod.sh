#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/windows-host.sh
source "$TASK_DIR/../../lib/windows-host.sh"
# shellcheck source=../../lib/mod-layout.sh
source "$TASK_DIR/../../lib/mod-layout.sh"

require_windows_host
require_command zip
windows_webview_ready || fail "Build the Windows in-game viewer first: ./scripts/run.sh windows-webview-check"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
# A test build: marked as development, so it never replaces itself with a release.
stage_mod "$stage/ImplResourcePack" windows "" development
(cd "$stage" && zip -qr ImplResourcePack.zip ImplResourcePack)

windows_powershell <<PS
New-Item -ItemType Directory -Force (Join-Path \$HOME '$WINDOWS_REMOTE_DIR') | Out-Null
PS
windows_upload "$WINDOWS_REMOTE_DIR" "$stage/ImplResourcePack.zip"
mods="${IMPL_RESOURCEPACK_WINDOWS_MODS_DIR//\'/\'\'}"
windows_powershell <<PS
if (Get-Process 'A Dance of Fire and Ice' -ErrorAction SilentlyContinue) {
  throw 'Close ADOFAI on the Windows PC first: a running game keeps the installed DLLs locked.'
}
\$mods = '$mods'
if (-not (Test-Path \$mods)) { throw "The Mods folder was not found: \$mods. Set IMPL_RESOURCEPACK_WINDOWS_MODS_DIR." }
\$dir = Join-Path \$HOME '$WINDOWS_REMOTE_DIR'
\$unpacked = Join-Path \$dir 'install'
if (Test-Path \$unpacked) { Remove-Item -Recurse -Force \$unpacked }
Expand-Archive (Join-Path \$dir 'ImplResourcePack.zip') -DestinationPath \$unpacked
\$target = Join-Path \$mods 'ImplResourcePack'
New-Item -ItemType Directory -Force \$target | Out-Null
# The pre-launcher root payload, and earlier versions: a test install starts fresh.
foreach (\$legacy in 'ImplResourcePack.dll', 'ImplResourcePack.pdb', 'native', 'Assets', 'ImplDmNote', 'Runtime') {
  \$path = Join-Path \$target \$legacy
  if (Test-Path \$path) { Remove-Item -Recurse -Force \$path }
}
# Overwrite the payload in place; settings files stay untouched.
Copy-Item -Path (Join-Path \$unpacked 'ImplResourcePack/*') -Destination \$target -Recurse -Force
"Installed to \$target (restart ADOFAI to load it)"
PS
