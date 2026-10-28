#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

run_task "Validate local build inputs" "$TASKS_DIR/validate/local-build-inputs.sh"
run_task "Verify AdofaiIpc dependency" "$TASKS_DIR/verify/adofai-ipc.sh"
# Release by default: a Debug assembly disables JIT optimizations inside the game.
# Set IMPL_RESOURCEPACK_CONFIGURATION=Debug to step through the mod in a debugger.
configuration="${IMPL_RESOURCEPACK_CONFIGURATION:-Release}"
run_task "Build mod and native libraries ($configuration)" "$TASKS_DIR/build/mod.sh" "$configuration"
run_task "Run C# tests" "$TASKS_DIR/test/csharp.sh"
if [ "${1:-install}" = "check" ]; then
  exit 0
fi
# The rest of the installed mod: ImplDmNote, built after the tests pass.
run_task "Build ImplDmNote" "$TASKS_DIR/build/dmnote.sh"
run_task "Install mod and ImplDmNote" "$TASKS_DIR/install/mod.sh"
