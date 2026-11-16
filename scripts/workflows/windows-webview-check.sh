#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

run_task "Build the Windows game WebView host on the Windows PC" "$TASKS_DIR/build/game-webview-windows.sh"
run_task "Smoke-test the Windows game WebView host" "$TASKS_DIR/test/game-webview-windows.sh"
