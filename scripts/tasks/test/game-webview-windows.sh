#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/windows-host.sh
source "$TASK_DIR/../../lib/windows-host.sh"

bash "$TASK_DIR/../build/game-webview-windows.sh"

# The probe stands in for ADOFAI: a UnityWndClass window with a flip-model swap
# chain and a loopback OBS fixture. It checks composed screen pixels and real
# clicks, so it needs the signed-in desktop for about a minute.
printf 'Running the smoke probe on the Windows desktop (it moves the cursor and clicks inside its own window).\n'
windows_powershell <<PS
\$dir = Join-Path \$HOME '$WINDOWS_REMOTE_DIR'
\$out = Join-Path \$dir 'out'
\$log = Join-Path \$out 'smoke.log'
Remove-Item \$log -ErrorAction SilentlyContinue
\$arguments = '"' + (Join-Path \$out 'ImplGameWebView.dll') + '" "' + \$log + '"'
& (Join-Path \$dir 'src/run-on-desktop.ps1') -Exe (Join-Path \$out 'ImplGameWebViewSmoke.exe') -Arguments \$arguments -TimeoutSeconds 300
\$code = \$LASTEXITCODE
if (Test-Path \$log) { Get-Content \$log -Encoding UTF8 } else { 'The smoke probe wrote no log.' }
exit \$code
PS
