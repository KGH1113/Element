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
  package     Build the release package and metadata
  check       Validate all shell scripts
  desktop-check  Check the ImplDmNote frontend and native backend without installing
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
  package)
    exec "$SCRIPTS_DIR/workflows/package-release.sh"
    ;;
  check)
    exec "$SCRIPTS_DIR/workflows/check-scripts.sh"
    ;;
  desktop-check|desktop-build|desktop-bundle|desktop-render-test)
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
