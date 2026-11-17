using Element.Presentation.Layout.ContentEditors.Shell.Dock;
using Element.Presentation.Ui.ColorPicker;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The width an editor's preview centers in: the screen from its left margin
/// to the property column, or to an open color picker where that reaches
/// further left. It glides aside as the picker opens and back as it closes.
/// </summary>
internal sealed class OverlayPreviewSpace
{
  private readonly OverlayGlide _glide = new();

  /// <summary>Still gliding; the editor redraws every frame until it arrives.</summary>
  public bool Moving => _glide.Moving;

  /// <summary>Takes the next width at once, as when the editor is laid out afresh.</summary>
  public void Snap() => _glide.Snap();

  /// <summary>
  /// Where the space is this frame, in frame units. <paramref name="reach"/>,
  /// when given, is where an open picker starts instead of above the keypad
  /// (one beside the column).
  /// </summary>
  public void Measure(
    OverlayEditorFrame screen,
    UiColorPicker picker,
    InputDockPanel dock,
    OverlayEditorInspector inspector,
    bool reduceMotion,
    out float left,
    out float right,
    float reach = float.NaN
  )
  {
    left = OverlayEditorFrame.Margin;
    right = Mathf.Min(screen.Width - OverlayEditorFrame.Margin, inspector.Left - 24);
    if (picker != null && picker.Visible)
      right = Mathf.Min(right, (float.IsNaN(reach) ? KeypadPicker.Left(picker, dock) : reach) - 16);
    right = _glide.Toward(right, reduceMotion);
  }
}
