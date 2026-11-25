using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>One chip in a field row: its mark, swatch or letters and what it says; an operator as its plain sign.</summary>
internal sealed class FieldChipView : View<FieldChip>
{
  public const float Height = 22;

  private static readonly UiColor Fill = FieldLook.Mini;

  public FieldChipView(ItemSlot<FieldChip> slot)
    : base(slot) { }

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
      View lead =
        gradient != null ? new SwatchDot(() => Color.white, () => gradient).Frame(width: 13, height: 13)
        : chip.HasSwatch ? TypeMarks.Swatch(chip.Swatch, 12)
        : chip.Glyph != null ? TypeMarks.Letter(chip.Glyph, 14)
        : TypeMarks.Letter(chip.Type, 14);
      // A pill, its mark only the glyph: the row's own mark already has the tile.
      return HStack(spacing: 3, lead, Text(chip.Label).Font(Font.Caption.Sized(12)).LineLimit(1))
        .Padding(new Insets(0, 5, 0, 9))
        .Frame(height: Height)
        .Background(Fill, Height / 2);
    }
  }
}
