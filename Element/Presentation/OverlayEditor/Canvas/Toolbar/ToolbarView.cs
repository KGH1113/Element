using Element.Application.Input;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static Element.Presentation.OverlayEditor.Parts.EditorControls;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Canvas.Toolbar;

/// <summary>
/// The main toolbar: what is selected and the whole layout's actions on top;
/// the selection's scale or Edit button, its actions, Add Element, the
/// highlight color and the default font below; the status line under them.
/// </summary>
internal sealed class ToolbarView : View<ToolbarViewModel>
{
  // The highlight color it is drawn in; the toolbar is drawn again when that changes.
  private readonly UiColor _lit = UiPalette.SoftAccent;

  public ToolbarView(ToolbarViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: 0,
        HStack(
            spacing: 8,
            Grip("Drag panel"),
            Text(() => VM.Selection.Value)
              .LineLimit(1)
              .Named("Selection")
              .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
            WideButton(L("전체 초기화", "Reset all"), "Reset all", () => VM.ResetAll, 104, 36)
              .Disabled(() => VM.Floating.Value),
            HStack(
              spacing: 4,
              IconButton(UiIconKind.Undo, "Undo", L("실행 취소", "Undo"), () => VM.Undo, 36),
              IconButton(UiIconKind.Redo, "Redo", L("다시 실행", "Redo"), () => VM.Redo, 36)
            ),
            WideButton(L("취소", "Cancel"), "Cancel", () => VM.Cancel, 88, 36),
            WideButton(L("저장", "Save"), "Save", () => VM.Save, 104, 36, ButtonStyle.Prominent)
          )
          .Frame(height: 36),
        HairLine.Across().Padding(new Insets(5, 0, 6, 0)),
        If(
          () => VM.Floating.Value,
          () =>
            Text(
                VM.NativeOrbit
                  ? L(
                    "요소는 위아래로 끌어 순서 변경 · 두 손가락으로 회전 · 핀치로 확대 · 빈 곳을 두 번 눌러 시점 초기화",
                    "Drag up/down to reorder · Two fingers to orbit · Pinch to zoom · Double-click space to reset"
                  )
                  : L(
                    "요소는 위아래로 끌어 순서 변경 · 빈 곳을 끌어 회전 · 휠로 확대 · 빈 곳을 두 번 눌러 시점 초기화",
                    "Drag elements up or down to reorder · Drag space to orbit · Scroll to zoom · Double-click space to reset"
                  )
              )
              .Font(Font.Caption)
              .Foreground(Colors.Muted)
              .Frame(maxWidth: float.PositiveInfinity, height: 40),
          () =>
            HStack(
              spacing: 8,
              // Elements sized in their editor show its Edit button in the scale's place.
              If(
                  () => VM.SizedInEditor.Value,
                  () =>
                    WideButton(
                      Text(() => VM.EditCaption.Value).LineLimit(1),
                      "Edit element",
                      () => VM.Edit,
                      172,
                      40,
                      ButtonStyle.Prominent
                    ),
                  () =>
                    HStack(
                      spacing: 8,
                      IconButton(UiIconKind.Plus, "Increase size", L("확대", "Increase size"), () => VM.Grow, 40),
                      ScaleField(),
                      IconButton(UiIconKind.Minus, "Decrease size", L("축소", "Decrease size"), () => VM.Shrink, 40)
                    )
                )
                .Frame(width: 172, height: 40, alignment: Alignment.Leading),
              Separator(),
              HStack(
                spacing: 4,
                IconButton(
                  UiIconKind.Reset,
                  "Reset element",
                  L("선택 요소 초기화", "Reset element"),
                  () => VM.ResetElement,
                  40
                ),
                SnapButton(),
                IconButton(
                  UiIconKind.Delete,
                  "Delete element",
                  L("요소 삭제 · Cmd/Ctrl + Delete", "Delete element · Cmd/Ctrl + Delete"),
                  () => VM.Delete,
                  40
                )
              ),
              Separator(),
              WideButton(L("요소 추가", "Add Element"), "Add element", () => VM.Add, 132, 40, ButtonStyle.Prominent),
              // What is in front and what is grouped; lit while its panel is open.
              WideButton(
                  HStack(
                      spacing: 6,
                      Image(ElementIcons.Of(UiIconKind.Layers), 20),
                      Text(L("순서", "Order")).LineLimit(1).Frame(maxWidth: float.PositiveInfinity)
                    )
                    .Foreground(() => VM.LayersOpen.Value ? _lit : Colors.Foreground),
                  "Layers",
                  () => VM.Layers,
                  108,
                  40
                )
                .Hint(L("앞뒤 순서와 묶음", "What's in front, and groups")),
              WideButton(
                HStack(
                  spacing: 8,
                  Spacer().Frame(width: 16, height: 16).Background(() => VM.Highlight.Value, 8),
                  Text(L("하이라이트 색", "Highlight")).LineLimit(1).Frame(maxWidth: float.PositiveInfinity)
                ),
                "Highlight color",
                () => VM.Color,
                140,
                40
              ),
              // The texts' default font sits beside their highlight color.
              WideButton(
                HStack(
                    spacing: 6,
                    Image(ElementIcons.Of(UiIconKind.Font), 20),
                    Text(L("기본 폰트", "Font")).LineLimit(1).Frame(maxWidth: float.PositiveInfinity)
                  )
                  .Foreground(Ink(() => VM.Font)),
                "Default font",
                () => VM.Font,
                112,
                40
              )
            )
        ),
        Text(() => VM.Status.Text.Value)
          .Font(Font.Caption.Sized(11))
          .Foreground(() => VM.Status.Ink.Value)
          .LineLimit(1)
          .TextAlignment(TextAlign.Center)
          .Named("Status")
          .Frame(maxWidth: float.PositiveInfinity, height: 14)
          .Padding(new Insets(2, 0, 0, 0))
      )
      .Padding(new Insets(12, 12, 4, 12));

  /// <summary>
  /// The selection's scale in percent, typed: applied when typing ends,
  /// refused with the range while it isn't a number in range. A dash for several.
  /// </summary>
  private View ScaleField() =>
    EnvironmentReader(environment =>
        HStack(
            spacing: 2,
            TextField(Bind(() => VM.Scale.Value))
              .TextFieldStyle(TextFieldStyle.Plain)
              .InputKind(TextInputKind.Decimal)
              .CharacterLimit(5)
              .TextAlignment(TextAlign.Trailing)
              .OnFocusChanged(InputGate.SetTextEditing)
              .OnEditEnded((_, _) => VM.FinishTyping.TryExecute())
              .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity)
              .Named("Scale percent"),
            Text("%").Font(Font.Caption).Foreground(Colors.Muted).Hidden(() => VM.Several.Value)
          )
          .Padding(new Insets(4, 10, 4, 10))
          .Background(Colors.SurfaceRaised, environment.Theme.CornerRadius, Colors.Line)
      )
      .Disabled(() => !VM.Scalable.Value)
      .Frame(width: 84, height: 40);

  /// <summary>Snapping: the icon takes the highlight while it is on.</summary>
  private View SnapButton()
  {
    UiColor on = UiPalette.SoftAccent;
    return IconButton(
      UiIconKind.Snap,
      "Snapping",
      L("자동 맞춤", "Snapping"),
      () => VM.Snap,
      40,
      () => VM.Snapping.Value ? on : Colors.Muted
    );
  }
}
