#!/usr/bin/env bash
# The files a formatting command formats: only those with uncommitted changes,
# so formatting never rewrites code nobody touched.

if [ "${ELEMENT_CHANGED_FILES_LOADED:-0}" = "1" ]; then
  return 0
fi
ELEMENT_CHANGED_FILES_LOADED=1

# changed_files <root> <pathspec>...: files under the repository <root> that
# are staged, unstaged or new and match the git pathspecs, as absolute paths,
# one per line. Deleted files are left out.
changed_files() {
  local root="$1" file
  shift
  {
    git -C "$root" diff --name-only --diff-filter=d HEAD -- "$@"
    git -C "$root" ls-files --others --exclude-standard -- "$@"
  } | sort -u | while IFS= read -r file; do
    printf '%s/%s\n' "$root" "$file"
  done
}
