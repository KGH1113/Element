using System;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Digits;

/// <summary>
/// One count of decimals for the picked value, as a key that says the
/// value's own number at that count ("99.52"). Lit while it is the count
/// read here; pressed, it becomes it.
/// </summary>
internal sealed class DigitsChoiceViewModel : ViewModel
{
  /// <param name="pick">Reads the picked value at this many decimals.</param>
  public DigitsChoiceViewModel(int digits, Action<int> pick)
  {
    Digits = digits;
    Name = "Digits " + digits;
    Pick = Command(() => pick(digits));
  }

  /// <summary>How many decimals it reads the value at.</summary>
  public readonly int Digits;

  /// <summary>Names the drawn key, for the checks: "Digits 2".</summary>
  public readonly string Name;

  /// <summary>The value's number at this many decimals, as it would read: "99.52".</summary>
  public readonly State<string> Number = new("");

  /// <summary>It is the count the value is read at here.</summary>
  public readonly State<bool> Lit = new(false);

  public Command Pick { get; }
}
