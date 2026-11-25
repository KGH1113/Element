# ui 이름 바꾸기 표

2026-11-11, `refactor/ui`. 공용 UI 부품을 `Presentation/Ui`로 옮기며 바뀐 타입 이름과 네임스페이스입니다. 다른 브랜치의 코드에 다시 적용할 때 씁니다.

**이름 규칙 하나:** `Presentation/Ui`의 타입은 화면 이름을 달지 않습니다. 앞에 붙은 `Settings`·`Layout`·`Overlay`·`Editor`는 `Ui`가 됩니다(`SettingsUiFactory`는 `UiUiFactory`가 아니라 `UiFactory`). 화면 이름이 없던 타입은 이름을 그대로 둡니다(`ShortcutKeys`, `PopoverStack`). 화면 하나에만 쓰여 그 화면 폴더로 간 타입도 이름을 그대로 둡니다.

**다시 적용하는 법:**
- 아래 표의 이름을 단어 단위로 바꿉니다(`Element/`, `Element.Tests/`, `scripts/ui-preview/`). 멤버 이름이 바뀐 줄을 타입 이름보다 먼저 바꿉니다.
- 옮긴 타입을 쓰는 파일에 새 네임스페이스의 `using`을 더하고, 비게 된 네임스페이스의 `using`을 지웁니다. `using static Element.Presentation.Settings.SettingsLocale;`는 `using static Element.Presentation.Ui.Theme.UiLocale;`입니다.
- `Element.Presentation.Ui.*` 안에서는 `Input`이 `Ui.Input` 네임스페이스로 읽히므로 Unity 입력은 `UnityEngine.Input`으로 씁니다.

## Ui/Theme (`Element.Presentation.Ui.Theme`)

| 예전 | 지금 |
| --- | --- |
| `Settings.SettingsPalette` | `UiPalette` |
| `Layout.LayoutColors.Warning` | `UiPalette.Error` (같은 색) |
| `Layout.LayoutColors.Bar` / `.Toolbar` / `.Menu` / `.Disabled` | `UiPalette.Bar` / `.Toolbar` / `.Menu` / `.Disabled` |
| `SettingsUiFactory.Background` / `.Surface` / `.Muted` / `.Foreground` | `UiPalette.Background` / `.Surface` / `.Muted` / `.Foreground` |
| `Settings.SettingsLocale` | `UiLocale` |
| `Overlay.Shared.OverlayTheme` | `UiTheme` (쓰이지 않던 값 색 함수는 지움) |
| `Settings.SettingsFonts` | `UiFonts` |

## Ui/Graphics (`Element.Presentation.Ui.Graphics`)

| 예전 | 지금 |
| --- | --- |
| `Settings.SettingsVectorGraphic` | `UiVectorGraphic` |
| `Settings.SettingsKeySymbol` | `UiKeySymbol` |
| `Layout.LayoutActionIcon`, `SettingsNavigationIcon`, `SettingsGradientIcon`, `SettingsDuplicateIcon`, `SettingsEditIcon`, `SettingsExportIcon` | `UiIcon` |
| `Layout.LayoutAction`, `SettingsNavigationIconKind`, `SettingsGradientIconKind` | `UiIconKind` |
| `SettingsNavigationIconKind.Back` / `.Forward` / `.Close` | `UiIconKind.PageBack` / `.PageForward` / `.WindowClose` |
| `SettingsGradientIconKind.Delete` | `UiIconKind.RemoveStop` |
| `SettingsDuplicateIcon` / `SettingsEditIcon` / `SettingsExportIcon` (클래스) | `UiIcon` + `UiIconKind.Duplicate` / `.Edit` / `.Export` |
| `GlyphButton<T>(name, parent, size, glyph, click, normal, hover, out T)` | `GlyphButton(name, parent, size, glyph, UiIconKind, click, normal, hover)` |

## Ui/Input (`Element.Presentation.Ui.Input`)

| 예전 | 지금 |
| --- | --- |
| `Settings.ShortcutKeys`, `Settings.KeyboardKeys` | 이름 그대로 |
| `Settings.SettingsModifierKeys` | `UiModifierKeys` |
| `Settings.SettingsCursor` / `SettingsCursorKind` | `UiCursor` / `UiCursorKind` |
| `Settings.SettingsInputCapture` | `UiInputCapture` |
| `Settings.SettingsGestureEnd` | 지움 (쓰이지 않음) |

## Ui/Widgets (`Element.Presentation.Ui.Widgets`)

| 예전 | 지금 |
| --- | --- |
| `Settings.SettingsUiFactory` | `UiFactory` |
| `Layout.EditorUiFactory.Create(font, reduceMotion)` | `new UiFactory(font, reduceMotion)` |
| `SettingsButton`, `SettingsSlider`, `SettingsSwitchMotion`, `SettingsFocusRing`, `SettingsIconInteraction`, `SettingsRoundedSprite`, `SettingsCapsuleGraphic` | `UiButton`, `UiSlider`, `UiSwitchMotion`, `UiFocusRing`, `UiIconInteraction`, `UiRoundedSprite`, `UiCapsuleGraphic` |
| `SettingsSegmentedControl`, `SettingsSegmentStyle`, `SettingsValueRow` | `UiSegmentedControl`, `UiSegmentStyle`, `UiValueRow` |
| `SettingsToast`, `SettingsToastMotion`, `SettingsPointerArea` | `UiToast`, `UiToastMotion`, `UiPointerArea` |
| `Layout.ContentEditors.Shell.EditorCapsules` | `UiCapsules` |

