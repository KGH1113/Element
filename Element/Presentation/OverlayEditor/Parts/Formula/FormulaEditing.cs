using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// What the keypad edits: one slot and the formula being typed there, which
/// may still end in an operator. Every change writes the finished part to the
/// slot at once, so the preview follows each key. The keypad and the dock's
/// line read it and follow each change by themselves.
/// </summary>
internal sealed class FormulaEditing : Observable
{
  private FormulaSlot _slot;
  private OverlayFormula _working;

  public FormulaSlot Slot
  {
    get
    {
      Reading();
      return _slot;
    }
  }

  /// <summary>The formula being typed; changed in place, so read it again after a change.</summary>
  public OverlayFormula Working
  {
    get
    {
      Reading();
      return _working;
    }
  }

  public bool Active
  {
    get
    {
      Reading();
      return _slot != null;
    }
  }

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
    bool same = _slot != null && _slot.Key == slot.Key && _working != null;
    _slot = slot;
    // A redraw points at the same place with a slot made afresh: nothing its readers show changed.
    if (same)
      return;
    _working = slot.Read();
    Changed();
  }

  public void Deactivate()
  {
    if (_slot == null && _working == null)
      return;
    _slot = null;
    _working = null;
    Changed();
  }

  public bool CanPress(char key) => Active && _working.CanType(key);

  public bool Press(char key)
  {
    if (!CanPress(key))
      return false;
    _working.Type(key);
    Commit();
    return true;
  }

  public bool CanClear => Active && !_working.IsEmpty;

  /// <summary>
  /// Starts the formula over. Nothing is written until something new is
  /// typed, so clearing and leaving keeps what was there.
  /// </summary>
  public void Clear()
  {
    if (!Active)
      return;
    _working.Clear();
    Changed();
  }

  public bool CanBackspace => Active && _working.CanBackspace;

  public bool Backspace()
  {
    if (!CanBackspace)
      return false;
    _working.Backspace();
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
    _working.Drop(value.Id);
    Commit();
    return FormulaDropResult.Applied;
  }

  // The formula changed in place: its readers follow, and the slot takes the finished part.
  private void Commit()
  {
    Changed();
    OverlayFormula finished = _working.Trimmed();
    // A number field keeps its last number until something new is typed.
    if (finished.IsEmpty && !_slot.IsText)
      return;
    _slot.Write(finished);
  }
}
