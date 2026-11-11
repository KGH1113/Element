# Runs the Windows in-game viewer's smoke probe on this desktop and prints its
# log. CI and CD run it on GitHub's Windows runner, whose session has a
# desktop. Over SSH there is none, so scripts/tasks/test/game-webview-windows.sh
# starts the probe through run-on-desktop.ps1 instead.
param(
  [Parameter(Mandatory = $true)] [string] $Build,
  [string] $Shots = '',
  [int] $TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'

$log = Join-Path $Build 'smoke.log'
Remove-Item $log -ErrorAction SilentlyContinue
$arguments = '"' + (Join-Path $Build 'ElementGameWebView.dll') + '" "' + $log + '"'
if ($Shots) {
  New-Item -ItemType Directory -Force $Shots | Out-Null
  $arguments += ' "' + $Shots + '"'
}
$probe = Start-Process (Join-Path $Build 'ElementGameWebViewSmoke.exe') -ArgumentList $arguments -PassThru
# Reading the handle now keeps ExitCode available after the probe exits.
$null = $probe.Handle
$finished = $probe.WaitForExit($TimeoutSeconds * 1000)
if (-not $finished) { $probe.Kill() }
if (Test-Path $log) { Get-Content $log -Encoding UTF8 } else { 'The smoke probe wrote no log.' }
if (-not $finished) { throw "The smoke probe did not finish within $TimeoutSeconds seconds." }
if ($probe.ExitCode -ne 0) { throw "The smoke probe failed with exit code $($probe.ExitCode)." }
'The Windows in-game viewer passed the smoke probe.'
