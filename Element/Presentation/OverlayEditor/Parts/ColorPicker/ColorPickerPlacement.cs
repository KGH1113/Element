using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.Ui.ColorPicker;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.ColorPicker;

/// <summary>Where the text and shape editors' color picker opens: in the input dock's keypad column.</summary>
internal static class ColorPickerPlacement
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
    float y = Mathf.Max(EditorFramePanel.TopBar + 8, Mathf.Min(top, dock.Top - 8 - picker.Height));
    // The panel keeps the size of what it shows; only its place follows the dock.
    picker.Root.anchoredPosition = new Vector2(Left(picker, dock), -y);
    picker.Root.SetAsLastSibling();
    // A gradient's picked color stands where every input method does.
    if (picker.PlaneRoot != null && picker.PlaneRoot.gameObject.activeSelf)
    {
      Rect place = dock.MethodArea;
      picker.PlaneRoot.anchoredPosition = new Vector2(place.x, -place.y);
      picker.PlaneRoot.SetAsLastSibling();
    }
  }

  /// <summary>A solid color's picker over the keypad's keys, where every input method is.</summary>
  public static void PlaceOnKeypad(UiColorPicker picker, InputDockPanel dock)
  {
    if (picker == null || !picker.Visible)
      return;
    Rect keys = dock.MethodArea;
    picker.Root.anchoredPosition = new Vector2(keys.x, -keys.y);
    picker.Root.SetAsLastSibling();
  }

  /// <summary>Where the picker above the keypad starts (frame units): it is as wide as the column and ends with it.</summary>
  public static float Left(UiColorPicker picker, InputDockPanel dock) => dock.KeypadArea.xMax - picker.CurrentWidth;
}
