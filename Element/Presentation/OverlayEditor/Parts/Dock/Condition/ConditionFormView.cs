using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

/// <summary>
/// The condition's input method, in the keypad's place: five fields one
/// under another, the sentence's words before them. A field shows its values
/// as small chips, or while empty its type's mark and name.
/// </summary>
internal sealed class ConditionFormView : View<ConditionFormViewModel>
{
  private const float Gap = 7,
    WordWidth = 50,
    Radius = 10;

  // Five rows fill the place every input method has.
  private const float RowHeight = (KeypadView.MethodHeight - 4 * Gap) / 5;

  private readonly UiColor _fill = UiPalette.EditorControl;
  private readonly UiColor _bad = UiPalette.Error;
  private readonly UiColor _noEdge = FieldLook.Clear;

  public ConditionFormView(ConditionFormViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(spacing: Gap, Rows())
      .Offset(x: () => VM.Shake.X)
      .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Top);

  private View[] Rows()
  {
    View[] rows = new View[VM.Slots.Count];
    for (int i = 0; i < rows.Length; i++)
      rows[i] = Row(VM.Slots[i]);
    return rows;
  }

  private View Row(ConditionSlotViewModel slot) =>
    HStack(
      spacing: 0,
      Text(slot.Word)
        .Font(Font.Caption.Sized(13))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Frame(width: WordWidth, alignment: Alignment.Leading),
      Button(
          HStack(
              spacing: 6,
              If(
                () => slot.Empty.Value,
                () =>
                  HStack(
                    spacing: 7,
                    Switch(() => slot.Type.Value, type => TypeMarks.Mark(type, 18)),
                    Text(slot.Name).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1)
                  ),
                () =>
                  HStack(
                    spacing: 4,
                    ForEach(() => slot.Chips.Value, chip => chip.Identity, chip => new FieldChipView(chip))
                  )
              ),
              Spacer()
            )
            .Padding(0, 9)
            .Frame(maxWidth: float.PositiveInfinity, height: RowHeight),
          () => slot.Enter,
          ButtonStyle.Plain
        )
        .ButtonStyle(new ButtonSurfaceStyle(_fill, Radius))
        .Overlay(
          RoundedRectangle(Radius).Stroke(() => slot.Bad.Value ? _bad : _noEdge, FieldLook.Edge).AllowsHitTesting(false)
        )
        .Named("Condition slot " + slot.Slot)
    );
}
