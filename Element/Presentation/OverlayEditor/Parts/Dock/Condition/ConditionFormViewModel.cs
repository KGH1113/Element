using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Theme;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

/// <summary>
/// A condition's input method, drawn as a fork: what is compared on top
/// (value, comparison, value), and under it the two answers side by side.
/// With the values as they are now, the answer the condition gives is lit,
/// and the other waits. A slot clicked is gone into, where the dock's line
/// and input method are that slot's. When Apply is refused, the slot in the
/// way is outlined and the whole shakes.
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
        slot == OverlayPiece.Result ? words.Then
        : slot == OverlayPiece.Default ? words.Otherwise
        : "";
      slots[slot] = Own(new ConditionSlotViewModel(slot, word, FormulaRefusals.SlotName(slot), enter));
    }
    Slots = slots;
  }

  /// <summary>Compared, comparison, compared with, result, default.</summary>
  public readonly IReadOnlyList<ConditionSlotViewModel> Slots;

  /// <summary>Shakes the fork when Apply is refused.</summary>
  public readonly EditorShake Shake = new();

  /// <summary>
  /// Shows <paramref name="condition"/>, whose answers must give
  /// <paramref name="gives"/>. <paramref name="values"/> say which way it
  /// goes now. <paramref name="problem"/> is what keeps the field from being
  /// finished, shown on its slot while <paramref name="alarmed"/>.
  /// </summary>
  public void Show(
    OverlayPiece condition,
    OverlayValueType gives,
    IOverlayNumberSource values,
    OverlayTypeProblem problem,
    bool alarmed
  )
  {
    if (condition == null || condition.Kind != OverlayPieceKind.Condition)
      return;
    bool known = OverlayVariableEvaluator.TryHolds(condition, values, out bool holds);
    for (int slot = 0; slot < Slots.Count; slot++)
    {
      ConditionSlotViewModel row = Slots[slot];
      OverlayVariable held = condition.Slots[slot];
      bool answer = slot >= OverlayPiece.Result;
      OverlayValueType type =
        slot == OverlayPiece.Compare ? OverlayValueType.Compare
        : answer ? gives
        : OverlayValueType.Number;
      row.Type.Value = type;
      row.Sign.Value =
        slot == OverlayPiece.Compare && held.Count == 1 && held[0].Kind == OverlayPieceKind.Compare ? held[0].Op : '\0';
      if (answer)
        ShowAnswer(row, held, type);
      else
        ShowPills(row, held, type, 1);
      row.Live.Value = answer && known && holds == (slot == OverlayPiece.Result);
      row.Quiet.Value = answer && known && !row.Live.Value;
      row.Bad.Value = alarmed && problem != null && problem.Condition == condition && problem.Slot == slot;
    }
  }

  // An answer that is one thing typed or picked by hand shows as that thing, large; anything else as pills.
  private static void ShowAnswer(ConditionSlotViewModel row, OverlayVariable held, OverlayValueType type)
  {
    OverlayPiece only = held.Count == 1 ? held[0] : null;
    OverlayVariableWords words = FormulaWords.Current;
    switch (only?.Kind)
    {
      case OverlayPieceKind.Color:
        ShowColor(row, OverlayTextComposer.ParseHex(only.Text), only.Text);
        return;
      case OverlayPieceKind.Value when OverlayValueCatalog.Get(only.Value).IsColor:
        ShowColor(row, UiPalette.Accent, LineViewModel.Label(only));
        return;
      case OverlayPieceKind.Number:
        ShowBig(row, only.Text.Replace('-', '−'), 30);
        return;
      case OverlayPieceKind.Compare:
        ShowBig(row, OverlayVariable.Symbol(only.Op).ToString(), 30);
        return;
      case OverlayPieceKind.Bool:
        ShowBig(row, only.Bool ? words.Yes : words.No, 24);
        return;
      case OverlayPieceKind.Point:
        ShowBig(row, words.Point(only.Point), 19);
        return;
      default:
        ShowPills(row, held, type, 2);
        return;
    }
  }

  private static void ShowColor(ConditionSlotViewModel row, UnityEngine.Color color, string caption)
  {
    color.a = 1;
    row.Swatch.Value = color;
    row.Caption.Value = caption;
    row.Look.Value = ConditionSlotLook.Color;
  }

  private static void ShowBig(ConditionSlotViewModel row, string text, float size)
  {
    row.Big.Value = text;
    row.BigSize.Value = size;
    row.Look.Value = ConditionSlotLook.Big;
  }

  private static void ShowPills(ConditionSlotViewModel row, OverlayVariable held, OverlayValueType type, int most)
  {
    row.Chips.Value = held.IsEmpty ? Nothing : FieldChips.Of(held, type, most);
    row.Look.Value = held.IsEmpty ? ConditionSlotLook.Empty : ConditionSlotLook.Chips;
  }

  protected override void OnDisposed() => Shake.Dispose();
}
