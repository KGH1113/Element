#!/usr/bin/env bash
set -euo pipefail

# version [x.y.z]: prints the one version the mod and ElementNote share (and
# fails when they differ), or sets it. Its output is only the version.
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"

if [ -n "${1:-}" ]; then
  exec node "$SCRIPTS_DIR/tasks/version/version.mjs" set "$1"
fi
exec node "$SCRIPTS_DIR/tasks/version/version.mjs" check
