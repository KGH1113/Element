#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/dmnote-app.sh
source "$TASK_DIR/../../lib/dmnote-app.sh"

# The ImplDmNote a local install carries: the macOS app, built again only
# after its sources change. The Windows app comes from CD (windows-install).
if [ "${IMPL_RESOURCEPACK_SKIP_DMNOTE:-0}" = "1" ]; then
  printf 'Skipping ImplDmNote (IMPL_RESOURCEPACK_SKIP_DMNOTE=1).\n'
elif ! is_macos; then
  printf 'Skipping ImplDmNote: a local build makes only the macOS app.\n'
else
  ensure_dmnote_macos
fi
