#!/usr/bin/env bash
set -euo pipefail
TASK_REPOSITORY="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TASK_DESKTOP="$TASK_REPOSITORY/apps/impl-dm-note"
cd "$TASK_DESKTOP"
case "${1:-}" in
  desktop-ipc-format)
    npm exec -- prettier --write src/renderer/infrastructure/ipc/sdkMessagePort.ts src/renderer/infrastructure/ipc/sdkMessagePort.test.ts
    ;;
  desktop-frontend-check)
    npm run check
    npm run build
    ;;
  desktop-check)
    npm run sdk:check
    npm run type-check
    npm run lint -- --max-warnings=0
    npm run format:check
    npm run test
    cargo check --offline --manifest-path src-tauri/Cargo.toml
    cargo test --offline --manifest-path src-tauri/Cargo.toml
    cargo clippy --offline --manifest-path src-tauri/Cargo.toml --all-targets -- -D warnings
    ;;
  desktop-native-format)
    rustfmt --edition 2021 src-tauri/src/commands/app/adofai_ipc.rs src-tauri/src/services/render/mod.rs
    ;;
  desktop-native-check)
    if [ "${2:-}" != "--clippy-only" ]; then
      cargo check --offline --manifest-path src-tauri/Cargo.toml
      cargo test --offline --manifest-path src-tauri/Cargo.toml
    fi
    cargo clippy --offline --manifest-path src-tauri/Cargo.toml --all-targets -- -D warnings
    ;;
  desktop-message-test)
    npm exec -- vitest run src/renderer/infrastructure/ipc/sdkMessagePort.test.ts src/renderer/application/connectionSupervisor.test.ts src/renderer/ipc/keyLimiterIpcCoordinator.test.ts src/renderer/render/renderIpcCoordinator.test.ts src/renderer/ipc/keyLimiterSnapshot.test.ts
    ;;
  desktop-format)
    npm exec -- prettier --write src/renderer/application src/renderer/bootstrap src/renderer/infrastructure src/renderer/ipc src/renderer/render/renderIpcCoordinator.ts src/renderer/render/renderIpcCoordinator.test.ts src/renderer/__tests__/fakeMessagePort.ts src/renderer/hooks/app/useKeyLimiterSync.ts src/renderer/hooks/app/useKeyLimiterSync.test.ts src/renderer/windows/main/index.tsx src/renderer/stores/useKeyLimiterStatusStore.ts src/renderer/stores/useKeyLimiterStatusStore.test.ts src/renderer/render/virtual-clock.test.ts src/renderer/render/virtual-clock.mjs src/renderer/hooks/overlay/useNoteSystem.test.ts src/renderer/utils/core/styleElement.ts src/renderer/hooks/app/useCustomCssInjection.ts src/renderer/stores/useFontStore.ts src/renderer/windows/render/index.mjs
    ;;
  desktop-licenses)
    npm run licenses:generate
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
