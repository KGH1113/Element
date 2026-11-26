using System;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Field;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

/// <summary>
/// One of a condition's five slots as its input method shows it: the word it
/// reads after ("if", "then", "otherwise"), what it holds as chips, or while
/// empty its type's mark and its name. A click goes into it.
/// </summary>
internal sealed class ConditionSlotViewModel : ViewModel
{
  /// <param name="word">The word before it in the sentence; empty for a slot that goes on from the one above.</param>
  /// <param name="name">What goes in it, said while it is empty: "Result".</param>
  /// <param name="enter">Goes into this slot.</param>
  public ConditionSlotViewModel(int slot, string word, string name, Action<int> enter)
  {
    Slot = slot;
    Word = word ?? "";
    Name = name ?? "";
    Enter = Command(() => enter(slot));
  }

  public readonly int Slot;

  public readonly string Word;

  public readonly string Name;

  /// <summary>What the slot must give, for its mark while it is empty.</summary>
  public readonly State<OverlayValueType> Type = new(OverlayValueType.Number);

  /// <summary>What it holds; none while it is empty.</summary>
  public readonly State<FieldChip[]> Chips = new(Array.Empty<FieldChip>());

  public readonly State<bool> Empty = new(true);

  /// <summary>It keeps the field from being finished, and Apply was just refused: its outline says so.</summary>
  public readonly State<bool> Bad = new(false);

  public Command Enter { get; }
}
