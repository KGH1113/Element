using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The width an editor's preview and its bar center in: the screen between
/// its margins, or the part left of an open color picker. It glides aside as
/// the picker opens and back as it closes.
/// </summary>
internal sealed class OverlayPreviewSpace
{
  private readonly OverlayGlide _glide = new();

  /// <summary>Still gliding; the editor redraws every frame until it arrives.</summary>
  public bool Moving => _glide.Moving;

  /// <summary>Takes the next width at once, as when the editor is laid out afresh.</summary>
  public void Snap() => _glide.Snap();

  /// <summary>Where the space is this frame, in frame units.</summary>
  public void Measure(
    OverlayEditorFrame screen,
    SettingsColorPicker picker,
    OverlayInputDock dock,
    bool reduceMotion,
    out float left,
    out float right
  )
  {
    left = OverlayEditorFrame.Margin;
    right = screen.Width - OverlayEditorFrame.Margin;
    if (picker != null && picker.Visible)
      right = Mathf.Min(right, KeypadPicker.Left(picker, dock) - 16);
    right = _glide.Toward(right, reduceMotion);
  }
}
