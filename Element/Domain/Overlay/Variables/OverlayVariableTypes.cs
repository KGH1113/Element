using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// What type a variable gives, and whether it can be what its field needs.
/// Numbers are joined only by operators, and only numbers can be; pieces
/// side by side show as they are, which only a field of type Any takes. A
/// condition gives what its result and default give, which must be what the
/// field needs; it compares two numbers by a comparison, which is a value
/// too: its slot takes one, or a condition that gives one.
/// </summary>
internal static class OverlayVariableTypes
{
  private const int MaxDepth = 16;

  /// <summary>What a piece gives, as far as can be known before the game runs; Any for an empty condition.</summary>
  public static OverlayValueType TypeOf(OverlayPiece piece) => TypeOf(piece, 0);

  /// <summary>What a variable gives: one piece's type, Number for a calculation, Any for pieces side by side.</summary>
  public static OverlayValueType TypeOf(OverlayVariable variable) => TypeOf(variable, 0);

  /// <summary>
  /// The first thing that keeps <paramref name="variable"/> from being of
  /// type <paramref name="needed"/>; null when it can be. An empty variable
  /// is a problem unless <paramref name="mayBeEmpty"/>.
  /// </summary>
  public static OverlayTypeProblem Check(OverlayVariable variable, OverlayValueType needed, bool mayBeEmpty = false) =>
    Check(variable, needed, mayBeEmpty, null, -1, 0);

  /// <summary>Whether a value of <paramref name="found"/> can go where <paramref name="needed"/> is.</summary>
  public static bool Fits(OverlayValueType needed, OverlayValueType found) =>
    needed == OverlayValueType.Any || found == OverlayValueType.Any || needed == found;

  /// <summary>The type a catalog value gives.</summary>
  public static OverlayValueType TypeOf(OverlayValueDefinition value) =>
    value.Kind switch
    {
      OverlayValueKind.Number or OverlayValueKind.Time => OverlayValueType.Number,
      OverlayValueKind.Color => OverlayValueType.Color,
      OverlayValueKind.Bool => OverlayValueType.Bool,
      OverlayValueKind.Any => OverlayValueType.Any,
      OverlayValueKind.Compare => OverlayValueType.Compare,
      OverlayValueKind.Point => OverlayValueType.Point,
      _ => OverlayValueType.Text,
    };

  /// <summary>The live values a variable reads, in its slots too.</summary>
  public static OverlayValueMask Mask(OverlayVariable variable, int depth)
  {
    OverlayValueMask mask = OverlayValueMask.None;
    if (variable == null || depth > MaxDepth)
      return mask;
    for (int i = 0; i < variable.Count; i++)
    {
      OverlayPiece piece = variable[i];
      if (piece.Kind == OverlayPieceKind.Value)
        mask |= OverlayValueCatalog.Bit(piece.Value);
      foreach (OverlayVariable slot in piece.Slots)
        mask |= Mask(slot, depth + 1);
    }
    return mask;
  }

  private static OverlayValueType TypeOf(OverlayPiece piece, int depth)
  {
    if (depth > MaxDepth)
      return OverlayValueType.Any;
    switch (piece.Kind)
    {
      case OverlayPieceKind.Number:
        return OverlayValueType.Number;
      case OverlayPieceKind.Text:
        return OverlayValueType.Text;
      case OverlayPieceKind.Color:
        return OverlayValueType.Color;
      case OverlayPieceKind.Bool:
        return OverlayValueType.Bool;
      case OverlayPieceKind.Compare:
        return OverlayValueType.Compare;
      case OverlayPieceKind.Point:
        return OverlayValueType.Point;
      case OverlayPieceKind.Value:
        return TypeOf(OverlayValueCatalog.Get(piece.Value));
      case OverlayPieceKind.Condition:
        // What it gives is what its result gives, or its default while that is empty.
        OverlayVariable result = piece.Slots[OverlayPiece.Result];
        return !result.IsEmpty ? TypeOf(result, depth + 1) : TypeOf(piece.Slots[OverlayPiece.Default], depth + 1);
      case OverlayPieceKind.Group:
        return TypeOf(piece.Slots[0], depth + 1);
      default:
        return OverlayValueType.Any;
    }
  }

  private static OverlayValueType TypeOf(OverlayVariable variable, int depth)
  {
    if (variable == null || variable.IsEmpty || depth > MaxDepth)
      return OverlayValueType.Any;
    int end = variable.SegmentEnd(0);
    if (end != variable.Count)
      return OverlayValueType.Any;
    return end > 1 ? OverlayValueType.Number : TypeOf(variable[0], depth);
  }

