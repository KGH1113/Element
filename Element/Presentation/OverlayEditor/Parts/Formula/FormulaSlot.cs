using System;
using Element.Domain.Overlay.Formula;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// One place a formula can be edited: a value in a text, or a number field.
/// <see cref="Key"/> tells the same place apart across redraws.
/// </summary>
internal sealed class FormulaSlot
{
  private readonly Func<OverlayFormula> _read;
  private readonly Action<OverlayFormula> _write;

  public FormulaSlot(string key, bool isText, Func<OverlayFormula> read, Action<OverlayFormula> write)
  {
    Key = key;
    IsText = isText;
    _read = read;
    _write = write;
  }

  public string Key { get; }

  /// <summary>
  /// A value in a text: it may hold words (the song title), and emptying it
  /// removes it. A number field never holds words and keeps its last number.
  /// </summary>
  public bool IsText { get; }

  public OverlayFormula Read() => _read() ?? new OverlayFormula();

  public void Write(OverlayFormula formula) => _write(formula);
}
