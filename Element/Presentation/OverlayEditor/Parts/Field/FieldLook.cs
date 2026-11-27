using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// How fields and the values in them look, wherever they are drawn (the
/// property column, the dock's line, the pills around a shape): values are
/// pills a little lighter than a control, a slot inside a value is a thin
/// outline, and what is picked wears a ring in the highlight color; its fill
/// stays, so a picked value still reads as the same value.
/// </summary>
internal static class FieldLook
{
  public const float Ring = 2,
    Edge = 1.5f;

  /// <summary>A value's pill, on the editor's dimmed view or on a control.</summary>
  public static readonly UiColor Chip = UiPalette.Wash(UiPalette.EditorDim, .15f);

  /// <summary>A value that holds slots (a condition): a control, with its values lighter on it.</summary>
  public static readonly UiColor Holder = UiPalette.EditorControl;

  /// <summary>Inside a slot.</summary>
  public static readonly UiColor Slot = UiPalette.Wash(UiPalette.EditorDim, .1f);

  /// <summary>A slot's outline, and an empty place's.</summary>
  public static readonly UiColor SlotEdge = UiPalette.Wash(UiPalette.EditorDim, .25f);

  /// <summary>A small value in a field row.</summary>
  public static readonly UiColor Mini = new Color(1, 1, 1, .1f);

  /// <summary>Under a type's glyph.</summary>
  public static readonly UiColor MarkTile = new Color(1, 1, 1, .1f);

  public static readonly UiColor Clear = new Color(0, 0, 0, 0);

  /// <summary>The picked one's ring; nothing while it isn't.</summary>
  public static UiColor RingOf(bool picked) => picked ? (UiColor)UiPalette.SoftAccent : Clear;

  /// <summary>An outline that is the highlight color while picked and the slot's edge otherwise.</summary>
  public static UiColor EdgeOf(bool picked) => picked ? (UiColor)UiPalette.SoftAccent : SlotEdge;
}
