#!/usr/bin/env bash
set -euo pipefail

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

usage() {
  cat <<'USAGE'
Usage: ./scripts/run.sh <command>

Commands:
  assets      Build Windows, Linux, and macOS font AssetBundles
  build       Build and install the mod
  mod-check   Build and test the mod without installing
  install     Install the already verified mod payload
  package     Build the release package and metadata
  check       Validate all shell scripts
  ipc-sync <repository> <release-dlls>  Pin the local ADOFAI-IPC runtime and SDK
  desktop-native-check  Check/test/clippy native backend without installing
  desktop-message-test  Run message/session application regressions
  desktop-native-format  Format the changed native message boundaries
  desktop-format  Format the changed frontend message boundaries
  desktop-licenses  Generate bundled source and dependency notices
  mod-format  Format the changed C# message boundaries
  desktop-check  Check the ImplDmNote frontend and native backend without installing
  desktop-frontend-check  Verify SDK, types, lint, formatting, tests, and frontend build
  desktop-ipc-format  Format the IPC lifecycle adapter and its regression tests
  desktop-build  Build the ImplDmNote frontend and native backend without installing
  desktop-bundle  Package the native app with its embedded render surface without installing
  desktop-render-test  Run isolated native render fixtures without live inputs
  help        Show this help
USAGE
}

command_name="${1:-help}"
case "$command_name" in
  assets)
    exec "$SCRIPTS_DIR/workflows/build-assets.sh"
    ;;
  build)
    exec "$SCRIPTS_DIR/workflows/build-install.sh"
    ;;
  mod-format)
    exec "$SCRIPTS_DIR/workflows/format-mod.sh"
    ;;
  mod-check)
    exec "$SCRIPTS_DIR/workflows/build-install.sh" check
    ;;
  install)
    exec "$SCRIPTS_DIR/tasks/install/mod.sh"
    ;;
  package)
    exec "$SCRIPTS_DIR/workflows/package-release.sh"
    ;;
  check)
    exec "$SCRIPTS_DIR/workflows/check-scripts.sh"
    ;;
  ipc-sync)
    exec node "$SCRIPTS_DIR/tasks/verify/sync-adofai-ipc.mjs" "${2:-}" "${3:-}"
    ;;
  desktop-check|desktop-frontend-check|desktop-ipc-format|desktop-build|desktop-bundle|desktop-render-test|desktop-message-test|desktop-format|desktop-licenses|desktop-native-check|desktop-native-format)
    exec "$SCRIPTS_DIR/workflows/desktop-render.sh" "$command_name" "${@:2}"
    ;;
  help|-h|--help)
    usage
    ;;
  *)
    printf 'Unknown command: %s\n\n' "$command_name" >&2
    usage >&2
    exit 2
    ;;
esac
