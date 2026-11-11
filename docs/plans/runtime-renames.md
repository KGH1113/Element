# runtime 2단계: 이름 바뀐 타입

`refactor/runtime2`에서 Infrastructure·Bootstrap 타입 이름을 바꾸거나 나눈 목록입니다. 다른 영역을 합칠 때 이 표대로 이름을 고칩니다.

## DmNote → ElementNote (내부 이름만)

| 전 | 후 |
| --- | --- |
| `IDmNoteApp` | `IBundledElementNoteApp` |
| `BundledDmNoteApp` | `BundledElementNoteApp` |
| `MacDmNoteApp` | `MacElementNoteApp` |
| `WindowsDmNoteApp` | `WindowsElementNoteApp` |
| `DmNoteAppLauncher` | `ElementNoteAppLauncher` |
| `IDmNoteAppFocus` | `IElementNoteAppFocus` |
| `DmNoteAppFocus` | `ElementNoteAppFocus` |
| `MacDmNoteAppFocus` | `MacElementNoteAppFocus` |
| `WindowsDmNoteAppFocus` | `WindowsElementNoteAppFocus` |
| `DmNoteKeyMapper` | `ElementNoteKeyMapper` |
| `DmNoteKeyResolver` | `ElementNoteKeyResolver` |
| `DmNoteNativeIpcFeature` | `ElementNoteNativeViewers` (IPC 채널에는 `.Endpoint`를 넘김) |
| 테스트 `DmNoteAppTests` | `ElementNoteAppTests` |

**바꾸지 않은 것:** `Element.Domain.DmNote` 네임스페이스(core 영역), 설정 필드 `ShowDmNoteInGame`·`AutoOpenCloseDmNote`, 저장된 배치 id `dmnote-hand`·`dmnote-foot`, IPC 이름(`mirror.native.*` 등).

## 나눈 클래스

| 전 | 후 |
| --- | --- |
| `DmNoteNativeIpcFeature` | `ElementNoteNativeViewers`(조립·프레임), `NativeViewerIpcEndpoint`, `NativeViewerTargets`, `NativeViewerHostLifetime`, `NativeViewerRenderPlacements`, `ElementNoteTabOpener`, `NativeViewerErrorLog` |
| `NativeViewerLayoutTarget`의 오프라인 견본 | `NativeViewerSamplePreview` |
| `GameplayTweaksController` (삭제) | `Game/Editor/GuardedEditorTweaks`, `Game/Planets/GuardedPlanetAppearance`. 채터 차단 설정은 `ModRuntime`이 `ChatterBlocker.Configure`로 넘김 |
| `GameHooks.Tweaks` | `GameHooks.EditorTweaks`, `GameHooks.PlanetAppearance` |
| `GameplayTweaksController.RefreshPlanets()` | `GuardedPlanetAppearance.Apply()` |
| `EditorTweaksController` | `EditorTweaksController`(순서만), `EditorSelectionLabel`, `EditorFineRotation`, `EditorEventSync` |
| `EditorTweaksController.Mutation` | `EditorFloorMutation` |
| `PlanetPreviewRenderer` | `PlanetPreviewRenderer`(카메라·대상), `PlanetPreviewSources`, `PlanetPreviewSource`(전 `Source`), `PlanetPreviewVisual`(전 `Preview`), `PlanetPreviewObjects` |
| `PlanetAppearanceController.PlanetState` | `PlanetOriginalAppearance` |
| `PlanetAppearanceController.ParticleColors` | `PlanetParticleColors` |
| `OverlayFontCatalog` | `OverlayFontCatalog`(찾기·불러오기), `OverlayFontFolder`, `InstalledFontList`, `OverlayFontFiles`, `OverlayFontFaces` |
| `ModRuntime.WarnIfKeyboardChatterBlockerIsLoaded` | `Infrastructure/Mods/KeyboardChatterBlockerCheck.WarnIfLoaded` |
| `AttemptRunStateProvider.cs`·`KeyCountRunStateProvider.cs` 안의 런 상태 | `AttemptRunState.cs`, `KeyCountRunState.cs` (이름·네임스페이스 그대로) |

`Element.Infrastructure.Game` 네임스페이스는 이제 비어 있습니다.
