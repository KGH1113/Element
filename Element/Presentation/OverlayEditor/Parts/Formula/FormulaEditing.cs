using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// What the keypad edits: one slot and the formula being typed there, which
/// may still end in an operator. Every change writes the finished part to the
/// slot at once, so the preview follows each key.
/// </summary>
internal sealed class FormulaEditing
{
  public FormulaSlot Slot { get; private set; }
  public OverlayFormula Working { get; private set; }
  public bool Active => Slot != null;

  public void Activate(FormulaSlot slot)
  {
    if (slot == null)
    {
      Deactivate();
      return;
    }
    // The same place keeps what is being typed, such as a trailing "×" or a
    // "2" on its way to "25" that is applied as the smallest size meanwhile.
    // Undo deactivates first, so restored formulas are read afresh.
    bool same = Slot != null && Slot.Key == slot.Key && Working != null;
    Slot = slot;
    if (!same)
      Working = slot.Read();
  }

  public void Deactivate()
  {
    Slot = null;
    Working = null;
  }

  public bool CanPress(char key) => Active && Working.CanType(key);

  public bool Press(char key)
  {
    if (!CanPress(key))
      return false;
    Working.Type(key);
    Commit();
    return true;
  }

  public bool CanClear => Active && !Working.IsEmpty;

  /// <summary>
  /// Starts the formula over. Nothing is written until something new is
  /// typed, so clearing and leaving keeps what was there.
  /// </summary>
  public void Clear()
  {
    if (Active)
      Working.Clear();
  }

  public bool CanBackspace => Active && Working.CanBackspace;

  public bool Backspace()
  {
    if (!CanBackspace)
      return false;
    Working.Backspace();
    Commit();
    return true;
  }

  /// <summary>Whether <paramref name="value"/> may be dropped on <paramref name="slot"/>.</summary>
  public static bool Accepts(FormulaSlot slot, OverlayValueDefinition value) =>
    slot != null && (value.IsNumeric || slot.IsText);

  /// <summary>
  /// The value joins after a trailing operator, otherwise it replaces the
  /// slot's formula. The slot becomes the keypad's.
  /// </summary>
  public FormulaDropResult Drop(FormulaSlot slot, OverlayValueDefinition value)
  {
    if (!Accepts(slot, value))
      return FormulaDropResult.RefusedWords;
    Activate(slot);
    Working.Drop(value.Id);
    Commit();
    return FormulaDropResult.Applied;
  }

  private void Commit()
  {
    OverlayFormula finished = Working.Trimmed();
    // A number field keeps its last number until something new is typed.
    if (finished.IsEmpty && !Slot.IsText)
      return;
    Slot.Write(finished);
  }
}
