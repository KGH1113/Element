# Builds ImplGameWebView.dll and its smoke probe with MSVC on the Windows build host.
# Invoked over SSH by scripts/tasks/build/game-webview-windows.sh.
param(
  [Parameter(Mandatory = $true)] [string] $Source,
  [Parameter(Mandatory = $true)] [string] $Output
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Pinned WebView2 SDK: headers and the static loader linked into the DLL.
$sdkVersion = '1.0.4314.35'
$sdkSha256 = '90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4'
$cache = Join-Path $env:LOCALAPPDATA 'ImplResourcePack\build-cache'
$sdk = Join-Path $cache "webview2-$sdkVersion\build\native"

if (-not (Test-Path (Join-Path $sdk 'include\WebView2.h'))) {
  New-Item -ItemType Directory -Force $cache | Out-Null
  $package = Join-Path $cache "microsoft.web.webview2.$sdkVersion.nupkg"
  $url = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$sdkVersion/microsoft.web.webview2.$sdkVersion.nupkg"
  Invoke-WebRequest $url -OutFile $package -UseBasicParsing
  $actual = (Get-FileHash $package -Algorithm SHA256).Hash
  if ($actual -ne $sdkSha256) {
    Remove-Item $package -Force
    throw "WebView2 SDK checksum mismatch: $actual"
  }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $unpacked = Join-Path $cache "webview2-$sdkVersion"
  if (Test-Path $unpacked) { Remove-Item -Recurse -Force $unpacked }
  [IO.Compression.ZipFile]::ExtractToDirectory($package, $unpacked)
  Remove-Item $package -Force
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = if (Test-Path $vswhere) {
  & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
}
if (-not $vs) { throw 'Install Visual Studio Build Tools with the C++ x64 toolset on the Windows build host.' }

# Import the x64 developer environment into this session.
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
cmd /c "`"$vcvars`" >nul && set" | ForEach-Object {
  if ($_ -match '^([^=]+)=(.*)$') { Set-Item -Path "env:$($Matches[1])" -Value $Matches[2] }
}

function Invoke-Compiler([string[]] $Arguments) {
  # cl reports diagnostics on stdout; keep PowerShell from turning stderr into errors.
  $previous = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  & cl.exe @Arguments 2>&1 | ForEach-Object { "$_" }
  $code = $LASTEXITCODE
  $ErrorActionPreference = $previous
  if ($code -ne 0) { throw "cl.exe failed with exit code $code" }
}

New-Item -ItemType Directory -Force $Output | Out-Null
Push-Location $Output
try {
  $common = @('/nologo', '/std:c++17', '/EHsc', '/O2', '/W4', '/WX', '/MT', '/GS', '/guard:cf',
    '/DUNICODE', '/D_UNICODE', '/external:W0', '/external:I', (Join-Path $sdk 'include'))
  # Static CRT and loader: the DLL depends only on system libraries.
  Invoke-Compiler ($common + @((Join-Path $Source 'GameWebViewWin.cpp'), '/LD', '/Fe:ImplGameWebView.dll',
      '/link', '/NOLOGO', '/GUARD:CF', '/DYNAMICBASE', '/NXCOMPAT', (Join-Path $sdk 'x64\WebView2LoaderStatic.lib'),
      'd3d11.lib', 'd2d1.lib', 'dcomp.lib', 'dxgi.lib', 'ole32.lib', 'user32.lib', 'shell32.lib', 'advapi32.lib'))
  Invoke-Compiler ($common + @((Join-Path $Source 'GameWebViewWinSmoke.cpp'), '/Fe:ImplGameWebViewSmoke.exe',
      '/link', '/NOLOGO', '/SUBSYSTEM:WINDOWS', 'd3d11.lib', 'dxgi.lib', 'user32.lib', 'gdi32.lib', 'ws2_32.lib',
      'ole32.lib', 'shell32.lib'))
}
finally {
  Pop-Location
}
"Built $(Join-Path $Output 'ImplGameWebView.dll') with WebView2 SDK $sdkVersion"
