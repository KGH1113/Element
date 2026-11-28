using System;
using Element.Domain.Overlay.Variables;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// One field the dock opens as a variable: a number, a color, true or false,
/// a point, or a value in a text (Any). <see cref="Key"/> tells the same field apart
/// across redraws; <see cref="Type"/> is what it must give.
/// </summary>
internal sealed class FormulaSlot
{
  private readonly Func<OverlayVariable> _read;
  private readonly Action<OverlayVariable> _write;

  public FormulaSlot(string key, OverlayValueType type, Func<OverlayVariable> read, Action<OverlayVariable> write)
  {
    Key = key;
    Type = type;
    _read = read;
    _write = write;
  }

  public string Key { get; }

  /// <summary>
  /// What it must give. A number field keeps its last number while nothing
  /// valid is typed; a value in a text (Any) is removed once emptied.
  /// </summary>
  public OverlayValueType Type { get; }

  /// <summary>A value in a text: emptying it removes it.</summary>
  public bool IsText => Type == OverlayValueType.Any;

  public OverlayVariable Read() => _read() ?? new OverlayVariable();

  public void Write(OverlayVariable variable) => _write(variable);
}
