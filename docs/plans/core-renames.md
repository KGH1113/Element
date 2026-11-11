# core 이름 바꾸기 표

2026-11-11, `refactor/core2`. Domain·Application·Configuration을 기능 폴더로 옮기며 바뀐 네임스페이스입니다. 다른 영역의 코드에 다시 적용할 때 씁니다.
타입 이름은 그대로이고, 네임스페이스는 폴더 경로와 같습니다.

**다시 적용하는 법:**
- 옮긴 타입을 쓰는 파일에 새 네임스페이스의 `using`을 더합니다.
- 없어진 네임스페이스(아래 "없어진 네임스페이스")의 `using`은 지웁니다.
- 일부만 적은 이름도 고칩니다. 예: `Domain.Elements.OverlayElementDefaults` → `Domain.Overlay.Defaults.OverlayElementDefaults`.
- `scripts/ui-preview/prepare.mjs`의 경로도 아래 표의 새 폴더를 따릅니다.

## Domain

| 예전 | 지금 | 타입 |
| --- | --- | --- |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Values` | `OverlayValueCatalog`, `OverlayValueCategory`, `OverlayValueDefinition`, `OverlayValueGroup`, `OverlayValueId`, `OverlayValueKind`, `OverlayValueMask`, `OverlayValueSection`, `OverlayShownValues` |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Formula` | `IOverlayNumberSource`, `OverlayFormula`, `OverlayFormulaDisplay`, `OverlayFormulaEvaluator`, `OverlayFormulaKeypad`, `OverlayFormulaSyntax`, `OverlayFormulaToken`, `OverlayFormulaTokenKind` |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Text` | `OverlayTextDocument`, `OverlayTextItem`, `OverlayAnimateState` |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Shape` | `OverlayShapeGeometry` |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Gradient` | `OverlayRgba`, `OverlayGradientEdits`, `OverlayGradientFrame`, `OverlayGradientMath`, `OverlayGradientSample` |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Fonts` | `OverlayFontFace` |
| `Element.Domain.Elements` | `Element.Domain.Overlay.Defaults` | `OverlayDefaultElement`, `OverlayDefaultRuns`, `OverlayElementDefaults`, `OverlayElementMigration` |
| `Element.Domain.DmNote` | `Element.Domain.KeyViewer` | `NativeViewerDefaultLayout`, `NativeViewerFailure`, `NativeViewerLayoutSync`, `NativeViewerStatus`, `NativeViewerSurface` |
| `Element.Domain.Layout` | `Element.Domain.KeyViewer` | `NativeViewerGrowth` |
| `Element.Domain.DmNote` | `Element.Domain.Rendering` | `PixelRect`, `RenderFrameCrop`, `RenderOverlayRects`, `RenderViewerPlacement` |
| `Element.Domain.Input` | `Element.Domain.KeyLimiter` | `KeyLimiterSnapshot` |
| `Element.Domain.Input` | `Element.Domain.Tweaks` | `ChatterFilter` |
| `Element.Domain.Editor` | `Element.Domain.Tweaks` | `EditorTransform`, `EditorTransformKind` |
| `Element.Domain.Combo` | `Element.Domain.Judgement` | `ComboRule`, `ComboSnapshot` |
| `Element.Domain.TimingScale` | `Element.Domain.Judgement` | `TimingScaleSnapshot` |

## Application

| 예전 | 지금 | 타입 |
| --- | --- | --- |
| `Element.Application.Combo` | `Element.Application.Judgement` | `ComboFeature` |
| `Element.Application.TimingScale` | `Element.Application.Judgement` | `ITimingScaleSource`, `TimingScaleFeature` |

## Configuration

| 예전 | 지금 | 타입 |
| --- | --- | --- |
| `Element.Configuration` | `Element.Configuration.Overlay` | `Anchor9`, `OverlayColorSettings`, `OverlayElementKinds`, `OverlayFontKey`, `OverlayFontUse`, `OverlayGradient`, `OverlayGradientKind`, `OverlayGradientStop`, `OverlayGrowth`, `OverlayLayoutSettings`, `OverlayModSettings`, `OverlayShapeKind`, `OverlayShapePoint`, `OverlayShapeSettings`, `OverlayTextRun`, `OverlayTextSettings` |
| `Element.Configuration` | `Element.Configuration.Gameplay` | `GameplayTweaksSettings`, `ChatterBlockerOptions`, `EditorTweaksOptions`, `JudgementVisualOptions`, `PlanetAppearanceOptions`, `PlanetAppearanceProfile`, `PlanetComponentAppearance` |

- `Element.Configuration`에 남은 것: `ElementSettings`, `CopiedInPlaceAttribute`, `SettingsCopy`, `SettingsXml`, `KeyShortcut`.
- `RemovedEffects`와 `EffectPreset`은 `EffectSettings.cs`에서 파일 하나씩으로 나왔고, 네임스페이스(`Element.Configuration.Effects`)는 그대로입니다.
- 저장 파일은 바뀌지 않습니다. `Element.Tests/Configuration/SavedFormatTests.cs`가 옮기기 전 코드로 쓴 설정 파일과 프로필 파일을 읽고 다시 써서 바이트까지 같은지 확인합니다.

## 없어진 네임스페이스

이 네임스페이스의 `using`은 컴파일되지 않습니다.

- `Element.Domain.Elements`, `Element.Domain.DmNote`, `Element.Domain.Input`, `Element.Domain.Editor`, `Element.Domain.Combo`, `Element.Domain.TimingScale`
- `Element.Application.Combo`, `Element.Application.TimingScale`

## UI 검사 (`prepare.mjs`)

- `Element/Domain/Elements` 폴더 → `Element/Domain/Overlay`를 하위 폴더까지 읽습니다.
- `Element/Domain/DmNote/NativeViewer*` 4개 → `Element/Domain/KeyViewer` 폴더 전체.
- `Element/Domain/DmNote/RenderViewerPlacement.cs` → `Element/Domain/Rendering/RenderViewerPlacement.cs`.
- `Element/Domain/Input/ChatterFilter.cs` → `Element/Domain/Tweaks/ChatterFilter.cs`.
- Configuration은 전부터 하위 폴더까지 읽어서 바꾸지 않았습니다.