  private static OverlayTypeProblem Check(
    OverlayVariable variable,
    OverlayValueType needed,
    bool mayBeEmpty,
    OverlayPiece condition,
    int slot,
    int depth
  )
  {
    if (depth > MaxDepth)
      return new OverlayTypeProblem(
        OverlayTypeProblemKind.TooDeep,
        null,
        needed,
        OverlayValueType.Any,
        slot,
        condition
      );
    if (variable == null || variable.IsEmpty)
      return mayBeEmpty
        ? null
        : new OverlayTypeProblem(OverlayTypeProblemKind.Empty, null, needed, OverlayValueType.Any, slot, condition);
    for (int start = 0; start < variable.Count; )
    {
      if (variable[start].Kind == OverlayPieceKind.Operator)
        return new OverlayTypeProblem(
          OverlayTypeProblemKind.Dangling,
          variable[start],
          needed,
          OverlayValueType.Number,
          slot,
          condition
        );
      int end = variable.SegmentEnd(start);
      // Two values side by side only show as they are, which only Any takes.
      if (start > 0 && needed != OverlayValueType.Any)
        return new OverlayTypeProblem(
          OverlayTypeProblemKind.SideBySide,
          variable[start],
          needed,
          OverlayValueType.Any,
          slot,
          condition
        );
      if (end < variable.Count && variable[end].Kind == OverlayPieceKind.Operator)
        return new OverlayTypeProblem(
          OverlayTypeProblemKind.Dangling,
          variable[end],
          needed,
          OverlayValueType.Number,
          slot,
          condition
        );
      bool calculation = end - start > 1;
      // In a calculation every piece is a number; alone, a piece is what the field needs.
      OverlayValueType each = calculation ? OverlayValueType.Number : needed;
      for (int i = start; i < end; i += 2)
      {
        OverlayPiece piece = variable[i];
        OverlayTypeProblem inner = CheckPiece(piece, each, depth);
        if (inner != null)
          return inner.Condition != null
            ? inner
            : new OverlayTypeProblem(inner.Kind, inner.Piece, inner.Needed, inner.Found, slot, condition);
        OverlayValueType found = TypeOf(piece, depth);
        if (calculation && found != OverlayValueType.Number && found != OverlayValueType.Any)
          return new OverlayTypeProblem(
            OverlayTypeProblemKind.NotNumber,
            piece,
            OverlayValueType.Number,
            found,
            slot,
            condition
          );
        if (!calculation && !Fits(needed, found))
          return new OverlayTypeProblem(OverlayTypeProblemKind.WrongType, piece, needed, found, slot, condition);
        // A comparison or a point only goes where one is needed: a text shows neither.
        if ((found == OverlayValueType.Compare || found == OverlayValueType.Point) && needed != found)
          return new OverlayTypeProblem(OverlayTypeProblemKind.WrongType, piece, needed, found, slot, condition);
      }
      start = end;
    }
    return null;
  }

  // What is inside a piece: a condition's slots, a group's variable.
  private static OverlayTypeProblem CheckPiece(OverlayPiece piece, OverlayValueType needed, int depth)
  {
    switch (piece.Kind)
    {
      case OverlayPieceKind.Condition:
        // Words may show nothing otherwise; any other type needs both answers.
        bool any = needed == OverlayValueType.Any;
        return Check(
            piece.Slots[OverlayPiece.Left],
            OverlayValueType.Number,
            false,
            piece,
            OverlayPiece.Left,
            depth + 1
          )
          ?? Check(
            piece.Slots[OverlayPiece.Compare],
            OverlayValueType.Compare,
            false,
            piece,
            OverlayPiece.Compare,
            depth + 1
          )
          ?? Check(
            piece.Slots[OverlayPiece.Right],
            OverlayValueType.Number,
            false,
            piece,
            OverlayPiece.Right,
            depth + 1
          )
          ?? Check(piece.Slots[OverlayPiece.Result], needed, any, piece, OverlayPiece.Result, depth + 1)
          ?? Check(piece.Slots[OverlayPiece.Default], needed, any, piece, OverlayPiece.Default, depth + 1);
      case OverlayPieceKind.Group:
        return Check(piece.Slots[0], needed, false, null, -1, depth + 1);
      default:
        return null;
    }
  }
}
