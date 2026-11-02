#!/usr/bin/env bash
set -euo pipefail

# Tabub: the sample mod built on ElementKit (docs/elementkit-plan.md). It has
# no overlay of its own; it needs Element with ElementKit installed.

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

project="$ELEMENT_PROJECT_ROOT/Tabub"
# Its own output folder: a shared one would put ElementKit.dll next to it,
# and a mod must never ship its own copy.
output="$project/bin/Release"
destination="$ADOFAI_MODS_DIR/Tabub"
assert_non_root_path "$destination"

DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
  "$DOTNET_EXE" build "$project/Tabub.csproj" \
    --configuration Release \
    -p:AdofaiManaged="$ADOFAI_MANAGED" \
    -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL" \
    -p:HarmonyDll="$HARMONY_DLL"

require_file "$output/Tabub.dll"
require_file "$output/Info.json"
mkdir -p "$destination"
cp "$output/Tabub.dll" "$output/Info.json" "$destination/"
rm -f "$destination/ElementKit.dll"
printf 'Installed Tabub at %s\nIt needs Element with ElementKit: ./scripts/run.sh install\n' "$destination"
