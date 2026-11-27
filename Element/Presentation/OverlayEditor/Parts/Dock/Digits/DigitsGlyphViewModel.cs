using System;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Digits;

/// <summary>
/// One part of the picked value's number as its input method writes it: the
/// whole part, the point, or one decimal. Solid while the value is read that
/// far, faint past it; pressed, the value is read up to it (the whole part
/// and the point read it whole). Under the pointer it shows what it would read.
/// </summary>
internal sealed class DigitsGlyphViewModel : ViewModel
{
  /// <param name="digits">How many decimals pressing it reads the value at.</param>
  /// <param name="name">Names the drawn part, for the checks: "Digits 2", "Digits point".</param>
  /// <param name="pick">Reads the picked value at that many decimals.</param>
  /// <param name="hover">The pointer came over it (true) or left it.</param>
  public DigitsGlyphViewModel(int digits, string name, Action<int> pick, Action<int, bool> hover)
  {
    Digits = digits;
    Name = name;
    Pick = Command(() => pick(digits));
    _hover = hover;
  }

  private readonly Action<int, bool> _hover;

  /// <summary>How many decimals pressing it reads the value at.</summary>
  public readonly int Digits;

  public readonly string Name;

  /// <summary>What it writes: the whole part, ".", or one digit; empty while there is no number.</summary>
  public readonly State<string> Text = new("");

  /// <summary>The value is read this far: it is drawn solid, not faint.</summary>
  public readonly State<bool> Solid = new(false);

  /// <summary>Under the pointer, the last digit it would read: drawn in the highlight color.</summary>
  public readonly State<bool> Hot = new(false);

  public Command Pick { get; }

  public void Hover(bool over) => _hover(Digits, over);
}
