#!/usr/bin/env bash
set -euo pipefail
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$WORKFLOW_DIR/../lib/context.sh"
cd "$IMPL_RESOURCEPACK_PROJECT_ROOT"
"$DOTNET_EXE" csharpier format ImplResourcePack/Application/KeyLimiter ImplResourcePack/Domain/DmNote ImplResourcePack/Infrastructure/Ipc ImplResourcePack/Infrastructure/Game/DmNote ImplResourcePack/Infrastructure/Game/Input/DmNoteKeyResolver.cs ImplResourcePack/Bootstrap/ModRuntime.cs ImplResourcePack/Bootstrap/Main.cs ImplResourcePack/Configuration/ImplResourcePackSettings.cs ImplResourcePack.Tests/Program.cs ImplResourcePack.Tests/NativeViewerTests.cs
