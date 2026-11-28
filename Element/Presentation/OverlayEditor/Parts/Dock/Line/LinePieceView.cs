using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
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

  private const float RemoveSize = 20,
    OperatorRadius = 8;

  private readonly LineViewModel _line;
  private readonly UiColor _ring = UiPalette.SoftAccent;
  private readonly UiColor _clear = FieldLook.Clear;
  private readonly UiColor _removeHover = new Color(1, 1, 1, .1f);

  public LinePieceView(ItemSlot<LinePieceViewModel> slot, LineViewModel line)
    : base(slot) => _line = line;

  public override View Body =>
    VM.Kind switch
    {
      LinePieceKind.Operator => Operator(),
      LinePieceKind.Group => new LineSlotView(VM.Slots[0], _line),
      _ => Chip(),
    };

  // A pill: its mark or swatch, then what it says. Picked, it wears a ring and keeps its fill, and shows its × to be taken out.
  // The whole pill is the button, so it lights as itself; the × inside is a button of its own.
  private View Chip() =>
    Button(
        HStack(
            spacing: 2,
            HStack(
              spacing: 6,
              If(
                () => VM.HasSwatch,
                () =>
                  Spacer().Frame(width: 14, height: 14).Background(() => VM.Swatch, 7).Padding(new Insets(0, 2, 0, 0)),
                () => TypeMarks.Mark(() => VM.Type, 18)
              ),
              // Words not typed yet: the hollow box they have everywhere. A piece keeps its view as it is typed into.
              If(
                () => VM.Blank,
                () => BlankWords.Box(17),
                () => Text(() => VM.Label).Font(Font.Body.Sized(17)).Foreground(Colors.Foreground).LineLimit(1)
              ),
              // A value read at decimals of its own says how many, quietly.
              If(
                () => VM.Digits.Length > 0,
                () =>
                  Text(() => VM.Digits).Font(Font.Body.Sized(14)).Foreground(Colors.Muted).LineLimit(1).Named("Digits")
              )
            ),
            If(() => VM.Removable, RemoveKey)
          )
          .Padding(new Insets(0, 7, 0, 12))
          .Frame(height: ChipHeight),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(FieldLook.Chip, ChipHeight / 2))
      .Named("Piece " + VM.Key)
      .Overlay(Capsule().Stroke(() => VM.Selected ? _ring : _clear, FieldLook.Ring).AllowsHitTesting(false));

  // The picked piece's ×: it is taken out, where it stands.
  private View RemoveKey() =>
    Button(
        Image(ElementIcons.Of(UiIconKind.Close), 10)
          .Foreground(Colors.Muted)
          .Frame(width: RemoveSize, height: RemoveSize),
        _line.Remove,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(_clear, RemoveSize / 2, hovered: _removeHover))
      .Named("Remove piece")
      .Frame(width: RemoveSize, height: RemoveSize)
      .Padding(new Insets(0, 0, 0, 4));

  // A sign, lit under the pointer in the shape of the ring it wears when picked.
  private View Operator() =>
    Button(
        HStack(
            spacing: 0,
            Text(() => VM.Label).Font(Font.Body.Sized(20).Bolded()).Foreground(Colors.Foreground),
            If(() => VM.Removable, RemoveKey)
          )
          .Padding(0, 7)
          .Frame(height: ChipHeight - 4),
        () => _line.Pick(VM.Route, VM.Index),
        ButtonStyle.Plain
      )
      .ButtonStyle(SurfaceHover.Of(OperatorRadius))
      .Named("Operator")
      .Overlay(
        RoundedRectangle(OperatorRadius)
          .Stroke(() => VM.Selected ? _ring : _clear, FieldLook.Ring)
          .AllowsHitTesting(false)
      );
}
