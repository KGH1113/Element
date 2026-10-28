#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command zip
mkdir -p "$IMPL_RESOURCEPACK_PACKAGE_OUTPUT_DIR"

for platform in $IMPL_RESOURCEPACK_PACKAGE_PLATFORMS; do
  stage="$(package_stage "$platform")"
  archive="$(package_zip "$platform")"
  require_dir "$stage"
  rm -f "$archive"
  (
    cd "$(dirname "$stage")"
    zip -qr "$archive" ImplResourcePack -x 'ImplResourcePack/*.log' '*/.DS_Store' '*/.development'
  )
  printf 'Packaged to %s\n' "$archive"
done
