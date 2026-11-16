#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

# ipc-sync <repository> <release-dlls>
run_task "Sync ADOFAI-IPC" node "$TASKS_DIR/sync/adofai-ipc.mjs" "${1:-}" "${2:-}"
