#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

# build: mod-check, then the ElementNote the install carries, then install.
"$WORKFLOW_DIR/mod-check.sh"
run_task "Build ElementNote" "$TASKS_DIR/build/element-note.sh"
"$WORKFLOW_DIR/install.sh"
