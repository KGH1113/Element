using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// One chip in a field row: its mark, swatch or letters and what it says; an
/// operator as its plain sign. Where a value stands on its own (a
/// condition's input method) it is the larger pill a value is on the line,
/// its mark on a tile.
/// </summary>
internal sealed class FieldChipView : View<FieldChip>
{
  public const float Height = 22,
    LargeHeight = 26;

  private static readonly UiColor Fill = FieldLook.Mini;

  private readonly bool _large;
  private readonly float _widest;

  /// <param name="large">A value standing on its own: the pill a value is on the line.</param>
  /// <param name="widest">The widest its words may be before they are cut short; 0 for as wide as they are.</param>
  public FieldChipView(ItemSlot<FieldChip> slot, bool large = false, float widest = 0)
    : base(slot)
  {
    _large = large;
    _widest = widest;
  }

  public override View Body
  {
    get
    {
      FieldChip chip = VM;
      if (chip.Operator)
        return Text(chip.Label).Font(Font.Body.Sized(15)).Foreground(Colors.Muted).LineLimit(1);
      if (chip.Muted)
        return Text(chip.Label).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1);
      if (chip.Bare)
        return Text(chip.Label)
          .Font(Font.Caption.Sized(12))
          .LineLimit(1)
          .Padding(0, 9)
          .Frame(height: Height)
          .Background(Fill, Height / 2);
      // A chip is drawn once: its gradient is read as it was given.
      OverlayResolvedGradient gradient = chip.Gradient;
      if (_large)
      {
        View mark =
          gradient != null ? new SwatchDot(() => Color.white, () => gradient).Frame(width: 13, height: 13)
          : chip.HasSwatch ? TypeMarks.Swatch(chip.Swatch, 12)
          : chip.Glyph != null ? TypeMarks.Mark(chip.Glyph, 16)
          : TypeMarks.Mark(chip.Type, 16);
        View words = chip.Blank ? BlankWords.Box(13) : Text(chip.Label).Font(Font.Caption.Sized(13)).LineLimit(1);
        if (_widest > 0 && !chip.Blank)
          words = words.Frame(maxWidth: _widest);
        return HStack(spacing: 5, mark, words)
          .Padding(new Insets(0, 6, 0, 10))
          .Frame(height: LargeHeight)
          .Background(FieldLook.Chip, LargeHeight / 2);
      }
      View lead =
        gradient != null ? new SwatchDot(() => Color.white, () => gradient).Frame(width: 13, height: 13)
        : chip.HasSwatch ? TypeMarks.Swatch(chip.Swatch, 12)
        : chip.Glyph != null ? TypeMarks.Letter(chip.Glyph, 14)
        : TypeMarks.Letter(chip.Type, 14);
      // A pill, its mark only the glyph: the row's own mark already has the tile.
      return HStack(
          spacing: 3,
          lead,
          chip.Blank ? BlankWords.Box(12) : Text(chip.Label).Font(Font.Caption.Sized(12)).LineLimit(1)
        )
        .Padding(new Insets(0, 5, 0, 9))
        .Frame(height: Height)
        .Background(Fill, Height / 2);
    }
  }
}
