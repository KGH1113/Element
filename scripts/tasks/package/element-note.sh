#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/dmnote-app.sh
source "$TASK_DIR/../../lib/dmnote-app.sh"

# Each release archive carries the ElementNote built from the app sources; the
# mod opens it with the game.
if [ "${ELEMENT_SKIP_DMNOTE:-0}" = "1" ]; then
  printf 'Warning: packaging without ElementNote (ELEMENT_SKIP_DMNOTE=1). Players open it themselves.\n' >&2
  exit 0
fi

if packages_platform macos; then
  ensure_dmnote_macos
else
  printf 'Skipping the macOS ElementNote (not packaged in this run).\n'
fi

if ! packages_platform windows; then
  exit 0
elif dmnote_windows_current; then
  printf 'Using the Windows ElementNote build: %s\n' "$(dmnote_windows_zip)"
else
  fail "The Windows ElementNote archive is missing or older than the committed app sources ($(dmnote_windows_zip)). Run ./scripts/run.sh dmnote-windows-build (builds on ELEMENT_WINDOWS_HOST), set ELEMENT_DMNOTE_WINDOWS_ZIP to the portable zip CD built, or set ELEMENT_SKIP_DMNOTE=1 to package without ElementNote."
fi
