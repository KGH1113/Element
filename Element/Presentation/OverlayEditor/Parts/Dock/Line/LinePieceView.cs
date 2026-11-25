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
/// One piece on the line. A value is a pill with its type's mark (a color its
/// swatch); an operator is its sign; a condition lays its slots out in a row,
/// "if [ ] ≥ [ ] then [ ] otherwise [ ]"; a calculation in a text is outlined.
/// A click picks what was clicked: the piece, the comparison, or a slot, and
/// the picked one wears a ring in the highlight color.
/// </summary>
internal sealed class LinePieceView : View<LinePieceViewModel>
{
  public const float ChipHeight = 32;

  private readonly LineViewModel _line;
  private readonly UiColor _ring = UiPalette.SoftAccent;
  private readonly UiColor _clear = FieldLook.Clear;

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

  // A pill: its mark or swatch, then what it says. Picked, it wears a ring and keeps its fill.
  private View Chip() =>
    Button(
        HStack(
          spacing: 6,
          If(
            () => VM.HasSwatch,
            () => Spacer().Frame(width: 14, height: 14).Background(() => VM.Swatch, 7).Padding(new Insets(0, 2, 0, 0)),
            () => TypeMarks.Mark(() => VM.Type, 18)
          ),
          Text(() => VM.Label).Font(Font.Body.Sized(17)).Foreground(Colors.Foreground).LineLimit(1)
        ),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .Named("Piece " + VM.Key)
      .Padding(new Insets(0, 7, 0, 12))
      .Frame(height: ChipHeight)
      .Background(FieldLook.Chip, ChipHeight / 2)
      .Overlay(Capsule().Stroke(() => VM.Selected ? _ring : _clear, FieldLook.Ring));

  private View Operator() =>
    Button(
        Text(() => VM.Label).Font(Font.Body.Sized(20).Bolded()).Foreground(Colors.Foreground),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .Named("Operator")
      .Padding(0, 7)
      .Frame(height: ChipHeight - 4)
      .Overlay(RoundedRectangle(8).Stroke(() => VM.Selected ? _ring : _clear, FieldLook.Ring));

  // if [left] [≥] [right] then [result] otherwise [default], the words picking the condition itself.
  private View Condition() =>
    HStack(
        spacing: 6,
        Word(FormulaWords.Current.ConditionStart),
        new LineSlotView(VM.Slots[0], _line),
        Button(
            Text(() => VM.Compare).Font(Font.Body.Sized(17).Bolded()).Foreground(Colors.Foreground),
            () => _line.Pick(Into(1), 0),
            ButtonStyle.Plain
          )
          .Named("Comparison")
          .Padding(0, 11)
          .Frame(height: ChipHeight - 4)
          .Background(FieldLook.Chip, (ChipHeight - 4) / 2)
          .Overlay(Capsule().Stroke(() => VM.CompareSelected ? _ring : _clear, FieldLook.Ring)),
        new LineSlotView(VM.Slots[2], _line),
        Word(FormulaWords.Current.Then),
        new LineSlotView(VM.Slots[3], _line),
        Word(FormulaWords.Current.Otherwise),
        new LineSlotView(VM.Slots[4], _line)
      )
      .Padding(4, 10)
      .Background(FieldLook.Holder, 15)
      .Overlay(RoundedRectangle(15).Stroke(() => VM.Selected ? _ring : _clear, FieldLook.Ring));

  private View Word(string word) =>
    Button(
        Text(word).Font(Font.Body.Sized(14)).Foreground(Colors.Muted),
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
