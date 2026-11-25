using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// The If tool in an editor's property column: the cases' cards from the
/// top, "Add a case", the otherwise card, and under them a slider that tries
/// a value out. Taller than the column, it scrolls, keeping its bottom in
/// view as cases are added.
/// </summary>
internal sealed class IfToolView : View<IfToolViewModel>
{
  public const float Gap = 6,
    AddHeight = 32,
    TryHeight = 30;

  private readonly float _width;
  private readonly float _room;

  /// <param name="room">How tall the tool may be before it scrolls.</param>
  public IfToolView(IfToolViewModel vm, float width, float room)
    : base(vm)
  {
    _width = width;
    _room = room;
  }

  public override View Body =>
    ScrollView(
        VStack(
            spacing: Gap,
            VStack(spacing: Gap, ForEach(() => VM.Cases.Value, card => card.Index, slot => new IfCaseView(slot))),
            AddRow(),
            If(() => VM.HasIf.Value, () => new IfCaseView(VM.Otherwise)),
            If(() => VM.CanTry.Value, TryRow)
          )
          .Frame(width: _width, alignment: Alignment.TopLeading)
      )
      .Frame(width: _width, maxHeight: _room, alignment: Alignment.Top)
      // A new case goes in near the bottom, by Add a case: that end stays in view as the cards grow.
      .DefaultScrollAnchor(Alignment.Bottom);

  private View AddRow()
  {
    string add = L("경우 더하기", "Add a case"),
      make = L("만약 만들기", "Make an If");
    return Button(
        HStack(
            spacing: 6,
            Image(ElementIcons.Of(UiIconKind.Plus), 14).Foreground(Colors.Muted),
            UI.Text(() => VM.HasIf.Value ? add : make).Font(Font.Caption.Sized(13)).LineLimit(1),
            Spacer()
          )
          .Padding(new Insets(0, 10, 0, 10))
          .Frame(maxWidth: float.PositiveInfinity, height: AddHeight),
        () => VM.Add,
        ButtonStyle.Plain
      )
      .Named("Add case");
  }

  // Moving it sets the compared value by hand, so every case can be seen without playing a level.
  private View TryRow() =>
    VStack(
        spacing: 2,
        HStack(
          spacing: 6,
          UI.Text(L("값 움직여 보기", "Try a value"))
            .Font(Font.Caption.Sized(12))
            .Foreground(Colors.Muted)
            .LineLimit(1),
          UI.Text(() => VM.TryName.Value)
            .Font(Font.Caption.Sized(12))
            .Foreground((UiColor)UiPalette.SoftAccent)
            .LineLimit(1),
          Spacer(minLength: 4),
          UI.Text(() => VM.TryNumber.Value).Font(Font.Caption.Sized(13)).LineLimit(1)
        ),
        Slider(null, Bind(() => VM.Tried), 0, 1, step: 0, showsValue: false).Frame(height: TryHeight).Named("Try value")
      )
      .Padding(new Insets(6, 4, 0, 4));
}
