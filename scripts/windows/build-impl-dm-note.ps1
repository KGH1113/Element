# Builds the ImplDmNote portable archive on the Windows build host from an
# archive of the committed app sources. Invoked over SSH by
# scripts/tasks/build/dmnote-windows.sh; nothing here touches the desktop.
param(
  [Parameter(Mandatory = $true)] [string] $Sources,
  [Parameter(Mandatory = $true)] [string] $Work,
  [Parameter(Mandatory = $true)] [string] $Output
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Vite 7 needs Node 20.19 or later. A private Node 22 leaves the system Node alone.
function Use-Node {
  $system = Get-Command node -ErrorAction SilentlyContinue
  if ($system) {
    $version = [version]((& node --version).TrimStart('v'))
    if ($version -ge [version]'20.19.0') { return }
  }
  $cache = Join-Path $env:LOCALAPPDATA 'ImplResourcePack\build-cache'
  $node = Join-Path $cache 'node22'
  if (-not (Test-Path (Join-Path $node 'node.exe'))) {
    New-Item -ItemType Directory -Force $cache | Out-Null
    $base = 'https://nodejs.org/dist/latest-v22.x'
    $sums = (Invoke-WebRequest "$base/SHASUMS256.txt" -UseBasicParsing).Content
    $line = ($sums -split "`n") | Where-Object { $_ -match 'node-v22\.[0-9.]+-win-x64\.zip$' } | Select-Object -First 1
    $hash, $name = $line.Trim() -split '\s+'
    $zip = Join-Path $cache $name
    Invoke-WebRequest "$base/$name" -OutFile $zip -UseBasicParsing
    if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $hash.ToUpper()) {
      Remove-Item $zip -Force
      throw 'The Node.js download failed its checksum.'
    }
    $unpacked = Join-Path $cache 'node-unpack'
    if (Test-Path $unpacked) { Remove-Item -Recurse -Force $unpacked }
    Expand-Archive $zip -DestinationPath $unpacked
    Move-Item (Get-ChildItem $unpacked -Directory | Select-Object -First 1).FullName $node
    Remove-Item -Recurse -Force $unpacked, $zip
  }
  $env:PATH = "$node;$env:PATH"
}

# Native tools write progress to stderr; only the exit code means failure.
function Invoke-Step([string] $Name, [string] $Command) {
  "== $Name"
  cmd /c "$Command 2>&1" | Select-Object -Last 30
  if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

# The ASIO audio backend generates bindings with bindgen, which loads libclang.
function Use-Libclang {
  if ($env:LIBCLANG_PATH -and (Test-Path (Join-Path $env:LIBCLANG_PATH 'libclang.dll'))) { return }
  $llvm = Join-Path $env:ProgramFiles 'LLVM\bin'
  if (Test-Path (Join-Path $llvm 'libclang.dll')) {
    $env:LIBCLANG_PATH = $llvm
    return
  }
  throw 'ImplDmNote builds its ASIO audio backend with LLVM, which this PC does not have. Install it once (winget install LLVM.LLVM) or set LIBCLANG_PATH, then run the build again.'
}

Use-Node
Use-Libclang
$env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"
"node $(node --version)"

# Fresh sources every time; only the Cargo target folder survives so release
# builds stay incremental.
$source = Join-Path $Work 'src'
$target = Join-Path $source 'apps\impl-dm-note\src-tauri\target'
$keptTarget = Join-Path $Work 'cargo-target'
if (Test-Path $target) {
  if (Test-Path $keptTarget) { Remove-Item -Recurse -Force $keptTarget }
  Move-Item $target $keptTarget
}
if (Test-Path $source) { Remove-Item -Recurse -Force $source }
New-Item -ItemType Directory -Force $source | Out-Null
tar -xf $Sources -C $source
if ($LASTEXITCODE -ne 0) { throw "Unpacking the app sources failed with exit code $LASTEXITCODE." }
if (Test-Path $keptTarget) { Move-Item $keptTarget $target }

Set-Location $source
Invoke-Step 'npm ci' 'npm ci --no-audit --no-fund'
Invoke-Step 'desktop:build:windows' 'npm run desktop:build:windows'

$built = Get-ChildItem (Join-Path $source 'apps\impl-dm-note\dist\portable') -Filter '*-windows-x64-portable.zip' |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $built) { throw 'The build finished without a portable archive.' }
New-Item -ItemType Directory -Force $Output | Out-Null
Copy-Item $built.FullName (Join-Path $Output 'impl-dm-note-portable.zip') -Force
"Built $($built.Name) ($([math]::Round($built.Length / 1MB, 1)) MB)"
