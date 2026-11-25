using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>One chip in a field row: its mark, swatch or letters and what it says; an operator as its plain sign.</summary>
internal sealed class FieldChipView : View<FieldChip>
{
  public const float Height = 24;

  private static readonly UiColor Fill = new Color(1, 1, 1, .07f);

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
          .Font(Font.Caption.Sized(13))
          .LineLimit(1)
          .Padding(0, 8)
          .Frame(height: Height)
          .Background(Fill, 7);
      View lead =
        chip.Gradient != null ? new SwatchDot(() => Color.white, () => VM.Gradient).Frame(width: 13, height: 13)
        : chip.HasSwatch ? TypeMarks.Swatch(chip.Swatch, 12)
        : chip.Glyph != null ? Glyph(chip.Glyph)
        : TypeMarks.Mark(chip.Type, 15);
      return HStack(spacing: 4, lead, Text(chip.Label).Font(Font.Caption.Sized(13)).LineLimit(1))
        .Padding(0, 7)
        .Frame(height: Height)
        .Background(Fill, 7);
    }
  }

  private static View Glyph(string glyph) =>
    Text(glyph)
      .Font(Font.Caption.Sized(10))
      .Foreground(Colors.Muted)
      .LineLimit(1)
      .TextAlignment(TextAlign.Center)
      .Frame(width: 15, height: 15)
      .Background(new Color(1, 1, 1, .1f), 4);
}
