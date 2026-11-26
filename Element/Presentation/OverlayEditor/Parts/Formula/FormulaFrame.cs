using Element.Domain.Overlay.Variables;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// One step of the dock's path: a variable that is open, what it must give,
/// and where it sits: the field itself (slot -1), or slot <see cref="Slot"/>
/// of <see cref="Owner"/> (a condition or a group).
/// </summary>
internal sealed class FormulaFrame
{
  public FormulaFrame(OverlayVariable variable, OverlayValueType type, int slot, bool mayBeEmpty, OverlayPiece owner)
  {
    Variable = variable;
    Type = type;
    Slot = slot;
    MayBeEmpty = mayBeEmpty;
    Owner = owner;
  }

  public OverlayVariable Variable { get; }

  public OverlayValueType Type { get; }

  /// <summary>The slot of <see cref="Owner"/> it is; -1 for the field.</summary>
  public int Slot { get; }

  /// <summary>Nothing in it is fine: a condition's answer in words, which then shows nothing.</summary>
  public bool MayBeEmpty { get; }

  /// <summary>The condition or group it belongs to; null for the field.</summary>
  public OverlayPiece Owner { get; }

  /// <summary>The piece picked outside when it was opened, picked again on the way back.</summary>
  public int Outer { get; set; } = -1;
}
