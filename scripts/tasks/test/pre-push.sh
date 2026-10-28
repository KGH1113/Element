#!/usr/bin/env bash
# Builds and tests the mod before a push that changes it. The mod compiles
# against the game's assemblies, which cannot be published, so this local hook
# stands in for CI. Skip it once with `git push --no-verify`.
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$TASK_DIR/../../.." && pwd)"
ZERO=0000000000000000000000000000000000000000
MOD_PATHS='^(ImplResourcePack/|ImplResourcePack\.(Tests|Launcher|Updater)/|ImplResourcePack\.Unity/Assets/|native/|scripts/|vendor/adofai-ipc-runtime/)'

changed_files() {
  local local_sha="$1" remote_sha="$2" base
  if [ "$remote_sha" != "$ZERO" ] && git cat-file -e "$remote_sha" 2>/dev/null; then
    git diff --name-only "$remote_sha" "$local_sha"
    return
  fi
  # A new branch: compare with where it left main, or check everything.
  base="$(git merge-base "$local_sha" origin/main 2>/dev/null || true)"
  if [ -n "$base" ]; then
    git diff --name-only "$base" "$local_sha"
  else
    printf 'ImplResourcePack/\n'
  fi
}

needs_check=false
while read -r _local_ref local_sha _remote_ref remote_sha; do
  [ "$local_sha" = "$ZERO" ] && continue
  if changed_files "$local_sha" "$remote_sha" | grep -Eq "$MOD_PATHS"; then
    needs_check=true
  fi
done

if [ "$needs_check" = true ]; then
  printf 'pre-push: the mod changed; running ./scripts/run.sh mod-check\n'
  "$ROOT/scripts/run.sh" mod-check
fi
