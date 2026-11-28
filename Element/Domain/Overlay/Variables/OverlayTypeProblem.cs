namespace Element.Domain.Overlay.Variables;

/// <summary>
/// What is wrong with a variable for its field, and where: the piece at
/// fault (null for the variable as a whole), the slot of a condition it is
/// in, what was needed and what was found.
/// </summary>
internal sealed class OverlayTypeProblem
{
  public OverlayTypeProblem(
    OverlayTypeProblemKind kind,
    OverlayPiece piece,
    OverlayValueType needed,
    OverlayValueType found,
    int slot = -1,
    OverlayPiece condition = null
  )
  {
    Kind = kind;
    Piece = piece;
    Needed = needed;
    Found = found;
    Slot = slot;
    Condition = condition;
  }

  public OverlayTypeProblemKind Kind { get; }

  /// <summary>The piece at fault; null for the variable as a whole.</summary>
  public OverlayPiece Piece { get; }

  public OverlayValueType Needed { get; }

  public OverlayValueType Found { get; }

  /// <summary>The slot of <see cref="Condition"/> the problem is in; -1 when it isn't in one.</summary>
  public int Slot { get; }

  /// <summary>The innermost condition the problem is in; null when it isn't in one.</summary>
  public OverlayPiece Condition { get; }
}
