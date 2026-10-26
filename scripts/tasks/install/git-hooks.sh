#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$TASK_DIR/../../.." && pwd)"

git -C "$ROOT" config core.hooksPath scripts/hooks
printf 'Git hooks enabled: pushes that change the mod run ./scripts/run.sh mod-check first.\n'
