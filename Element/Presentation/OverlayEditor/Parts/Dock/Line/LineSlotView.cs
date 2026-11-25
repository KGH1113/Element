using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// A variable laid out on the line: its pieces side by side, in a soft box
/// unless it is the field's own. Empty, it says what goes in it with that
/// type's mark, and a click makes it the place a value goes; one empty where
/// something must be reads warm.
/// </summary>
internal sealed class LineSlotView : View<LineSlotViewModel>
{
  private readonly LineViewModel _line;
  private readonly UiColor _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .4f);
  private readonly UiColor _box = new Color(1, 1, 1, .06f);
  private readonly UiColor _warn = Color.Lerp(UiPalette.EditorControl, UiPalette.Error, .25f);
  private readonly UiColor _clear = new Color(0, 0, 0, 0);

  public LineSlotView(LineSlotViewModel vm, LineViewModel line)
    : base(vm) => _line = line;

  public override View Body =>
    If(
        () => VM.Pieces.Length == 0,
        () =>
          Button(
              HStack(
                spacing: 5,
                TypeMarks.Mark(() => VM.Type, 16),
                Text(() => VM.Placeholder).Font(Font.Body.Sized(15)).Foreground(Colors.Muted).LineLimit(1)
              ),
              () => _line.Pick(VM.Route, -1),
              ButtonStyle.Plain
            )
            .Named("Empty slot")
            .Padding(0, 8)
            .Frame(height: 32),
        () => HStack(spacing: 4, ForEach(() => VM.Pieces, piece => piece.Key, slot => new LinePieceView(slot, _line)))
      )
      .Padding(() => VM.Root ? new Insets(0, 0, 0, 0) : new Insets(3, 4, 3, 4))
      .Background(
        () =>
          VM.Targeted ? _lit
          : VM.Missing ? _warn
          : VM.Root ? _clear
          : _box,
        9
      );
}
