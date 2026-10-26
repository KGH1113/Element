#!/usr/bin/env bash
set -euo pipefail
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$WORKFLOW_DIR/../lib/context.sh"
cd "$IMPL_RESOURCEPACK_PROJECT_ROOT"
"$DOTNET_EXE" csharpier format \
  ImplResourcePack/Application \
  ImplResourcePack/Domain/DmNote ImplResourcePack/Domain/Layout \
  ImplResourcePack/Domain/Judgement/JudgementVisualPolicy.cs ImplResourcePack/Domain/Judgement/HitMarginPolicy.cs \
  ImplResourcePack/Domain/Editor \
  ImplResourcePack/Infrastructure/Game/Effects ImplResourcePack/Infrastructure/Game/Tweaks \
  ImplResourcePack/Infrastructure/Game/Editor/EditorTweaksController.cs \
  ImplResourcePack/Infrastructure/Ipc ImplResourcePack/Infrastructure/Game/DmNote \
  ImplResourcePack/Infrastructure/Assets/FontBundleLoader.cs \
  ImplResourcePack.Unity/Assets/Editor/FontBundleBuilder.cs ImplResourcePack.Unity/Assets/Editor/FontAssetGenerator.cs \
  ImplResourcePack/Infrastructure/Game/Input/KeyLimiterPatches.cs ImplResourcePack/Infrastructure/Game/Recording \
  ImplResourcePack/Infrastructure/Game/Input/DmNoteKeyResolver.cs \
  ImplResourcePack/Infrastructure/Game/Input/SettingsInputPatches.cs \
  ImplResourcePack/Bootstrap ImplResourcePack/Configuration ImplResourcePack/Presentation \
  ImplResourcePack.Tests/Program.cs ImplResourcePack.Tests/NativeViewerTests.cs \
  ImplResourcePack.Tests/OverlayLayoutTests.cs ImplResourcePack.Tests/LayoutEditingTests.cs \
  ImplResourcePack.Tests/GameplayTweaksTests.cs ImplResourcePack.Tests/EffectSettingsTests.cs \
  ImplResourcePack.Tests/PerformanceTests.cs ImplResourcePack.Tests/EffectFilterTests.cs \
  ImplResourcePack.Tests/NativeViewerSessionTests.cs
