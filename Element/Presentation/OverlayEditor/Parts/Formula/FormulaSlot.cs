using System;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// One place a formula can be edited: a value in a text, a number field, or
/// a color. <see cref="Key"/> tells the same place apart across redraws.
/// </summary>
internal sealed class FormulaSlot
{
  private readonly Func<OverlayFormula> _read;
  private readonly Action<OverlayFormula> _write;
  private readonly Func<string> _otherwise;
  private readonly Action<OverlayValueDefinition> _ownColor;

  /// <param name="otherwise">What shows when none of the formula's cases holds, as the dock reads it; null for nothing to say.</param>
  /// <param name="ownColor">A color: a color value picked with no case being written is its own color.</param>
  public FormulaSlot(
    string key,
    OverlayFormulaKind kind,
    Func<OverlayFormula> read,
    Action<OverlayFormula> write,
    Func<string> otherwise = null,
    Action<OverlayValueDefinition> ownColor = null
  )
  {
    Key = key;
    Kind = kind;
    _read = read;
    _write = write;
    _otherwise = otherwise;
    _ownColor = ownColor;
  }

  /// <summary>A value in a text (<paramref name="isText"/>) or a number field.</summary>
  public FormulaSlot(string key, bool isText, Func<OverlayFormula> read, Action<OverlayFormula> write)
    : this(key, isText ? OverlayFormulaKind.Text : OverlayFormulaKind.Number, read, write) { }

  public string Key { get; }

  /// <summary>
  /// What it holds: a number field never holds words and keeps its last
  /// number; a value in a text may hold words (the song title), and emptying
  /// it removes it; a color holds cases that give colors, and its own color
  /// is what shows otherwise.
  /// </summary>
  public OverlayFormulaKind Kind { get; }

  public bool IsText => Kind == OverlayFormulaKind.Text;

  public OverlayFormula Read() => _read() ?? new OverlayFormula();

  public void Write(OverlayFormula formula) => _write(formula);

  /// <summary>What shows when no case holds, as the dock reads it; null for nothing to say.</summary>
  public string Otherwise() => _otherwise?.Invoke();

  /// <summary>A color value picked while no case is written becomes the color's own.</summary>
  public void OwnColor(OverlayValueDefinition value) => _ownColor?.Invoke(value);
}
