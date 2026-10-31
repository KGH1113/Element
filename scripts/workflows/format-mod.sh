#!/usr/bin/env bash
set -euo pipefail
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$WORKFLOW_DIR/../lib/context.sh"
cd "$ELEMENT_PROJECT_ROOT"
"$DOTNET_EXE" csharpier format \
  Element/Application \
  Element/Domain/DmNote Element/Domain/Layout Element/Domain/Elements \
  Element/Domain/Judgement/JudgementVisualPolicy.cs Element/Domain/Judgement/HitMarginPolicy.cs \
  Element/Domain/Editor Element/Domain/Attempts \
  Element/Infrastructure/Attempts Element/Infrastructure/Game/Attempts \
  Element/Infrastructure/Game/Effects Element/Infrastructure/Game/Tweaks \
  Element/Infrastructure/Game/Editor/EditorTweaksController.cs \
  Element/Infrastructure/Ipc Element/Infrastructure/Game/DmNote \
  Element/Infrastructure/Assets/FontBundleLoader.cs \
  Element.Unity/Assets/Editor/FontBundleBuilder.cs Element.Unity/Assets/Editor/FontAssetGenerator.cs \
  Element/Infrastructure/Game/Recording \
  Element/Infrastructure/Game/Input/DmNoteKeyResolver.cs \
  Element/Infrastructure/Game/Input/SettingsInputPatches.cs \
  Element/Bootstrap Element/Configuration Element/Presentation \
  Element.Tests/Program.cs Element.Tests/NativeViewerTests.cs \
  Element.Tests/OverlayLayoutTests.cs Element.Tests/LayoutEditingTests.cs Element.Tests/OverlayElementTests.cs \
  Element.Tests/GameplayTweaksTests.cs Element.Tests/EffectSettingsTests.cs \
  Element.Tests/PerformanceTests.cs Element.Tests/EffectFilterTests.cs \
  Element.Tests/NativeViewerSessionTests.cs Element.Tests/AttemptTests.cs
