#!/usr/bin/env bash
set -euo pipefail
TASK_REPOSITORY="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TASK_DESKTOP="$TASK_REPOSITORY/apps/impl-dm-note"
cd "$TASK_DESKTOP"
case "${1:-}" in
  desktop-check)
    npm run type-check
    npm run test
    cargo check --offline --manifest-path src-tauri/Cargo.toml
    ;;
  desktop-build)
    npm run build
    cargo build --offline --manifest-path src-tauri/Cargo.toml
    ;;
  desktop-bundle)
    npm exec -- tauri build --debug --bundles app
    if [[ "$(uname -s)" == Darwin ]]; then
      node scripts/sign-macos-app.mjs --debug
      mkdir -p "$TASK_REPOSITORY/Release"
      ditto -c -k --sequesterRsrc --keepParent src-tauri/target/debug/bundle/macos/ImplDmNote.app "$TASK_REPOSITORY/Release/ImplDmNote-Renderer-macos-$(uname -m).zip"
    fi
    ;;
  desktop-render-test)
    node scripts/verify-render-native.mjs
    ;;
  *) exit 2 ;;
esac
