#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/artifacts.sh
source "$TASK_DIR/../../lib/artifacts.sh"

assert_non_root_path "$IMPL_RESOURCEPACK_PACKAGE_BUILD_ROOT"
assert_non_root_path "$IMPL_RESOURCEPACK_PACKAGE_STAGE"
mkdir -p "$IMPL_RESOURCEPACK_PACKAGE_BUILD_ROOT"
if [ -e "$IMPL_RESOURCEPACK_PACKAGE_STAGE" ]; then
  safe_remove_tree "$IMPL_RESOURCEPACK_PACKAGE_STAGE" "$IMPL_RESOURCEPACK_PACKAGE_BUILD_ROOT"
fi
mkdir -p "$IMPL_RESOURCEPACK_PACKAGE_STAGE"

copy_core_payload "$IMPL_RESOURCEPACK_PACKAGE_STAGE" package
copy_assets "$IMPL_RESOURCEPACK_PACKAGE_STAGE"

