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
/// swatch), a condition too: its five slots are its input method's. An
/// operator is its sign; a group is outlined around what it holds. A click
/// picks the piece, and the picked one wears a ring in the highlight color.
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
          // Words not typed yet: the hollow box they have everywhere. A piece keeps its view as it is typed into.
          If(
            () => VM.Blank,
            () => BlankWords.Box(17),
            () => Text(() => VM.Label).Font(Font.Body.Sized(17)).Foreground(Colors.Foreground).LineLimit(1)
          )
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
}
