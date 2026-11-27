using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Digits;

/// <summary>
/// The input method of a value with decimals (progress, X-Accuracy, BPM):
/// its own number at each count of decimals, from whole to four, the count
/// it is read at here lit. That number is what a condition compares and a
/// text writes, cut the way the game writes that value. Under them, the
/// operators that carry on a calculation.
/// </summary>
internal sealed class DigitsViewModel : ViewModel
{
  private readonly StringBuilder _number = new(16);

  /// <param name="pick">Reads the picked value at that many decimals.</param>
  public DigitsViewModel(Action<int> pick)
  {
    DigitsChoiceViewModel[] choices = new DigitsChoiceViewModel[OverlayValueNumbers.Most + 1];
    for (int digits = 0; digits < choices.Length; digits++)
      choices[digits] = Own(new DigitsChoiceViewModel(digits, pick));
    Choices = choices;
  }

  /// <summary>Whole, then one to four decimals.</summary>
  public readonly IReadOnlyList<DigitsChoiceViewModel> Choices;

  /// <summary>
  /// Shows <paramref name="value"/>'s number at each count, as
  /// <paramref name="numbers"/> have it now (the game's, or the value's
  /// example), the count it is read at lit.
  /// </summary>
  public void Show(OverlayPiece value, IOverlayNumberSource numbers)
  {
    if (value == null || value.Kind != OverlayPieceKind.Value)
      return;
    OverlayValueDefinition definition = OverlayValueCatalog.Get(value.Value);
    double raw = double.NaN;
    numbers?.TryGetNumber(value.Value, out raw);
    int read = OverlayValueNumbers.DigitsOf(value, definition);
    foreach (DigitsChoiceViewModel choice in Choices)
    {
      choice.Lit.Value = choice.Digits == read;
      if (double.IsNaN(raw))
      {
        choice.Number.Value = "—";
        continue;
      }
      _number
        .Clear()
        .AppendScaled(OverlayValueNumbers.Scaled(raw, choice.Digits, definition.Rounding), choice.Digits, true);
      choice.Number.Value = _number.ToString();
    }
  }
}
