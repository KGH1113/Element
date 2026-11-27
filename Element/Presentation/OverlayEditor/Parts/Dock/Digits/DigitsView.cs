using System.Collections.Generic;
using System.Linq;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Digits;

/// <summary>
/// The picked value's own number in the middle of a quiet card, written out
/// to four decimals: solid as far as it is read here, faint past it. Each
/// digit is pressed where it stands (as tall as the card, so it is easy to
/// hit); under the pointer the number shows what it would read, its last
/// digit in the highlight color. Under the card the keypad's four
/// operators, as tall as its keys, to carry on a calculation. It fills the
/// place it has, the whole input method's or what a folded condition leaves.
/// </summary>
internal sealed class DigitsView : View<DigitsViewModel>
{
  private const float Gap = KeypadView.Gap,
    Radius = 16,
    // What a digit is beyond what is read: there, but plainly not taken.
    Faint = .28f;

  private readonly IReadOnlyList<KeypadKeyViewModel> _operators;
  private readonly float _height,
    _card,
    _operatorRow,
    _size;
  private readonly UiColor _ink = UiPalette.Foreground;
  private readonly UiColor _hot = UiPalette.SoftAccent;

  /// <param name="keypad">The keypad whose operator keys stand under the number.</param>
  /// <param name="height">How tall a place it has; under a folded condition the number is a little smaller.</param>
  public DigitsView(DigitsViewModel vm, KeypadViewModel keypad, float height)
    : base(vm)
  {
    _operators = new[] { '/', '*', '-', '+' }.Select(op => keypad.Keys.First(key => key.Character == op)).ToArray();
    _height = height;
    _operatorRow = KeypadView.KeyHeightIn(height);
    _card = height - _operatorRow - Gap;
    _size = height < KeypadView.MethodHeight ? 34 : 40;
  }

  public override View Body =>
    VStack(
        spacing: Gap,
        // The card is a shape of its own, so it can be found; the number stands in its middle.
        ZStack(
            RoundedRectangle(Radius).Fill(UiPalette.EditorControl).Named("Digits number"),
            HStack(spacing: 0, VM.Glyphs.Select(Glyph).ToArray())
          )
          .Frame(maxWidth: float.PositiveInfinity, height: _card, alignment: Alignment.Center),
        HStack(
          spacing: Gap,
          _operators
            .Select(key => (View)new KeypadKeyView(key, 24, _operatorRow).Frame(maxWidth: float.PositiveInfinity))
            .ToArray()
        )
      )
      .Frame(width: KeypadView.Width, height: _height, alignment: Alignment.Top);

  // One part of the number, pressed where it stands.
  private View Glyph(DigitsGlyphViewModel glyph) =>
    Button(
        Text(() => glyph.Text.Value)
          // The keypad's digits' weight: drawn heavier, neighbours' ink would touch.
          .Font(Font.Body.Sized(_size))
          .Foreground(() => glyph.Hot.Value ? _hot : _ink)
          .Opacity(() => glyph.Solid.Value ? 1f : Faint)
          .LineLimit(1)
          // Room for each glyph's drawn edge, so neighbours never sit on each other.
          .Padding(0, 2.5f)
          .Frame(height: _card, alignment: Alignment.Center),
        () => glyph.Pick,
        ButtonStyle.Plain
      )
      // No fill behind a digit, hovered or pressed: the digit itself turns the highlight color.
      .ButtonStyle(new ButtonSurfaceStyle(FieldLook.Clear, 0, hovered: FieldLook.Clear, pressed: FieldLook.Clear))
      .OnHover(glyph.Hover)
      .Named(glyph.Name)
      .Frame(height: _card);
}
