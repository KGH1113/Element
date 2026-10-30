#!/usr/bin/env bash

if [ "${ELEMENT_CONTEXT_LOADED:-0}" = "1" ]; then
  return 0
fi
ELEMENT_CONTEXT_LOADED=1

ELEMENT_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ELEMENT_PROJECT_ROOT="$(cd "$ELEMENT_LIB_DIR/../.." && pwd)"

if [ -f "$ELEMENT_PROJECT_ROOT/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$ELEMENT_PROJECT_ROOT/.env"
  set +a
fi

ADOFAI_DIR="${ADOFAI_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
ADOFAI_MODS_DIR="${ADOFAI_MODS_DIR:-$ADOFAI_DIR/Mods}"
ADOFAI_MANAGED="${ADOFAI_MANAGED:-$ADOFAI_DIR/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed}"

DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET_ROOT_ARM64="${DOTNET_ROOT_ARM64:-$DOTNET_ROOT}"
DOTNET_EXE="${DOTNET_EXE:-$DOTNET_ROOT/dotnet}"

UNITY_MOD_MANAGER_DLL="${UNITY_MOD_MANAGER_DLL:-$ADOFAI_MANAGED/UnityModManager/UnityModManager.dll}"
HARMONY_DLL="${HARMONY_DLL:-$ADOFAI_MANAGED/UnityModManager/0Harmony.dll}"
UNITY_EXE="${UNITY_EXE:-/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity}"
ELEMENT_UNITY_PROJECT="$ELEMENT_PROJECT_ROOT/Element.Unity"
ELEMENT_FONT_ASSETS="$ELEMENT_PROJECT_ROOT/Element/Assets/Font"

ELEMENT_BUILD_OUTPUT="${ELEMENT_BUILD_DIR:-$ELEMENT_PROJECT_ROOT/build/Element}"
ELEMENT_BOOTSTRAP_BUILD_OUTPUT="${ELEMENT_BOOTSTRAP_BUILD_DIR:-$ELEMENT_PROJECT_ROOT/build/Element.Bootstrap}"
ELEMENT_INSTALL_PATH="${ELEMENT_INSTALL_DIR:-$ADOFAI_MODS_DIR/Element}"

ELEMENT_PACKAGE_BUILD_ROOT="${ELEMENT_PACKAGE_ROOT:-$ELEMENT_PROJECT_ROOT/build/package}"
# One release archive per OS: Element-macos.zip and -windows.zip.
# ELEMENT_PACKAGE_PLATFORMS=macos packages one OS, e.g. for a test.
ELEMENT_PACKAGE_OUTPUT_DIR="${ELEMENT_PACKAGE_DIR:-$ELEMENT_PROJECT_ROOT/build}"
ELEMENT_PACKAGE_PLATFORMS="${ELEMENT_PACKAGE_PLATFORMS:-macos windows}"
# The in-game updater reads this from the latest release (docs/auto-update.md).
ELEMENT_UPDATE_MANIFEST_PATH="${ELEMENT_UPDATE_MANIFEST:-$ELEMENT_PACKAGE_OUTPUT_DIR/Element.update.json}"
ELEMENT_PLATFORM="$(uname -s)"

export ELEMENT_PROJECT_ROOT
export ADOFAI_DIR ADOFAI_MODS_DIR ADOFAI_MANAGED
export DOTNET_ROOT DOTNET_ROOT_ARM64 DOTNET_EXE
export UNITY_MOD_MANAGER_DLL HARMONY_DLL

export UNITY_EXE ELEMENT_UNITY_PROJECT ELEMENT_FONT_ASSETS
export ELEMENT_BUILD_OUTPUT ELEMENT_BOOTSTRAP_BUILD_OUTPUT ELEMENT_INSTALL_PATH
export ELEMENT_PACKAGE_BUILD_ROOT ELEMENT_PACKAGE_OUTPUT_DIR ELEMENT_PACKAGE_PLATFORMS
export ELEMENT_UPDATE_MANIFEST_PATH
export ELEMENT_PLATFORM

is_macos() {
  [ "$ELEMENT_PLATFORM" = "Darwin" ]
}

# package_stage <macos|windows>: the mod folder staged for that release archive.
package_stage() {
  printf '%s/%s/Element\n' "$ELEMENT_PACKAGE_BUILD_ROOT" "$1"
}

# packages_platform <macos|windows>: true when this run packages that OS.
packages_platform() {
  case " $ELEMENT_PACKAGE_PLATFORMS " in
    *" $1 "*) return 0 ;;
    *) return 1 ;;
  esac
}

# package_zip <macos|windows>
package_zip() {
  printf '%s/Element-%s.zip\n' "$ELEMENT_PACKAGE_OUTPUT_DIR" "$1"
}

ADOFAI_IPC_BUNDLE="${ADOFAI_IPC_BUNDLE:-$ELEMENT_PROJECT_ROOT/vendor/adofai-ipc-runtime}"
export ADOFAI_IPC_BUNDLE
