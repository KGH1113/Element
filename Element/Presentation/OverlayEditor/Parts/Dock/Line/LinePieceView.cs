using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// One piece on the line. A value is a chip with its type's mark (a color its
/// swatch); an operator is its sign; a condition lays its slots out in a row,
/// "if [ ] ≥ [ ] then [ ] otherwise [ ]"; a calculation in a text is boxed.
/// A click picks what was clicked: the piece, the comparison, or a slot.
/// </summary>
internal sealed class LinePieceView : View<LinePieceViewModel>
{
  public const float ChipHeight = 34;

  private readonly LineViewModel _line;
  private readonly UiColor _chip = UiPalette.EditorControl;
  private readonly UiColor _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .45f);
  private readonly UiColor _box = new Color(1, 1, 1, .04f);
  private readonly UiColor _litBox = Color.Lerp(new Color(1, 1, 1, .04f), UiPalette.SoftAccent, .22f);
  private readonly UiColor _clear = new Color(0, 0, 0, 0);

  public LinePieceView(ItemSlot<LinePieceViewModel> slot, LineViewModel line)
    : base(slot) => _line = line;

  public override View Body =>
    VM.Kind switch
    {
      LinePieceKind.Operator => Operator(),
      LinePieceKind.Condition => Condition(),
      LinePieceKind.Group => new LineSlotView(VM.Slots[0], _line),
      _ => Chip(),
    };

  private View Chip() =>
    Button(
        HStack(
          spacing: 6,
          If(
            () => VM.HasSwatch,
            () => Spacer().Frame(width: 14, height: 14).Background(() => VM.Swatch, 7),
            () => TypeMarks.Mark(() => VM.Type, 18)
          ),
          Text(() => VM.Label).Font(Font.Body.Sized(18)).Foreground(Colors.Foreground).LineLimit(1)
        ),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .Named("Piece " + VM.Key)
      .Padding(0, 10)
      .Frame(height: ChipHeight)
      .Background(() => VM.Selected ? _lit : _chip, 9);

  private View Operator() =>
    Button(
        Text(() => VM.Label).Font(Font.Body.Sized(22)).Foreground(Colors.Foreground),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .Named("Operator")
      .Padding(0, 7)
      .Frame(height: ChipHeight)
      .Background(() => VM.Selected ? _lit : _clear, 8);

  // if [left] [≥] [right] then [result] otherwise [default], the words picking the condition itself.
  private View Condition() =>
    HStack(
        spacing: 5,
        Word(FormulaWords.Current.ConditionStart),
        new LineSlotView(VM.Slots[0], _line),
        Button(
            Text(() => VM.Compare).Font(Font.Body.Sized(18)).Foreground(Colors.Foreground),
            () => _line.Pick(Into(1), 0),
            ButtonStyle.Plain
          )
          .Named("Comparison")
          .Padding(0, 9)
          .Frame(height: ChipHeight - 4)
          .Background(() => VM.CompareSelected ? _lit : _chip, 8),
        new LineSlotView(VM.Slots[2], _line),
        Word(FormulaWords.Current.Then),
        new LineSlotView(VM.Slots[3], _line),
        Word(FormulaWords.Current.Otherwise),
        new LineSlotView(VM.Slots[4], _line)
      )
      .Padding(3, 6)
      .Background(() => VM.Selected ? _litBox : _box, 10);

  private View Word(string word) =>
    Button(
        Text(word).Font(Font.Body.Sized(15)).Foreground(Colors.Muted),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .Named("Condition word");

  // The route into slot <paramref name="slot"/> of this condition.
  private FormulaStep[] Into(int slot)
  {
    FormulaStep[] route = new FormulaStep[VM.Route.Length + 1];
    VM.Route.CopyTo(route, 0);
    route[route.Length - 1] = new FormulaStep(VM.Index, slot);
    return route;
  }
}
