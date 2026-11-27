using System.Collections.Generic;
using System.Linq;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Digits;

/// <summary>
/// The picked value's own number as keys, one for each count of decimals:
/// whole and one and two decimals on top, three and four under them, the
/// count read here lit like a chosen comparison. Under them the keypad's
/// four operators, as tall as its keys, to carry on a calculation. It
/// fills the place it has, the whole input method's or what a folded
/// condition leaves.
/// </summary>
internal sealed class DigitsView : View<DigitsViewModel>
{
  private const float Gap = KeypadView.Gap,
    Radius = 16;

  private readonly IReadOnlyList<KeypadKeyViewModel> _operators;
  private readonly float _height,
    _row,
    _operatorRow;
  private readonly UiColor _rest = UiPalette.EditorControl;
  private readonly UiColor _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .34f);
  private readonly UiColor _edge = UiPalette.SoftAccent;
  private readonly UiColor _noEdge = FieldLook.Clear;
  private readonly UiColor _hover = UiPalette.Wash(UiPalette.EditorControl, .05f);

  /// <param name="keypad">The keypad whose operator keys stand under the numbers.</param>
  /// <param name="height">How tall a place it has.</param>
  public DigitsView(DigitsViewModel vm, KeypadViewModel keypad, float height)
    : base(vm)
  {
    _operators = new[] { '/', '*', '-', '+' }.Select(op => keypad.Keys.First(key => key.Character == op)).ToArray();
    _height = height;
    _operatorRow = KeypadView.KeyHeightIn(height);
    _row = (height - _operatorRow - Gap * 2) / 2;
  }

  public override View Body =>
    VStack(
        spacing: Gap,
        HStack(spacing: Gap, Choice(VM.Choices[0]), Choice(VM.Choices[1]), Choice(VM.Choices[2])),
        HStack(spacing: Gap, Choice(VM.Choices[3]), Choice(VM.Choices[4])),
        HStack(
          spacing: Gap,
          _operators
            .Select(key =>
              (View)new KeypadKeyView(key, 24, false, _operatorRow).Frame(maxWidth: float.PositiveInfinity)
            )
            .ToArray()
        )
      )
      .Frame(width: KeypadView.Width, height: _height, alignment: Alignment.Top);

  // One count of decimals: the number it gives, in the middle of its key.
  private View Choice(DigitsChoiceViewModel choice) =>
    Button(
        Text(() => choice.Number.Value)
          .Font(Font.Body.Sized(20).Bolded())
          .LineLimit(1)
          .TextAlignment(TextAlign.Center)
          .Frame(maxWidth: float.PositiveInfinity, height: _row, alignment: Alignment.Center),
        () => choice.Pick,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => choice.Lit.Value ? _lit : _rest, Radius, hovered: _hover))
      .Overlay(
        RoundedRectangle(Radius)
          .Stroke(() => choice.Lit.Value ? _edge : _noEdge, FieldLook.Edge)
          .AllowsHitTesting(false)
      )
      .Named(choice.Name)
      .Frame(maxWidth: float.PositiveInfinity, height: _row);
}
