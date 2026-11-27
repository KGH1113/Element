using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// A variable laid out on the line: its pieces side by side, in a thin
/// outline unless it is the field's own. Empty, it says what goes in it with
/// that type's mark, and a click makes it the place a value goes, its outline
/// the highlight color. What is missing is outlined in the warning color
/// once finishing was refused.
/// </summary>
internal sealed class LineSlotView : View<LineSlotViewModel>
{
  private const float Radius = 12;

  private readonly LineViewModel _line;
  private readonly UiColor _ring = UiPalette.SoftAccent;
  private readonly UiColor _edge = FieldLook.SlotEdge;
  private readonly UiColor _fill = FieldLook.Slot;
  private readonly UiColor _clear = FieldLook.Clear;
  private readonly UiColor _bad = UiPalette.Error;
  private readonly UiColor _ink = Colors.Foreground;
  private readonly UiColor _quiet = Colors.Muted;

  public LineSlotView(LineSlotViewModel vm, LineViewModel line)
    : base(vm) => _line = line;

  public override View Body =>
    If(
        () => VM.Pieces.Length == 0,
        // An empty place says what goes in it: the type's mark and the place's name.
        () =>
          Button(
              HStack(
                spacing: 6,
                TypeMarks.Mark(() => VM.Type, 16),
                Text(() => VM.Placeholder)
                  .Font(Font.Body.Sized(14))
                  .Foreground(() => VM.Targeted ? _ink : _quiet)
                  .LineLimit(1)
              ),
              () => _line.Pick(VM.Route, -1),
              ButtonStyle.Plain
            )
            .Named("Empty slot " + VM.Key)
            .Padding(new Insets(0, 4, 0, 8))
            .Frame(height: 26),
        () => HStack(spacing: 5, ForEach(() => VM.Pieces, piece => piece.Key, slot => new LinePieceView(slot, _line)))
      )
      .Padding(() => VM.Root && VM.Pieces.Length > 0 ? new Insets(0, 0, 0, 0) : new Insets(2, 4, 2, 4))
      // The field's own pieces stand on the line; a slot inside a value is a thin outline.
      .Background(() => VM.Root ? _clear : _fill, Radius)
      .Overlay(
        RoundedRectangle(Radius)
          .Stroke(
            () =>
              VM.Root && VM.Pieces.Length > 0 ? _clear
              // After finishing was refused, an empty place that must hold something says so.
              : VM.Missing && _line.Alarmed ? _bad
              : VM.Targeted ? _ring
              : _edge,
            FieldLook.Edge
          )
      );
}
