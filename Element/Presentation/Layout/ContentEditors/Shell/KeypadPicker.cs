using Element.Presentation.Layout.ContentEditors.Shell.Dock;
using Element.Presentation.Ui.ColorPicker;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>Where the text and shape editors' color picker opens: in the input dock's keypad column.</summary>
internal static class KeypadPicker
{
  /// <summary>
  /// A color picker opens in the editor's property column, from
  /// <paramref name="top"/> (under the column's back row), above the input
  /// dock's keypad: the keypad below types its opacity and the values beside
  /// it can be dropped on it. On a screen too short for both, it rises until
  /// it stops under the header.
  /// </summary>
  public static void Place(UiColorPicker picker, InputDockPanel dock, float top)
  {
    if (picker == null || !picker.Visible)
      return;
    float y = Mathf.Max(OverlayEditorFrame.TopBar + 8, Mathf.Min(top, dock.Top - 8 - picker.Height));
    // The panel keeps the size of what it shows; only its place follows the dock.
    picker.Root.anchoredPosition = new Vector2(Left(picker, dock), -y);
    picker.Root.SetAsLastSibling();
  }

  /// <summary>
  /// A picker for a color shown in the property column's open tool (a stop
  /// of a color that follows a value) opens beside the column instead, its
  /// right edge <see cref="OverlayEditorInspector.Gap"/> left of
  /// <paramref name="column"/> and its top at <paramref name="top"/>, rising
  /// as far as it must to stay clear of the dock.
  /// </summary>
  public static void PlaceBeside(UiColorPicker picker, InputDockPanel dock, float column, float top)
  {
    if (picker == null || !picker.Visible)
      return;
    float y = Mathf.Max(
      OverlayEditorFrame.TopBar + 8,
      Mathf.Min(top, dock.Top - OverlayEditorInspector.Gap - picker.Height)
    );
    picker.Root.anchoredPosition = new Vector2(Mathf.Round(BesideLeft(picker, column)), -Mathf.Round(y));
    picker.Root.SetAsLastSibling();
  }

  /// <summary>Where a picker beside the column starts (frame units).</summary>
  public static float BesideLeft(UiColorPicker picker, float column) =>
    column - OverlayEditorInspector.Gap - picker.CurrentWidth;

  /// <summary>
  /// Where the picker above the keypad starts (frame units). It ends with the
  /// keypad; an editor's two-column picker reaches further left, so the
  /// preview and its bar keep to the space before it while it is open.
  /// </summary>
  public static float Left(UiColorPicker picker, InputDockPanel dock) => dock.KeypadArea.xMax - picker.CurrentWidth;
}
