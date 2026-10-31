# Installs the Element build next to this script into ADOFAI. Made as
# Install.ps1 by `./scripts/run.sh windows-install`; run it on the Windows PC
# with the game closed:
#   powershell -ExecutionPolicy Bypass -File .\Install.ps1 [-Mods <ADOFAI Mods folder>]
# Settings in the mod folder stay. When the zip has no ElementNote, the copy
# this PC already has is kept, so Auto open/close still opens it.
param([string] $Mods)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Game = 'A Dance of Fire and Ice'
$App = 'ElementNote\windows'
$Exe = 'element-note.exe'

# The Mods folder in any Steam library, so a game on another drive is found too.
function Find-Mods {
  $libraries = @()
  $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
  if ($steam) {
    $libraries += $steam
    $folders = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $folders) {
      foreach ($match in [regex]::Matches((Get-Content $folders -Raw), '"path"\s+"([^"]+)"')) {
        $libraries += $match.Groups[1].Value.Replace('\\', '\')
      }
    }
  }
  if (${env:ProgramFiles(x86)}) { $libraries += Join-Path ${env:ProgramFiles(x86)} 'Steam' }
  foreach ($library in $libraries) {
    $candidate = Join-Path $library "steamapps\common\$Game\Mods"
    if (Test-Path $candidate) { return $candidate }
  }
  throw "ADOFAI's Mods folder was not found. Install Unity Mod Manager into the game first, or pass the folder: .\Install.ps1 -Mods 'D:\SteamLibrary\steamapps\common\$Game\Mods'"
}

# The newest ElementNote on this PC: one an earlier install bundled, or the copy
# the mod installed for itself.
function Find-ExistingApp([string] $target) {
  $folders = @((Join-Path $target $App), (Join-Path $env:LOCALAPPDATA 'io.github.kgh1113.element\ElementNote'))
  $versions = Join-Path $target 'Runtime\versions'
  if (Test-Path $versions) {
    $folders += @(Get-ChildItem $versions -Directory | ForEach-Object { Join-Path $_.FullName $App })
  }
  $folders | Where-Object { Test-Path (Join-Path $_ $Exe) } |
    Sort-Object { (Get-Item (Join-Path $_ $Exe)).LastWriteTimeUtc } -Descending |
    Select-Object -First 1
}

if (Get-Process $Game -ErrorAction SilentlyContinue) {
  throw 'Close ADOFAI first: the running game keeps the mod files locked.'
}
if ($Mods) {
  if (-not (Test-Path $Mods)) { throw "The Mods folder was not found: $Mods" }
} else {
  $Mods = Find-Mods
}

$source = Join-Path $PSScriptRoot 'Element'
if (-not (Test-Path (Join-Path $source 'Info.json'))) {
  throw "Unzip the whole archive first: the Element folder is missing next to this script ($PSScriptRoot)."
}
$version = (Get-Content (Join-Path $source 'Info.json') -Raw | ConvertFrom-Json).Version
$runtime = "Runtime\versions\$version"
$target = Join-Path $Mods 'Element'
$bundled = Test-Path (Join-Path $source "$runtime\$App\$Exe")

# The kept app waits outside the mod folder while its Runtime folder is replaced.
$existing = $null
$kept = $null
if (-not $bundled) {
  $existing = Find-ExistingApp $target
  if ($existing) {
    $kept = Join-Path ([IO.Path]::GetTempPath()) ('ElementNote-' + [guid]::NewGuid())
    Copy-Item $existing $kept -Recurse
  }
}
try {
  New-Item -ItemType Directory -Force $target | Out-Null
  # The pre-launcher root payload and earlier versions: this install starts fresh.
  foreach ($old in 'Element.dll', 'Element.pdb', 'native', 'Assets', 'ElementNote', 'Runtime') {
    $path = Join-Path $target $old
    if (Test-Path $path) { Remove-Item -Recurse -Force $path }
  }
  # Overwrite the payload in place; settings files stay untouched.
  Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
  if ($kept) {
    $destination = Join-Path $target "$runtime\$App"
    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    Copy-Item $kept $destination -Recurse
  }
} finally {
  if ($kept -and (Test-Path $kept)) { Remove-Item -Recurse -Force $kept }
}
# A zip downloaded from the internet marks every file; Windows would warn
# before starting ElementNote.
Get-ChildItem $target -Recurse -File | Unblock-File

Write-Output "Installed Element $version to $target"
if ($bundled) {
  Write-Output 'ElementNote: installed the copy in this zip.'
} elseif ($kept) {
  Write-Output "ElementNote: kept the one already on this PC ($existing)."
} else {
  Write-Warning 'ElementNote is not in this zip or on this PC, so Auto open/close cannot open it. Make the zip again once CD has built ElementNote.'
}
Write-Output 'Start ADOFAI to load it.'
