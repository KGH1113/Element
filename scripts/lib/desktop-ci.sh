#!/usr/bin/env bash
# Desktop CI (.github/workflows/desktop-ci.yml) and the ImplDmNote builds its
# runs upload. Pull requests and pushes to main both build them; `release`
# takes main's, `windows-install` any run made from the same app sources.

if [ "${IMPL_RESOURCEPACK_DESKTOP_CI_LOADED:-0}" = "1" ]; then
  return 0
fi
IMPL_RESOURCEPACK_DESKTOP_CI_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"
# shellcheck source=dmnote-app.sh
source "${BASH_SOURCE[0]%/*}/dmnote-app.sh"

DESKTOP_CI_WORKFLOW=desktop-ci.yml
DESKTOP_CI_MAC_APP=impl-dm-note-macos-app
DESKTOP_CI_WINDOWS_APP=impl-dm-note-windows-portable
export DESKTOP_CI_WORKFLOW DESKTOP_CI_MAC_APP DESKTOP_CI_WINDOWS_APP

# desktop_ci_has_artifacts <run id> <artifact>...: the run still has all of
# them (artifacts expire after 90 days).
desktop_ci_has_artifacts() {
  local run="$1" names name
  shift
  names="$(cd "$IMPL_RESOURCEPACK_PROJECT_ROOT" && gh api "repos/{owner}/{repo}/actions/runs/$run/artifacts" \
    --jq '.artifacts[] | select(.expired | not) | .name')"
  for name in "$@"; do
    grep -Fxq "$name" <<< "$names" || return 1
  done
}

# desktop_ci_find_run <stamp> <branch, or "" for any> <artifact>...: the newest
# run whose commit has these app sources. Prints "ready <id>" when its builds
# can be downloaded, "running <id>" when it has not finished, or nothing.
desktop_ci_find_run() {
  local stamp="$1" branch="$2" id sha status conclusion
  local list=(run list --workflow "$DESKTOP_CI_WORKFLOW" --limit 30
    --json "databaseId,headSha,status,conclusion"
    --jq '.[] | "\(.databaseId) \(.headSha) \(.status) \(.conclusion)"')
  shift 2
  if [ -n "$branch" ]; then
    list+=(--branch "$branch")
  fi
  while read -r id sha status conclusion; do
    git -C "$IMPL_RESOURCEPACK_PROJECT_ROOT" cat-file -e "$sha^{commit}" 2>/dev/null || continue
    [ "$(dmnote_source_stamp_at "$sha")" = "$stamp" ] || continue
    if [ "$status" != "completed" ]; then
      printf 'running %s\n' "$id"
      return 0
    fi
    if [ "$conclusion" = "success" ] && desktop_ci_has_artifacts "$id" "$@"; then
      printf 'ready %s\n' "$id"
      return 0
    fi
  done < <(cd "$IMPL_RESOURCEPACK_PROJECT_ROOT" && gh "${list[@]}")
  return 0
}

# desktop_ci_download <run id> <artifact> <directory>
desktop_ci_download() {
  (cd "$IMPL_RESOURCEPACK_PROJECT_ROOT" && gh run download "$1" --name "$2" --dir "$3")
}
