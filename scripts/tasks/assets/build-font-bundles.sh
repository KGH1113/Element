#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_file "$UNITY_EXE"
require_dir "$ELEMENT_UNITY_PROJECT"
require_file "$ELEMENT_UNITY_PROJECT/Assets/Font/MAPLESTORY_OTF_BOLD.OTF"
require_file "$ELEMENT_UNITY_PROJECT/Assets/Editor/FontAssetGenerator.cs"
require_file "$ELEMENT_UNITY_PROJECT/Assets/Editor/FontBundleBuilder.cs"

"$UNITY_EXE" \
  -batchmode \
  -quit \
  -projectPath "$ELEMENT_UNITY_PROJECT" \
  -executeMethod Element.Editor.FontBundleBuilder.BuildAll \
  -logFile -
