#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

# dev → pull request (CI checks) → main → CD builds ImplDmNote and the Windows
# in-game viewer → this script, on the Mac with the game: builds the mod around
# those builds, packages it and publishes it. See docs/releasing.md.
app_env="$IMPL_RESOURCEPACK_PROJECT_ROOT/build/release/cd-builds.env"
mkdir -p "$(dirname "$app_env")"

run_task "Check main and the version" "$TASKS_DIR/release/preflight.sh"
run_task "Download the CD builds" "$TASKS_DIR/release/cd-builds.sh" "$app_env"
# shellcheck disable=SC1090
source "$app_env"
run_task "Run C# tests" "$TASKS_DIR/test/csharp.sh"
run_task "Package the mod" "$WORKFLOW_DIR/package-release.sh"
run_task "Publish the release" "$TASKS_DIR/release/publish.sh" "${1:-}"
