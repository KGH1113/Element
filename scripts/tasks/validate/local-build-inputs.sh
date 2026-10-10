#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_file "$DOTNET_EXE"
require_dir "$ADOFAI_MANAGED"
require_file "$UNITY_MOD_MANAGER_DLL"
require_file "$HARMONY_DLL"
require_file "$ADOFAI_MANAGED/Assembly-CSharp.dll"
require_file "$ADOFAI_MANAGED/Assembly-CSharp-firstpass.dll"
require_file "$ADOFAI_MANAGED/RDTools.dll"
require_file "$ADOFAI_MANAGED/UnityEngine.CoreModule.dll"
require_file "$ADOFAI_MANAGED/UnityEngine.InputLegacyModule.dll"
require_file "$ADOFAI_MANAGED/SkyHook.Unity.dll"
require_file "$ADOFAI_MANAGED/UnityEngine.AssetBundleModule.dll"
require_file "$ADOFAI_MANAGED/UnityEngine.UI.dll"
require_file "$ADOFAI_MANAGED/UnityEngine.ImageConversionModule.dll"
require_file "$ADOFAI_MANAGED/Unity.TextMeshPro.dll"
require_file "$IMPL_RESOURCEPACK_FONT_ASSETS/Windows/implresourcepackfont"
require_file "$IMPL_RESOURCEPACK_FONT_ASSETS/Linux/implresourcepackfont"
require_file "$IMPL_RESOURCEPACK_FONT_ASSETS/Mac/implresourcepackfont"
require_file "$ADOFAI_IPC_BUNDLE/AdofaiIpc.Contracts.dll"
require_file "$ADOFAI_IPC_BUNDLE/AdofaiIpc.Loader.dll"
