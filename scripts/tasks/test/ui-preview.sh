#!/usr/bin/env bash
set -euo pipefail
TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$TASK_DIR/../../lib/context.sh"
fixture="$ELEMENT_UNITY_PROJECT/Assets/GeneratedSettingsPreview"
if [[ -e "$fixture" ]]; then
  printf 'A UI preview fixture already exists: %s\n' "$fixture" >&2
  exit 1
fi
# TextMesh Pro writes the glyphs it drew into its fallback font asset; the
# committed one comes back afterwards. Unity's C# project files are not
# committed, so it may rewrite them freely.
fallback="$ELEMENT_UNITY_PROJECT/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"
backup="$(mktemp "${TMPDIR:-/tmp}/element-ui-preview.XXXXXX")"
cp -p "$fallback" "$backup"
cleanup() {
  rm -rf "$fixture" "$fixture.meta"
  cp -p "$backup" "$fallback"
  rm -f "$backup"
}
trap cleanup EXIT
mkdir -p "$fixture/Editor" "$ELEMENT_PROJECT_ROOT/build/ui-preview"
node "$TASK_DIR/../../ui-preview/prepare.mjs" "$ELEMENT_PROJECT_ROOT" "$fixture"
"$UNITY_EXE" -batchmode -quit -projectPath "$ELEMENT_UNITY_PROJECT" \
  -executeMethod Element.Editor.SettingsPreview.RenderAll \
  -logFile "$ELEMENT_PROJECT_ROOT/build/ui-preview/editor.log"
printf 'UI checks and screenshots: %s/build/ui-preview\n' "$ELEMENT_PROJECT_ROOT"
