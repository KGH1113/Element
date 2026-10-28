#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

run_task "Validate release package inputs" "$TASKS_DIR/validate/release-package-inputs.sh"
run_task "Verify AdofaiIpc dependency" "$TASKS_DIR/verify/adofai-ipc.sh"
run_task "Build mod (Release)" "$TASKS_DIR/build/mod.sh" Release
run_task "Prepare Windows game WebView host" "$TASKS_DIR/package/windows-webview.sh"
run_task "Prepare ImplDmNote" "$TASKS_DIR/package/impl-dm-note.sh"
run_task "Stage macOS package" "$TASKS_DIR/package/stage.sh" macos
run_task "Stage Windows package" "$TASKS_DIR/package/stage.sh" windows
run_task "Create package archives" "$TASKS_DIR/package/archive.sh"
run_task "Write release metadata" "$TASKS_DIR/package/write-release-assets.sh"
