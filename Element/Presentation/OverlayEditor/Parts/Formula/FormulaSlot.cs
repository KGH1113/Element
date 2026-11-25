using System;
using Element.Domain.Overlay.Variables;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// One field the dock opens as a variable: a number, a color, true or false,
/// or a value in a text (Any). <see cref="Key"/> tells the same field apart
/// across redraws; <see cref="Type"/> is what it must give.
/// </summary>
internal sealed class FormulaSlot
{
  private readonly Func<OverlayVariable> _read;
  private readonly Action<OverlayVariable> _write;
  private readonly Func<string> _placeholder;

  /// <param name="placeholder">What the dock shows while the variable is empty, as rich text; null for its hint.</param>
  public FormulaSlot(
    string key,
    OverlayValueType type,
    Func<OverlayVariable> read,
    Action<OverlayVariable> write,
    Func<string> placeholder = null
  )
  {
    Key = key;
    Type = type;
    _read = read;
    _write = write;
    _placeholder = placeholder;
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

  /// <summary>What shows while the variable is empty, such as a color's gradient; null for nothing.</summary>
  public string Placeholder() => _placeholder?.Invoke();
}
