#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command shasum

version="$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$IMPL_RESOURCEPACK_PROJECT_ROOT/ImplResourcePack/Info.json" | head -n 1)"
[ -n "$version" ] || fail "ImplResourcePack version is missing from Info.json."

mkdir -p "$(dirname "$IMPL_RESOURCEPACK_VERSION_ASSET_PATH")"
printf '%s\n' "$version" > "$IMPL_RESOURCEPACK_VERSION_ASSET_PATH"
printf 'Version asset: %s\n' "$IMPL_RESOURCEPACK_VERSION_ASSET_PATH"

for platform in $IMPL_RESOURCEPACK_PACKAGE_PLATFORMS; do
  archive="$(package_zip "$platform")"
  require_file "$archive"
  # Name only the archive, so the checksum verifies next to it anywhere.
  (cd "$(dirname "$archive")" && shasum -a 256 "$(basename "$archive")") > "$archive.sha256"
  printf 'Checksum asset: %s.sha256\n' "$archive"
done
