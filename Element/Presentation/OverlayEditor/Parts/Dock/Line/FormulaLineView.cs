using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// The dock's line: the open field's mark, whose it is and its name, then
/// its pieces laid out in place (a condition's five slots in a row, each a
/// click away), and the result on the right. A field picked from a fixed set
/// (a font) shows its one choice as a picked value. A long line scrolls
/// sideways. With nothing open it is clear. When what was asked can't be
/// done, it shakes.
/// </summary>
internal sealed class FormulaLineView : View<LineViewModel>
{
  public const float Height = 54;

  private readonly UiColor _ring = Ui.Theme.UiPalette.SoftAccent;

  public FormulaLineView(LineViewModel vm)
    : base(vm) { }

  public override View Body =>
    If(
        () => VM.Open,
        () =>
          HStack(
              spacing: 10,
              Who(),
              If(
                () => VM.Choosing,
                () => Choice().Frame(maxWidth: float.PositiveInfinity, height: Height, alignment: Alignment.Leading),
                () =>
                  ScrollView(Axis.Horizontal, new LineSlotView(VM.Root, VM))
                    .DefaultScrollAnchor(Alignment.Trailing)
                    .Frame(maxWidth: float.PositiveInfinity, height: Height)
                    .Named("Formula line")
              ),
              HStack(
                spacing: 6,
                If(
                  () => VM.ResultHasSwatch,
                  () => Spacer().Frame(width: 16, height: 16).Background(() => VM.ResultSwatch, 8)
                ),
                Text(() => VM.Result)
                  .Font(Font.Body.Sized(20))
                  .Foreground(Ui.Theme.UiPalette.SoftAccent)
                  .LineLimit(1)
                  .Named("Result")
              )
            )
            // "No" is a shake, not a sentence.
            .Offset(x: () => VM.Shake.X)
      )
      .Frame(maxWidth: float.PositiveInfinity, height: Height);

  // What is open: its mark, whose it is, its name and "=".
  private View Who() =>
    HStack(
      spacing: 6,
      If(
        () => VM.Choosing,
        () => Switch(() => VM.ChoiceGlyph, glyph => TypeMarks.Mark(glyph, 18)),
        () => TypeMarks.Mark(() => VM.Type, 18)
      ),
      If(
        () => VM.Whose.Length > 0,
        () => Text(() => VM.Whose).Font(Font.Body.Sized(16)).Foreground(Colors.Muted).LineLimit(1).Named("Whose")
      ),
      Text(() => VM.Name).Font(Font.Body.Sized(16).Bolded()).Foreground(Colors.Foreground).LineLimit(1).Named("Name"),
      Text("=").Font(Font.Body.Sized(16)).Foreground(Colors.Muted)
    );

  // The one thing a field picked from a fixed set holds: a picked value's pill, with the field's own mark.
  private View Choice() =>
    HStack(
        spacing: 6,
        Switch(() => VM.ChoiceGlyph, glyph => TypeMarks.Mark(glyph, 18)),
        Text(() => VM.ChoiceLabel).Font(Font.Body.Sized(17)).Foreground(Colors.Foreground).LineLimit(1).Named("Choice")
      )
      .Padding(new Insets(0, 7, 0, 12))
      .Frame(height: LinePieceView.ChipHeight)
      .Background(FieldLook.Chip, LinePieceView.ChipHeight / 2)
      .Overlay(Capsule().Stroke(() => _ring, FieldLook.Ring));
}
