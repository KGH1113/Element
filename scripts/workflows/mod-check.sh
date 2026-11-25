#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"

run_task "Check layers, roles and folders" node "$SCRIPTS_DIR/tasks/validate/structure.mjs" "$SCRIPTS_DIR/.."
run_task "Validate local build inputs" "$TASKS_DIR/validate/local-build-inputs.sh"
run_task "Verify AdofaiIpc dependency" "$TASKS_DIR/validate/adofai-ipc.sh"
# Release by default: a Debug assembly disables JIT optimizations inside the game.
# Set ELEMENT_CONFIGURATION=Debug to step through the mod in a debugger.
configuration="${ELEMENT_CONFIGURATION:-Release}"
run_task "Build mod and native libraries ($configuration)" "$TASKS_DIR/build/mod.sh" "$configuration"
run_task "Run C# tests" "$TASKS_DIR/test/csharp.sh"
