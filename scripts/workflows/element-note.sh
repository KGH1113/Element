#!/usr/bin/env bash
set -euo pipefail

# element-note.sh <desktop-command> [args...]: every ElementNote command of
# run.sh (apps/element-note). Frontend steps are npm scripts; where the root
# package.json names one (desktop:check, desktop:test), this runs that name,
# so npm and run.sh do the same thing.
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
REPOSITORY="$(cd "$SCRIPTS_DIR/.." && pwd)"
APP="$REPOSITORY/apps/element-note"
CARGO_MANIFEST=(--manifest-path src-tauri/Cargo.toml)
# shellcheck source=../lib/changed-files.sh
source "$SCRIPTS_DIR/lib/changed-files.sh"
cd "$APP"

root_npm() {
  (cd "$REPOSITORY" && npm run "$@")
}

native_check() {
  cargo check --offline "${CARGO_MANIFEST[@]}"
  cargo test --offline "${CARGO_MANIFEST[@]}"
  cargo clippy --offline "${CARGO_MANIFEST[@]}" --all-targets -- -D warnings
}

format_frontend() {
  npm exec -- prettier --write "$@"
}

format_native() {
  rustfmt --edition 2021 "$@"
}

# format_changed <what> <formatter> <pathspec...>: runs the formatter on the
# files with uncommitted changes that match the pathspecs.
format_changed() {
  local what="$1" formatter="$2" files=() file
  shift 2
  while IFS= read -r file; do
    files+=("$file")
  done < <(changed_files "$REPOSITORY" "$@")
  if [ "${#files[@]}" -eq 0 ]; then
    printf 'No changed %s files to format.\n' "$what"
    return 0
  fi
  "$formatter" "${files[@]}"
}

command_name="${1:-}"
shift || true
case "$command_name" in
  desktop-deps)
    npm ci --no-audit --no-fund
    cargo fetch "${CARGO_MANIFEST[@]}"
    ;;
  desktop-check)
    root_npm desktop:check
    native_check
    ;;
  desktop-frontend-check)
    root_npm desktop:check
    npm run build
    ;;
  desktop-native-check)
    case "${1:-}" in
      --check-only) cargo check --offline "${CARGO_MANIFEST[@]}" ;;
      --clippy-only) cargo clippy --offline "${CARGO_MANIFEST[@]}" --all-targets -- -D warnings ;;
      *) native_check ;;
    esac
    ;;
  desktop-native-test)
    cargo test --offline "${CARGO_MANIFEST[@]}" "$@"
    ;;
  desktop-test)
    if [ "$#" -eq 0 ]; then
      root_npm desktop:test
    else
      npm exec -- vitest run "$@"
    fi
    ;;
  desktop-message-test)
    # The code that carries messages between ElementNote, its native side and
    # Element: the bridge API, the application services, IPC, the native
    # viewers, and every IPC coordinator.
    npm exec -- vitest run src/renderer/api src/renderer/application src/renderer/infrastructure/ipc \
      src/renderer/ipc src/renderer/nativeViewers IpcCoordinator
    ;;
  desktop-visual-check)
    npm run test:visual
    ;;
  desktop-format | desktop-ipc-format)
    if [ "$#" -gt 0 ]; then
      format_frontend "$@"
    else
      format_changed frontend format_frontend \
        ':(glob)apps/element-note/src/**/*.'{ts,tsx,css,json,js,mjs,cjs} \
        ':(glob)apps/element-note/scripts/**/*.'{js,mjs,cjs}
    fi
    ;;
  desktop-native-format)
    if [ "$#" -gt 0 ]; then
      format_native "$@"
    else
      format_changed Rust format_native ':(glob)apps/element-note/src-tauri/**/*.rs'
    fi
    ;;
  desktop-licenses)
    npm run licenses:generate
    ;;
  desktop-build)
    npm run build
    cargo build --offline "${CARGO_MANIFEST[@]}"
    ;;
  desktop-bundle)
    if [ "$#" -gt 1 ]; then
      printf 'Usage: ./scripts/run.sh desktop-bundle [--debug|--release]\n' >&2
      exit 2
    fi
    bundle_profile=debug
    case "${1:-}" in
      "" | --debug) ;;
      --release) bundle_profile=release ;;
      *)
        printf 'Usage: ./scripts/run.sh desktop-bundle [--debug|--release]\n' >&2
        exit 2
        ;;
    esac
    tauri_args=(--bundles app)
    if [ "$bundle_profile" = debug ]; then
      tauri_args+=(--debug)
    fi
    npm exec -- tauri build "${tauri_args[@]}"
    if [[ "$(uname -s)" == Darwin ]]; then
      if [ "$bundle_profile" = debug ]; then
        node scripts/sign-macos-app.mjs --debug
      else
        node scripts/sign-macos-app.mjs
      fi
      mkdir -p "$REPOSITORY/Release"
      ditto -c -k --sequesterRsrc --keepParent "src-tauri/target/$bundle_profile/bundle/macos/ElementNote.app" \
        "$REPOSITORY/Release/ElementNote-Renderer-macos-$(uname -m).zip"
    fi
    ;;
  desktop-render-test)
    node scripts/verify-render-native.mjs
    ;;
  *)
    printf 'Unknown ElementNote command: %s\n' "$command_name" >&2
    exit 2
    ;;
esac
