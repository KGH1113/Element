#!/usr/bin/env bash
# The builds CD (.github/workflows/cd.yml) uploads: ElementNote for macOS and
# Windows and the Windows in-game viewer library. Merges to main build them,
# and so does a manual run on any branch. `release` takes main's;
# `windows-install` takes any run made from the same sources.

if [ "${ELEMENT_CD_BUILDS_LOADED:-0}" = "1" ]; then
  return 0
fi
ELEMENT_CD_BUILDS_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"
# shellcheck source=guards.sh
source "${BASH_SOURCE[0]%/*}/guards.sh"
# shellcheck source=dmnote-app.sh
source "${BASH_SOURCE[0]%/*}/dmnote-app.sh"
# shellcheck source=windows-webview.sh
source "${BASH_SOURCE[0]%/*}/windows-webview.sh"

CD_WORKFLOW=cd.yml
CD_MAC_APP=element-note-macos-app
CD_WINDOWS_APP=element-note-windows-portable
CD_WINDOWS_VIEWER=element-game-webview-windows
export CD_WORKFLOW CD_MAC_APP CD_WINDOWS_APP CD_WINDOWS_VIEWER

# release_source_stamp_at <commit>: everything a release takes from CD.
release_source_stamp_at() {
  printf '%s %s\n' "$(dmnote_source_stamp_at "$1")" "$(windows_webview_source_stamp_at "$1")"
}

# cd_has_artifacts <run id> <artifact>...: the run still has all of them
# (artifacts expire after 90 days).
cd_has_artifacts() {
  local run="$1" names name
  shift
  names="$(cd "$ELEMENT_PROJECT_ROOT" && gh api "repos/{owner}/{repo}/actions/runs/$run/artifacts" \
    --jq '.artifacts[] | select(.expired | not) | .name')"
  for name in "$@"; do
    grep -Fxq "$name" <<< "$names" || return 1
  done
}

# cd_find_run <stamp function> <stamp> <branch, or "" for any> <artifact>...:
# the newest run whose commit gives <stamp function> this stamp. Prints
# "ready <id>" when its builds can be downloaded, "running <id>" when it has
# not finished, or nothing.
cd_find_run() {
  local stamp_at="$1" stamp="$2" branch="$3" id sha status conclusion
  local list=(run list --workflow "$CD_WORKFLOW" --limit 30
    --json "databaseId,headSha,status,conclusion"
    --jq '.[] | "\(.databaseId) \(.headSha) \(.status) \(.conclusion)"')
  shift 3
  if [ -n "$branch" ]; then
    list+=(--branch "$branch")
  fi
  while read -r id sha status conclusion; do
    git -C "$ELEMENT_PROJECT_ROOT" cat-file -e "$sha^{commit}" 2>/dev/null || continue
    [ "$("$stamp_at" "$sha")" = "$stamp" ] || continue
    if [ "$status" != "completed" ]; then
      printf 'running %s\n' "$id"
      return 0
    fi
    if [ "$conclusion" = "success" ] && cd_has_artifacts "$id" "$@"; then
      printf 'ready %s\n' "$id"
      return 0
    fi
  done < <(cd "$ELEMENT_PROJECT_ROOT" && gh "${list[@]}")
  return 0
}

# cd_download <run id> <artifact> <directory>
cd_download() {
  (cd "$ELEMENT_PROJECT_ROOT" && gh run download "$1" --name "$2" --dir "$3")
}

# cd_fetch <stamp function> <stamp> <artifact> <file> <destination>: saves
# <file> from the newest CD run (any branch) built from these sources. When
# there is none, says why on stderr and fails.
cd_fetch() {
  local stamp_at="$1" stamp="$2" artifact="$3" file="$4" destination="$5" state run download branch
  if ! command -v gh >/dev/null 2>&1; then
    printf 'Warning: %s comes from CD, which needs the GitHub CLI (gh).\n' "$file" >&2
    return 1
  fi
  read -r state run <<< "$(cd_find_run "$stamp_at" "$stamp" "" "$artifact")" || true
  case "${state:-}" in
    ready) ;;
    running)
      printf 'Warning: CD run %s is still building %s. Run this again when it finishes.\n' "$run" "$file" >&2
      return 1
      ;;
    *)
      branch="$(git -C "$ELEMENT_PROJECT_ROOT" rev-parse --abbrev-ref HEAD)"
      printf 'Warning: CD has not built %s from these committed sources. Merge them to main, or push %s and run: gh workflow run %s --ref %s\n' \
        "$file" "$branch" "$CD_WORKFLOW" "$branch" >&2
      return 1
      ;;
  esac
  download="$(mktemp -d)"
  if ! cd_download "$run" "$artifact" "$download" || [ ! -f "$download/$file" ]; then
    rm -rf "$download"
    printf 'Warning: could not download %s from CD run %s.\n' "$file" "$run" >&2
    return 1
  fi
  mkdir -p "$(dirname "$destination")"
  mv -f "$download/$file" "$destination"
  rm -rf "$download"
  printf 'Downloaded %s from CD run %s\n' "$file" "$run"
}
