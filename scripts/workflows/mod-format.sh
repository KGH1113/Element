#!/usr/bin/env bash
set -euo pipefail

# mod-format [files...]: formats the given C# files with CSharpier, or else
# every C# file of the mod's projects with uncommitted changes. Files nobody
# touched stay as they are.
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/changed-files.sh
source "$SCRIPTS_DIR/lib/changed-files.sh"

files=("$@")
if [ "${#files[@]}" -eq 0 ]; then
  while IFS= read -r file; do
    files+=("$file")
  done < <(changed_files "$ELEMENT_PROJECT_ROOT" \
    ':(glob)Element/**/*.cs' ':(glob)Element.*/**/*.cs' ':(glob)ElementKit/**/*.cs')
fi
if [ "${#files[@]}" -eq 0 ]; then
  printf 'No changed C# files to format.\n'
  exit 0
fi
cd "$ELEMENT_PROJECT_ROOT"
"$DOTNET_EXE" csharpier format "${files[@]}"
