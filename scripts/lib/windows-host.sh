#!/usr/bin/env bash
# SSH transport to the optional Windows build host: a Windows PC with OpenSSH
# Server and Visual Studio Build Tools (C++ x64). The Windows in-game viewer
# library needs MSVC and the WebView2 SDK, so it is built and verified there.

if [ "${IMPL_RESOURCEPACK_WINDOWS_HOST_LOADED:-0}" = "1" ]; then
  return 0
fi
IMPL_RESOURCEPACK_WINDOWS_HOST_LOADED=1

# shellcheck source=context.sh
source "${BASH_SOURCE[0]%/*}/context.sh"
# shellcheck source=guards.sh
source "${BASH_SOURCE[0]%/*}/guards.sh"

IMPL_RESOURCEPACK_WINDOWS_HOST="${IMPL_RESOURCEPACK_WINDOWS_HOST:-}"
IMPL_RESOURCEPACK_WINDOWS_SSH_KEY="${IMPL_RESOURCEPACK_WINDOWS_SSH_KEY:-}"
IMPL_RESOURCEPACK_WINDOWS_MODS_DIR="${IMPL_RESOURCEPACK_WINDOWS_MODS_DIR:-C:\\Program Files (x86)\\Steam\\steamapps\\common\\A Dance of Fire and Ice\\Mods}"

# Relative to the remote user's home; scp and PowerShell both resolve it there.
WINDOWS_REMOTE_DIR=".impl-resourcepack/game-webview"
export IMPL_RESOURCEPACK_WINDOWS_HOST IMPL_RESOURCEPACK_WINDOWS_SSH_KEY IMPL_RESOURCEPACK_WINDOWS_MODS_DIR
export WINDOWS_REMOTE_DIR

require_windows_host() {
  [ -n "$IMPL_RESOURCEPACK_WINDOWS_HOST" ] ||
    fail "Set IMPL_RESOURCEPACK_WINDOWS_HOST in .env (for example user@windows-pc) to build the Windows in-game viewer. See .env.example."
  require_command ssh
  require_command scp
  require_command iconv
  require_command base64
}

windows_ssh_options() {
  WINDOWS_SSH_OPTIONS=(-o BatchMode=yes -o ConnectTimeout=15)
  if [ -n "$IMPL_RESOURCEPACK_WINDOWS_SSH_KEY" ]; then
    WINDOWS_SSH_OPTIONS+=(-i "${IMPL_RESOURCEPACK_WINDOWS_SSH_KEY/#\~/$HOME}")
  fi
}

# Runs PowerShell read from stdin on the Windows host with UTF-8 output. The
# script travels encoded, so no quoting reaches the remote shell.
windows_powershell() {
  local script encoded
  windows_ssh_options
  script="\$ErrorActionPreference = 'Stop'; \$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$(cat)"
  encoded="$(printf '%s' "$script" | iconv -f UTF-8 -t UTF-16LE | base64 | tr -d '\n')"
  # The encoded script expands here on purpose; the remote side only decodes it.
  # shellcheck disable=SC2029
  ssh "${WINDOWS_SSH_OPTIONS[@]}" "$IMPL_RESOURCEPACK_WINDOWS_HOST" \
    "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encoded" |
    LC_ALL=C tr -d '\r'
  return "${PIPESTATUS[0]}"
}

# windows_upload <remote relative directory> <local files...>
windows_upload() {
  local remote="$1"
  shift
  windows_ssh_options
  scp -q "${WINDOWS_SSH_OPTIONS[@]}" "$@" "$IMPL_RESOURCEPACK_WINDOWS_HOST:$remote/"
}

# windows_download <remote relative path> <local path>, replaced atomically.
windows_download() {
  local remote="$1" destination="$2" temporary
  windows_ssh_options
  mkdir -p "$(dirname "$destination")"
  temporary="$(mktemp "$(dirname "$destination")/.download.XXXXXX")"
  if ! scp -q "${WINDOWS_SSH_OPTIONS[@]}" "$IMPL_RESOURCEPACK_WINDOWS_HOST:$remote" "$temporary"; then
    rm -f "$temporary"
    return 1
  fi
  mv -f "$temporary" "$destination"
}
