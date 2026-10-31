#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

# publish.sh [--yes]: uploads the packages as a draft, checks the upload, and
# after confirmation publishes it as the latest release the updater reads.
confirmed="${ELEMENT_RELEASE_YES:-0}"
[ "${1:-}" = "--yes" ] && confirmed=1

cd "$ELEMENT_PROJECT_ROOT"
version="$(node "$TASK_DIR/../version/version.mjs" check)"
tag="v$version"
repository="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
assets=("$(package_zip macos)" "$(package_zip windows)" "$ELEMENT_UPDATE_MANIFEST_PATH")
for asset in "${assets[@]}"; do
  require_file "$asset"
done

notes=(--generate-notes)
if [ -f "docs/releases/$version.md" ]; then
  notes=(--notes-file "docs/releases/$version.md")
fi
# GitHub's "latest" skips prereleases, so a beta never reaches the updater.
channel=(--latest)
case "$version" in
  *-*) channel=(--prerelease) ;;
esac

gh release create "$tag" "${assets[@]}" --draft --target "$(git rev-parse HEAD)" \
  --title "Element $version" "${notes[@]}" >/dev/null
printf 'Uploaded draft %s\n' "$tag"

uploaded="$(gh release view "$tag" --json assets --jq '.assets[] | "\(.name) \(.size)"')"
for asset in "${assets[@]}"; do
  expected="$(basename "$asset") $(wc -c < "$asset" | tr -d '[:space:]')"
  grep -Fxq "$expected" <<< "$uploaded" || fail "The draft's $(basename "$asset") does not match the local file. Delete the draft (gh release delete $tag) and run the release again."
done
printf 'Checked %s uploaded assets\n' "${#assets[@]}"

if [ "$confirmed" != "1" ]; then
  printf 'Draft: https://github.com/%s/releases/tag/%s\n' "$repository" "$tag"
  read -r -p "Publish $tag to every player now? [y/N] " answer
  case "$answer" in
    y | Y | yes) ;;
    *)
      printf 'Left %s as a draft. Publish later with: gh release edit %s --draft=false %s\n' "$tag" "$tag" "${channel[0]}"
      exit 0
      ;;
  esac
fi
gh release edit "$tag" --draft=false "${channel[@]}" >/dev/null
printf 'Published %s\n' "$tag"

if [ "${channel[0]}" = "--prerelease" ]; then
  printf 'Prereleases are not offered by the in-game updater; testers install them by hand.\n'
  exit 0
fi
# The URL the updater reads; GitHub can take a moment to point it at the new release.
manifest="https://github.com/$repository/releases/latest/download/Element.update.json"
for _ in 1 2 3 4 5 6 7 8 9 10; do
  if [ "$(curl -fsSL "$manifest" 2>/dev/null | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{try{console.log(JSON.parse(s).version)}catch{}})')" = "$version" ]; then
    printf 'Players now receive %s: %s\n' "$version" "$manifest"
    exit 0
  fi
  sleep 3
done
fail "$tag is published, but $manifest does not report $version yet. Check that $tag is marked Latest on GitHub."