## Ui/Popovers (`Element.Presentation.Ui.Popovers`)

| 예전 | 지금 |
| --- | --- |
| `Settings.SettingsPopoverShade` | `UiPopoverShade` |
| `Layout.OverlayFontPopover` (`Layout/Editing`) | `UiFontPopover` |
| — | `PopoverStack.Clear()` (화면을 다시 만들 때) |

## Ui/ColorPicker (`Element.Presentation.Ui.ColorPicker`)

| 예전 | 지금 |
| --- | --- |
| `Settings.SettingsColorPicker` (partial 두 파일) | `UiColorPicker` + 색 막대 `UiGradientBar` + `IGradientBarHost` |
| `picker.StopSelected`, `.GradientBox`, `.ShownPosition`, `.ShownAlpha`, `.IsGradient`, `.SelectedStop`, `.PositionSlot`, `.PlacePosition`, `.SelectStop`, `.RemoveSelectedStop` | `picker.Gradient.…` (같은 이름). `GradientAvailable`, `Refresh`는 picker에 남음 |
| `SettingsColorPlane`, `SettingsColorMask`, `SettingsAlphaGraphic`, `SettingsGradientGraphic`, `SettingsGradientPreview`, `SettingsSwatchGraphic` | `UiColorPlane`, `UiColorMask`, `UiAlphaGraphic`, `UiGradientGraphic`, `UiGradientPreview`, `UiSwatchGraphic` |

UI 검사의 비공개 필드: 색 막대 쪽(`_bar`, `_kindRow`, `_tip`, `_delete`, `_kindFrames`, `_highlightNote`, `BarDown`·`BarDrag`·`BarHover`)은 `picker.Gradient`에서 찾습니다.

## 이름은 그대로, 폴더만 옮긴 것

| 예전 | 지금 |
| --- | --- |
| `Layout.OverlayColorPopover` (`Layout/Editing`) | `Layout.Canvas.OverlayColorPopover` |
| `Layout.OverlayGrowPicker`, `OverlayGrowArrow`, `OverlayGrowPopover`, `OverlayLiveValues` | `Layout.ContentEditors.Shell.…` |
| `Rendering.RenderNotices`, `Settings.EffectPresetShortcutNotice`, `Settings.KeyLimiterMenuText`, `Settings.UpdateStatusText` | `Notices.…` |
| `Settings.ProfileNames` | `Settings.Pages.ProfileNames` |
| `Settings.EffectPresetShortcuts` | `Screens.EffectPresetShortcuts` (저장·알림까지 직접 함, 생성자가 바뀜) |
| `Overlay.Attempt/Bpm/Combo/Judgement/Status/TimingScale.*Values`, `SongTitleMarkupNormalizer`, `Overlay.Shared.NumberText`, `Overlay.Elements.OverlayValues`, `Overlay.Shared.OverlayLocalization`·`OverlayLabels` | `Overlay.Values.…` |
| `Overlay.Shared.OverlayFonts`·`OverlayFontStatus`·`OverlayMaterialFactory` | `Overlay.Fonts.…` |
| `Overlay.Shared.OverlayTextFactory`, `Overlay.Elements.TextOverlayElement`·`OverlayTextComposer`·`OverlayCompiledRun`·`OverlayTextGradients`·`OverlayTextPulse`·`OverlayContentGeometry`·`OverlayFormulaText` | `Overlay.Text.…` |
| `Overlay.Elements.ShapeOverlayElement`·`ShapeGraphic`·`OverlayShapeResolver` | `Overlay.Shape.…` |
| `Overlay.Elements.LegacyOverlayPlacement`·`ShapeDefaultPlacement` | `Overlay.Migration.…` |
| Presentation의 `JudgementColors.Perfect` (`"60FF4E"`) | 지움. `Domain.Judgement.JudgementColors.Perfect` (`"#60FF4E"`) |

## 편집기 호스트

| 예전 | 지금 |
| --- | --- |
| `ITextEditorHost`, `IShapeEditorHost`의 겹친 멤버 | `Shell.IContentEditorHost` (두 인터페이스가 상속) |
| `ITextEditorHost.AnyPopover` | `IContentEditorHost.AnyOpen` |
| `ITextEditorHost.ClosePopovers()`, `IShapeEditorHost.CloseEditing()` | `IContentEditorHost.CloseOpen()` (편집기의 비공개 메서드 이름도 `CloseOpen`) |

## 새로 생긴 것

| 타입 | 하는 일 |
| --- | --- |
| `Presentation.Screens.SettingsScreens` | 설정 창과 배치 편집기 중 열린 것, 설정 단축키, 열고 닫을 때 입력과 네이티브 뷰어 |
| `Presentation.Settings.PlanetPreviews` | 행성 미리보기를 처음 그릴 때 만들고 창이 닫히면 놓음 |
| `Application.Gameplay.IPlanetPreview` | 행성 미리보기 포트 (`PlanetPreviewRenderer`가 구현) |
| `Application.KeyViewer.INativeViewerLayer` | 네이티브 뷰어가 설정 화면에 자리를 비켜 주는 포트 (`ElementNoteNativeViewers`가 구현) |
