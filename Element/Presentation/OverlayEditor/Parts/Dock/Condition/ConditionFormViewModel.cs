using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

/// <summary>
/// A condition's input method: its five slots one under another, read as a
/// sentence (if … then … otherwise …). Each shows what it holds; a click goes
/// into it, where the dock's line and input method are that slot's. When
/// Apply is refused, the slot in the way is outlined and the form shakes.
/// </summary>
internal sealed class ConditionFormViewModel : ViewModel
{
  private static readonly FieldChip[] Nothing = Array.Empty<FieldChip>();

  /// <param name="enter">Goes into one of the picked condition's slots.</param>
  public ConditionFormViewModel(Action<int> enter)
  {
    OverlayVariableWords words = FormulaWords.Current;
    ConditionSlotViewModel[] slots = new ConditionSlotViewModel[5];
    for (int slot = 0; slot < slots.Length; slot++)
    {
      string word =
        slot == OverlayPiece.Left ? words.ConditionStart
        : slot == OverlayPiece.Result ? words.Then
        : slot == OverlayPiece.Default ? words.Otherwise
        : "";
      slots[slot] = Own(new ConditionSlotViewModel(slot, word, FormulaRefusals.SlotName(slot), enter));
    }
    Slots = slots;
  }

  /// <summary>Compared, comparison, compared with, result, default.</summary>
  public readonly IReadOnlyList<ConditionSlotViewModel> Slots;

  /// <summary>Shakes the form when Apply is refused.</summary>
  public readonly EditorShake Shake = new();

  /// <summary>
  /// Shows <paramref name="condition"/>, whose result and default must give
  /// <paramref name="gives"/>. <paramref name="problem"/> is what keeps the
  /// field from being finished, shown on its slot while <paramref name="alarmed"/>.
  /// </summary>
  public void Show(OverlayPiece condition, OverlayValueType gives, OverlayTypeProblem problem, bool alarmed)
  {
    if (condition == null || condition.Kind != OverlayPieceKind.Condition)
      return;
    for (int slot = 0; slot < Slots.Count; slot++)
    {
      ConditionSlotViewModel row = Slots[slot];
      OverlayVariable held = condition.Slots[slot];
      OverlayValueType type =
        slot == OverlayPiece.Compare ? OverlayValueType.Compare
        : slot == OverlayPiece.Left || slot == OverlayPiece.Right ? OverlayValueType.Number
        : gives;
      row.Type.Value = type;
      row.Empty.Value = held.IsEmpty;
      row.Chips.Value = held.IsEmpty ? Nothing : FieldChips.Of(held, type);
      row.Bad.Value = alarmed && problem != null && problem.Condition == condition && problem.Slot == slot;
    }
  }

  protected override void OnDisposed() => Shake.Dispose();
}
