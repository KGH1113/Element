#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

# dev → pull request → main → Desktop CI builds ImplDmNote → this script, on
# the Mac with the game: packages the mod around that build and publishes it.
# See docs/releasing.md.
app_env="$IMPL_RESOURCEPACK_PROJECT_ROOT/build/release/ci-app.env"
mkdir -p "$(dirname "$app_env")"

run_task "Check main and the version" "$TASKS_DIR/release/preflight.sh"
run_task "Download ImplDmNote from Desktop CI" "$TASKS_DIR/release/ci-app.sh" "$app_env"
# shellcheck disable=SC1090
source "$app_env"
run_task "Run C# tests" "$TASKS_DIR/test/csharp.sh"
run_task "Package the mod" "$WORKFLOW_DIR/package-release.sh"
run_task "Publish the release" "$TASKS_DIR/release/publish.sh" "${1:-}"
