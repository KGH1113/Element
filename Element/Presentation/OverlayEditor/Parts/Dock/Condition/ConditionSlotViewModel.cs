using System;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Field;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

/// <summary>
/// One of a condition's five slots as its input method shows it: what it
/// holds (pills, one large number or word, or one color), or while empty its
/// type's mark and name. An answer (the result or the default) also knows
/// whether the condition goes its way now. A click goes into it.
/// </summary>
internal sealed class ConditionSlotViewModel : ViewModel
{
  /// <param name="word">The word an answer goes by: "then", "otherwise"; empty for what is compared.</param>
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

  public readonly State<ConditionSlotLook> Look = new(ConditionSlotLook.Empty);

  /// <summary>What it holds as pills.</summary>
  public readonly State<FieldChip[]> Chips = new(Array.Empty<FieldChip>());

  /// <summary>One number or word written large, and how large.</summary>
  public readonly State<string> Big = new("");

  public readonly State<float> BigSize = new(30);

  /// <summary>One color, and what it is called: its HEX, or "Highlight color".</summary>
  public readonly State<UiColor> Swatch = new(Color.white);

  public readonly State<string> Caption = new("");

  /// <summary>A comparison picked by hand, as its sign; '\0' while the slot holds something else.</summary>
  public readonly State<char> Sign = new('\0');

  /// <summary>The condition goes this way now, with the values as they are.</summary>
  public readonly State<bool> Live = new(false);

  /// <summary>The condition goes the other way now: this answer waits, quieter.</summary>
  public readonly State<bool> Quiet = new(false);

  /// <summary>It keeps the field from being finished, and Apply was just refused: its outline says so.</summary>
  public readonly State<bool> Bad = new(false);

  public Command Enter { get; }
}
