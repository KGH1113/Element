#!/usr/bin/env bash
set -euo pipefail
TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$TASK_DIR/../../lib/context.sh"
fixture="$IMPL_RESOURCEPACK_UNITY_PROJECT/Assets/GeneratedSettingsPreview"
if [[ -e "$fixture" ]]; then
  printf 'A UI preview fixture already exists: %s\n' "$fixture" >&2
  exit 1
fi
backup="$(mktemp -d "${TMPDIR:-/tmp}/impl-ui-preview.XXXXXX")"
preserved=(
  "Assembly-CSharp-Editor.csproj"
  "Assembly-CSharp.csproj"
  "ImplResourcePack.Unity.slnx"
  "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"
)
for i in "${!preserved[@]}"; do
  file="$IMPL_RESOURCEPACK_UNITY_PROJECT/${preserved[$i]}"
  if [[ -f "$file" ]]; then cp -p "$file" "$backup/$i"; fi
done
cleanup() {
  rm -rf "$fixture" "$fixture.meta"
  for i in "${!preserved[@]}"; do
    file="$IMPL_RESOURCEPACK_UNITY_PROJECT/${preserved[$i]}"
    if [[ -f "$backup/$i" ]]; then cp -p "$backup/$i" "$file"; else rm -f "$file"; fi
  done
  rm -rf "$backup"
}
trap cleanup EXIT
mkdir -p "$fixture/Editor" "$IMPL_RESOURCEPACK_PROJECT_ROOT/build/ui-preview"
node "$TASK_DIR/../../ui-preview/prepare.mjs" "$IMPL_RESOURCEPACK_PROJECT_ROOT" "$fixture"
"$UNITY_EXE" -batchmode -quit -projectPath "$IMPL_RESOURCEPACK_UNITY_PROJECT" \
  -executeMethod ImplResourcePack.Editor.SettingsPreview.RenderAll \
  -logFile "$IMPL_RESOURCEPACK_PROJECT_ROOT/build/ui-preview/editor.log"
printf 'UI checks and screenshots: %s/build/ui-preview\n' "$IMPL_RESOURCEPACK_PROJECT_ROOT"
