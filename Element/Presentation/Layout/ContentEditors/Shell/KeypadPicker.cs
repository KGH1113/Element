using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>Where the text and shape editors' color picker opens: in the input dock's keypad column.</summary>
internal static class KeypadPicker
{
  /// <summary>
  /// A color picker opens right above the input dock's keypad, in its
  /// column: the keypad below types its opacity and the values beside it
  /// can be dropped on it. On a screen too short for both, it stops under
  /// the top bar.
  /// </summary>
  public static void Place(UiColorPicker picker, OverlayInputDock dock)
  {
    if (picker == null || !picker.Visible)
      return;
    float height = picker.Height;
    float y = Mathf.Max(OverlayEditorFrame.TopBar + 8, dock.Top - 12 - height);
    UiFactory.Place(picker.Root, Left(picker, dock), y, picker.CurrentWidth, height);
    picker.Root.SetAsLastSibling();
  }

  /// <summary>
  /// Where the picker above the keypad starts (frame units). It ends with the
  /// keypad; an editor's two-column picker reaches further left, so the
  /// preview and its bar keep to the space before it while it is open.
  /// </summary>
  public static float Left(UiColorPicker picker, OverlayInputDock dock) => dock.KeypadArea.xMax - picker.CurrentWidth;
}
