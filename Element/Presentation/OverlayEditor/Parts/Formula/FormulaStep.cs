namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// One step into a variable from the field's: piece <see cref="Piece"/> of
/// the variable reached so far, then its slot <see cref="Slot"/> (a
/// condition's five, a group's one). A route of steps names any variable
/// laid out on the dock's line, so a click there picks it at once.
/// </summary>
internal readonly struct FormulaStep
{
  public FormulaStep(int piece, int slot)
  {
    Piece = piece;
    Slot = slot;
  }

  public int Piece { get; }

  public int Slot { get; }
}
