# Element on ElementUI

> Status: in progress on `feat/elementui` · 2026-11-12

Element's UI (settings window, overlay editor chrome, content editors, popovers)
moves from hand-placed UGUI (`UiFactory`, manual `Layout(width, height)`) to
ElementUI views and view models. The look stays Element's. The overlay HUD
drawn on the game screen (texts, shapes, key viewers) is not UI chrome and
stays as it is.

ElementUI: `~/dev/src/ElementUI` (read `README.md`, `docs/design.md` §2–§4,
§9–§10, and `src/ElementUI.Core/UI.cs`, `ViewModifiers.cs`). It is pinned in
`vendor/elementui` by `./scripts/run.sh elementui-sync ~/dev/src/ElementUI`.

## Rules for every screen

1. **Four rules.** Fixed content is a value (`Text("Save")`); changing content is an
   expression (`Text(() => VM.Name)`); controls `Bind(() => VM.Settings.X)`; buttons
   pick a command (`Button(Text("Reset"), () => VM.Reset)`).
2. **MVVM.** A page is a `XModel : SettingsPageModel` (fields, `Computed`, `Command`)
   and a `XView : View<XModel>` (layout and bindings only, no logic). Settings live
   in `VM.Settings` (the `ElementSettings`); bind straight to their fields.
3. **No manual layout.** No `Layout(width, height)`, no coordinates, no `UiFactory`.
   Stacks, `Spacer`, `Frame`, `Padding`, `Grid`, `Flow`, `ScrollView`. A narrow
   window must still work, without reading widths back into a view model:
   cards in `Grid(minColumnWidth:, spacing: 12)` (pick the minimum so the widest
   page, 936, keeps its column count); a row that stacks when narrow is
   `ViewThatFits(wide.Frame(minWidth: breakpoint, maxWidth: ∞), narrow)`;
   words that must stay short use `.LineLimit(n)`. An `If` or `ForEach` goes
   straight in a stack, no frame around it.
4. **Applying.** Any control write or command run raises `UiRoot.Edited`; the window
   then applies the settings. Don't call apply yourself. A change made outside a
   command or binding (a callback from a popover, a timer) calls `ElementUI.Runtime.Probe()`.
5. **Words.** `L("한국어", "English")` from `UiLocale` (it is `[ElementUI.Pure]`, so
   expressions may call it). Keep the exact wording of the old page.
6. **Look.** Colors are theme tokens (`Colors.Muted`, `Colors.Accent`, `Colors.Danger`);
   Element icons are `Image(ElementIcons.Of(UiIconKind.X), size)`. Sizes come from
   `ElementTheme` (body 16, title 27, heading 18, callout 15, caption 14).
   Match the old screenshot; small differences from ElementUI's controls are fine.
7. **Performance.** Nothing runs per frame unless the page truly polls (then
   `Tick()`, cheap). No allocation in `Tick`. Lists that change are
   `ObservableList<T>` or a replaced list.
8. **Analyzer.** The build runs ElementUI's analyzer: fix UI001/UI003/UI005/UI006.

## The window

- `Element/Presentation/Settings/Window/SettingsWindowViewModel.cs`: owns every
  page model (`Home`, `Tweaks`, `EditorTweaks`, `Judgment`, `Planets`, `Chatter`,
  `Effects`, `PresetEditor`, `General`, `Profiles`), `Show(page)`, `Back`,
  `Save()`, `EditLayout()`, `Features` (`SettingsFeatures`: planet previews,
  updates, profiles, fonts, chatter count, judgment-text link).
- `SettingsPageModel` hooks: `Opened`, `Shown`, `Tick` (per frame while shown),
  `TickCapture` (key recording; return true while recording), `CancelCapture`,
  `HandleEscape`, `SettingsReplaced`, `Closed`.
- Navigation: a page's command calls `Window.Show(Window.Judgment)`. Back is the
  window's. The page's parent is the second constructor argument.
- `Pages/HomeModel.cs` + `Pages/HomeView.cs` are the reference conversion.

## Element controls ElementUI doesn't have

Put them in `Element/Presentation/Ui/Declarative/Controls/`, one file each:

- A view that draws its own UGUI derives from `ElementUI.Ugui.NativeView`
  (`Build(RectTransform)`, `PreferredSize(Proposal)`, `Resized`, `Teardown`,
  `Watch(...)` to follow values). Reuse the existing Element widgets inside it
  (`UiColorPicker`, `UiIcon`, planet `RawImage`…) rather than redrawing them.
- A part made of ElementUI views is a static method returning `View`.

## Checks and verification

- Old page: `git show dev:Element/Presentation/Settings/Pages/<Name>Page.cs`;
  old preview checks for it: `git show dev:scripts/ui-preview/SettingsPreview.cs`.
- Old screenshots: `~/dev/src/impl-resourcepack/.claude/worktrees/elementui-baseline/build/ui-preview/*.png`.
- New checks: `scripts/ui-preview/Settings<Page>Checks.cs` with
  `[PageCheck("_tweaks")] static int Check(SettingsCheck check)` (see
  `SettingsPageChecks.cs`). Port the old page's assertions that still mean
  something (behavior, not pixel positions of the old layout).
- Done when: `./scripts/run.sh mod-check` and `./scripts/run.sh ui-check` pass, the
  new screenshots look like the old ones, and every control of the old page is there.

## Progress

