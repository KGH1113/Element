using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// The dock's line: the open field's mark and name, then its pieces laid out
/// in place (a condition's five slots in a row, each a click away), and the
/// result on the right. A long line scrolls sideways. With nothing open, it
/// says what to do.
/// </summary>
internal sealed class FormulaLineView : View<LineViewModel>
{
  public const float Height = 54;

  public FormulaLineView(LineViewModel vm)
    : base(vm) { }

  public override View Body =>
    If(
        () => VM.Open,
        () =>
          HStack(
            spacing: 10,
            Who(),
            ScrollView(Axis.Horizontal, new LineSlotView(VM.Root, VM))
              .DefaultScrollAnchor(Alignment.Trailing)
              .Frame(maxWidth: float.PositiveInfinity, height: Height)
              .Named("Formula line"),
            HStack(
              spacing: 6,
              If(() => VM.ResultHasSwatch, () => Spacer().Frame(width: 16, height: 16).Background(() => VM.ResultSwatch, 8)),
              Text(() => VM.Result)
                .Font(Font.Body.Sized(22))
                .Foreground(Ui.Theme.UiPalette.SoftAccent)
                .LineLimit(1)
                .Named("Result")
            )
          ),
        () =>
          Text(() => VM.Hint)
            .Font(Font.Body.Sized(16))
            .Foreground(Colors.Muted)
            .LineLimit(1)
            .Named("Line hint")
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      )
      .Frame(maxWidth: float.PositiveInfinity, height: Height);

  // What is open: its type's mark, its name and "=".
  private View Who() =>
    HStack(
      spacing: 6,
      TypeMarks.Mark(() => VM.Type, 20),
      Text(() => VM.Name).Font(Font.Body.Sized(17)).Foreground(Colors.Muted).LineLimit(1).Named("Name"),
      Text("=").Font(Font.Body.Sized(17)).Foreground(Colors.Muted)
    );
}
