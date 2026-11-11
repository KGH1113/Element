# Runs a program on the signed-in user's desktop and waits for it to exit.
# OpenSSH sessions run in session 0, which has no desktop for WebView2
# composition or screen pixels, so the probe starts through a scheduled task.
param(
  [Parameter(Mandatory = $true)] [string] $Exe,
  [string] $Arguments = '',
  [int] $TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'

$user = (Get-CimInstance Win32_ComputerSystem).UserName
if (-not $user) { throw 'Nobody is signed in to the Windows desktop. Sign in on the build host and retry.' }

$name = 'ElementDesktopProbe'
$action = New-ScheduledTaskAction -Execute $Exe -Argument $Arguments -WorkingDirectory (Split-Path $Exe)
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
  -ExecutionTimeLimit (New-TimeSpan -Seconds $TimeoutSeconds)
Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings -Force | Out-Null
try {
  Start-ScheduledTask -TaskName $name
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds + 15)
  Start-Sleep -Seconds 2
  while ((Get-ScheduledTask -TaskName $name).State -eq 'Running' -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 1
  }
  $result = (Get-ScheduledTaskInfo -TaskName $name).LastTaskResult
}
finally {
  # Unregistering stops a task that is still running, so this also enforces the timeout.
  Unregister-ScheduledTask -TaskName $name -Confirm:$false
}
exit $result
