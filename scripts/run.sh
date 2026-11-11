#!/usr/bin/env bash
set -euo pipefail

# The one entry for builds, checks, installs and releases. Every command takes
# the same path:
#   run.sh                 picks the workflow of the command
#   workflows/<command>.sh the steps of that command, in order
#   tasks/<verb>/<thing>   one job each: build, test, validate, install, package,
#                          release, sync, version, assets (a step may also be one
#                          npm, cargo or dotnet tool command)
#   lib/                   helpers the tasks share
SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKFLOWS_DIR="$SCRIPTS_DIR/workflows"

usage() {
  cat <<'USAGE'
Usage: ./scripts/run.sh <command>

Commands:
  assets      Build Windows, Linux, and macOS font AssetBundles
  build       Build the mod, its native libraries and ElementNote, test, and install it all
  mod-check   Build and test the mod without installing
  ui-check    Render and verify the settings UI in the Unity editor without opening the game
  native-webview-check  Build and verify the macOS game WebView host
  windows-webview-check  Build and smoke-test the Windows game WebView host on ELEMENT_WINDOWS_HOST (releases use CD's)
  windows-install  Make build/Element-windows-install.zip; run its Install.ps1 on the Windows PC (game closed)
  dmnote-windows-build  Build the Windows ElementNote for packages on ELEMENT_WINDOWS_HOST (no desktop input)
  install     Install the already verified mod payload and ElementNote
  package     Build the macOS and Windows release packages and their update manifest
  version [x.y.z]  Check that the mod and ElementNote share one version, or set it
  release [--yes]  On main: build the mod around CD's builds, package and publish (docs/process/releasing.md)
  check       Validate all shell scripts
  hooks       Run mod-check before pushes that change the mod (local CI)
  ipc-sync <repository> <release-dlls>  Pin the local ADOFAI-IPC runtime and SDK
  mod-format [files...]  Format the C# files you changed (or the given ones)
  desktop-deps  Install locked npm dependencies and fetch native dependencies
  desktop-check  Check the ElementNote frontend and native backend without installing
  desktop-frontend-check  Verify SDK, types, lint, formatting, tests, and frontend build
  desktop-native-check [--check-only|--clippy-only]  Check/test/clippy native backend without installing
  desktop-native-test [filter]  Run native tests, optionally filtered by test name
  desktop-test [filter...]  Run frontend unit regressions, optionally filtered
  desktop-message-test  Run the tests of the code that carries messages (API, IPC, viewers)
  desktop-visual-check  Verify the unchanged v1 key-viewer appearance baselines
  desktop-render-test  Run isolated native render fixtures without live inputs
  desktop-format [files...]  Format the frontend files you changed (or the given ones)
  desktop-ipc-format  Same as desktop-format (old name)
  desktop-native-format [files...]  Format the Rust files you changed (or the given ones)
  desktop-licenses  Generate bundled source and dependency notices
  desktop-build  Build the ElementNote frontend and native backend without installing
  desktop-bundle [--release]  Package the native app without installing (default: Debug)
  help        Show this help
USAGE
}

command_name="${1:-help}"
case "$command_name" in
  help | -h | --help)
    usage
    ;;
  assets | build | mod-check | ui-check | native-webview-check | windows-webview-check | windows-install | \
    install | package | version | release | check | hooks | ipc-sync | mod-format)
    exec "$WORKFLOWS_DIR/$command_name.sh" "${@:2}"
    ;;
  dmnote-windows-build)
    exec "$WORKFLOWS_DIR/element-note-windows-build.sh" "${@:2}"
    ;;
  desktop-*)
    exec "$WORKFLOWS_DIR/element-note.sh" "$command_name" "${@:2}"
    ;;
  *)
    printf 'Unknown command: %s\n\n' "$command_name" >&2
    usage >&2
    exit 2
    ;;
esac
