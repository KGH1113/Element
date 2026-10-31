#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

# A release is the merged main branch, unchanged, at a version not yet released.
# The in-game updater downloads from this repository's releases.
UPDATER_REPOSITORY="KGH1113/Element"

for command in git gh node curl; do
  require_command "$command"
done
gh auth status >/dev/null 2>&1 || fail "gh is not signed in. Run: gh auth login"
repository="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
[ "$repository" = "$UPDATER_REPOSITORY" ] \
  || fail "This checkout publishes to $repository, but the in-game updater reads $UPDATER_REPOSITORY (Element.Updater/UpdateSource.cs)."

cd "$ELEMENT_PROJECT_ROOT"
branch="$(git rev-parse --abbrev-ref HEAD)"
[ "$branch" = "main" ] || fail "Releases are made from main (now on $branch). Merge dev into main through a pull request, then run: git switch main && git pull"
[ -z "$(git status --porcelain)" ] || fail "The working tree has changes. Commit or stash them so the release matches main exactly."
git fetch --quiet origin main
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] \
  || fail "Local main differs from origin/main. Run: git pull --ff-only (and push anything that should be released)."

version="$(node "$TASK_DIR/../version/version.mjs" check)"
tag="v$version"
if git rev-parse -q --verify "refs/tags/$tag" >/dev/null || git ls-remote --exit-code --tags origin "refs/tags/$tag" >/dev/null 2>&1; then
  fail "$tag already exists. Bump the version on dev (./scripts/run.sh version <x.y.z>) and merge it to main."
fi
if gh release view "$tag" >/dev/null 2>&1; then
  fail "A release (or draft) for $tag already exists. Delete the draft (gh release delete $tag) or bump the version."
fi

# Every release package carries every part; local shortcuts would ship less.
if [ -f "$ELEMENT_PROJECT_ROOT/.env" ]; then
  overrides="$(grep -oE '^[[:space:]]*(ELEMENT_(SKIP_DMNOTE|SKIP_WINDOWS_WEBVIEW|PACKAGE_PLATFORMS|DMNOTE_WINDOWS_ZIP|DMNOTE_MAC_APP))=' "$ELEMENT_PROJECT_ROOT/.env" | tr -d ' =' || true)"
  [ -z "$overrides" ] || fail "Comment out these .env settings for a release; it always ships both OSes with ElementNote: ${overrides//$'\n'/ }"
fi
for variable in ELEMENT_SKIP_DMNOTE ELEMENT_SKIP_WINDOWS_WEBVIEW; do
  [ "${!variable:-0}" != "1" ] || fail "Unset $variable for a release."
done
[ "$ELEMENT_PACKAGE_PLATFORMS" = "macos windows" ] || fail "Unset ELEMENT_PACKAGE_PLATFORMS for a release."

printf 'Releasing %s from %s\n' "$tag" "$(git rev-parse --short HEAD)"
