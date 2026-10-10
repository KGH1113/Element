#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"

configuration="${1:-Debug}"

bash "$TASK_DIR/trackpad.sh"

DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
  "$DOTNET_EXE" build "$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/ImplResourcePack.csproj" \
    --configuration "$configuration" \
    -p:OutputPath="$IMPL_RESOURCEPACK_BUILD_OUTPUT/" \
    -p:AdofaiManaged="$ADOFAI_MANAGED" \
    -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL" \
    -p:HarmonyDll="$HARMONY_DLL" \
    -p:AdofaiIpcBundle="$ADOFAI_IPC_BUNDLE"
