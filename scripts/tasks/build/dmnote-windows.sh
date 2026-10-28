#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/windows-host.sh
source "$TASK_DIR/../../lib/windows-host.sh"
# shellcheck source=../../lib/dmnote-app.sh
source "$TASK_DIR/../../lib/dmnote-app.sh"

# Builds the Windows ImplDmNote portable archive on the Windows host from the
# committed app sources and stamps it for `package`. Only the build runs
# there; the desktop, mouse and keyboard stay untouched.
require_windows_host
require_command git
stamp="$(dmnote_source_stamp)"
[ -n "$stamp" ] ||
  fail "Commit the ImplDmNote changes first: the Windows build uses the committed app sources."

# Per worktree when set, so parallel builds on one PC never share files.
remote="${IMPL_RESOURCEPACK_WINDOWS_DMNOTE_DIR:-.impl-resourcepack/impl-dm-note}"
archive="$(dmnote_windows_zip)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
git -C "$IMPL_RESOURCEPACK_PROJECT_ROOT" archive --format=tar -o "$work/sources.tar" HEAD "${DMNOTE_SOURCE_PATHS[@]}"

windows_powershell <<PS
New-Item -ItemType Directory -Force (Join-Path \$HOME '$remote') | Out-Null
PS
windows_upload "$remote" "$work/sources.tar" "$IMPL_RESOURCEPACK_PROJECT_ROOT/scripts/windows/build-impl-dm-note.ps1"
windows_powershell <<PS
\$dir = Join-Path \$HOME '$remote'
& (Join-Path \$dir 'build-impl-dm-note.ps1') -Sources (Join-Path \$dir 'sources.tar') -Work \$dir -Output (Join-Path \$dir 'out')
PS
windows_download "$remote/out/impl-dm-note-portable.zip" "$archive"
printf '%s\n' "$stamp" >"$archive.sources"
printf 'Built Windows ImplDmNote: %s\n' "$archive"