| Area | State |
| --- | --- |
| ElementUI pinned, shipped with Element, Element theme and backend | done |
| Settings window frame, navigation, save error | done |
| Home | done |
| Tweaks, Editor tweaks, Judgment, Chatter | done |
| Planets (and the shared color swatch, color picker, texture views) | done |
| Effects, Preset editor, General, Profiles (and ShortcutField) | done; ui-check 5026 assertions on the merge |
| Dedupe stopgaps: SwitchCard/SwitchCards, WidthLayouts, WidthReader, CardRows, RaisePopover → ElementUI Grid(minColumnWidth:), ViewThatFits | done |
| Overlay editor chrome (toolbar, element bar, add menu, reset dialog) | done |
| Content editors: the frame's top line, the bar and its buttons, Grow from | done |
| Content editors: input dock and keypad, color picker form, highlight color popover | in progress |
| Mod element editor, ElementKit EditorPage on ElementUI | next |
| ElementKit `EditorPage` on ElementUI, `EditorSession` | next |
| Remove the old UI kit (`UiFactory` and widgets) | last |

## Overlay editor map (for the next slices)

The boundary: **a panel's frame stays custom, its contents become an ElementUI
screen.** Panels drag themselves and report their screen rects to ElementNote's
native viewers (`OverlayLayoutEditor.NativeExclusions`); keep that code and mount
a `UiHost` inside each panel.

Stays custom (hand-made interaction on the overlay or the editor stage):
`Layout/Canvas/LayoutDrag, LayoutMarquee, LayoutSelection, LayoutGuides,
LayoutKeyboard, LayoutSession, LayoutCommands`, `Layout/Graphics/*`,
`Layout/KeyViewers/*`, the text stage (`TextDocumentEditing, TextPreview,
TextLayoutMap, TextMarks, TextPointer, TextKeyboard, TextValueDrops`), the shape
stage (`ShapePreview, ShapeMeasures, ShapeGradient, ShapeValueDrops, ShapeInput`),
`Shell/OverlayGradientHandles/Lines/Editing`, formula state
(`OverlayFormulaEditing`, `OverlayFormulaSlot`), `OverlayEditorHistory`, value
dragging (`OverlayValueChipHandle`), color picker graphics (`UiColorPlane`,
`UiGradientBar`, …) and `UiFontPopover`'s recycled rows.

Becomes ElementUI (contents):

| Slice | Files | Edits |
| --- | --- | --- |
| Toolbar | `Canvas/LayoutToolbar` (441), `LayoutStatusLine`, `LayoutActionHint` | scale via `SetBounds`/`CommitLayout`, `LayoutSnapping`, `LayoutCommands`, undo state |
| Element bar | `Canvas/LayoutElementBar`, `LayoutArrangeBar`, `Kinds/KeyViewerKind.BuildBar` | align/distribute, key viewer hand/foot, open ElementNote |
| Add menu | `Canvas/LayoutAddMenu`, `Ui/Popovers/ListMenu`, `Kinds/*Kind.Row` | adds an element, opens its editor |
| Reset dialog, highlight color popover, font popover placement | `Canvas/LayoutModal`, `LayoutReset`, `OverlayColorPopover`, `LayoutPopovers` | `HighlightColor`, `OverlayFont` |
| Editor frame and bar | `Shell/OverlayEditorFrame` (hint, Cancel, Done), `OverlayEditorBar`, `OverlayBarButton`, bar parts (`TextSize/Color/Fonts/Animate`, `ShapeColors/Numbers`, `OverlayGrowFrom`) | each part's edit logic and history calls stay in its model |
| Input dock and keypad | `Shell/OverlayInputDock`, `OverlayKeypad` | keypad becomes a grid; the formula line and draggable chips stay custom (`NativeView`) |
| Grow-from picker | `Shell/OverlayGrowPicker`, `OverlayGrowPopover` | `GrowFrom` |
| Color picker form | `Ui/ColorPicker/UiColorPicker` fields (title, hex, opacity, Highlight/Custom, reset, close), `Shell/OverlayEditorPicker`, `OverlayOpacityField` | `OverlayColorSettings`; plane, sliders and gradient bar stay custom |
| Mod element editor | `Mods/OverlayModEditor`, `ModEditorPage` and parts | `ElementKit.EditorPage` is public: keep its API, implement it on ElementUI; `At(x, y)` needs a free-placement layer |

Done: the frame's top line is `OverlayEditorFrameModel`/`View` on a panel the
frame makes (`OverlayEditorFrame.Panel`, `NewBackend`); the bar is
`OverlayEditorBar` showing `OverlayBarButtonModel`s, which the parts make and
drive (their edit logic and undo stay in the part); Grow from's popover and the
nine points are `OverlayGrowPickerModel` with `OverlayGrowPopoverView` and
`OverlayGrowPointsView` (the mod element editor's anchor part mounts the points too).

Editors open through `ContentElementKind<T>.Open` → `IContentEditor.Open(target, closed)`;
Done/Cancel → `ContentElementKind.Closed` → `Session.Commit()`. Layout undo is
`Domain/Layout/LayoutHistory` (whole snapshots); editor undo is per editor and
dropped on close. Closing can be refused (`Commit()` returning false) — keep that.

The preview checks (`SettingsPreview`, `ModElementChecks`, `GradientChecks`) find
objects by GameObject name ("Drag panel", "Formula line", "Controls/Dragged value",
"Switch ", "Paint ", "List add") and read `FormulaText`, `NoticeText`, `TipText`:
update them with each slice.
