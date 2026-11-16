using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.Canvas;
using ElementUI;

namespace Element.Presentation.Layout.Kinds;

/// <summary>
/// The element toolbar of one key viewer: hand or foot, which replaces the
/// viewer in place, and Edit, which opens it in ElementNote.
/// </summary>
internal sealed class KeyViewerBarModel : LayoutBarModel
{
  private readonly KeyViewerKind _kind;
  private IOverlayLayoutTarget _target;

  public KeyViewerBarModel(KeyViewerKind kind, IOverlayLayoutTarget target)
  {
    _kind = kind;
    _target = target;
    Viewer = IndexOf(target);
    // Hand or foot replaces the target without a new bar: Edit reads it when pressed.
    Edit = Command(() => _kind.OpenInApp(_target));
  }

  /// <summary>The viewer picked: 0 for a hand viewer, 1 for a foot viewer.</summary>
  public int Viewer;

  /// <summary>Opens this viewer's tab in ElementNote.</summary>
  public Command Edit { get; }

  public override IOverlayLayoutTarget Target => _target;

  // The picker wrote another viewer: replace the shown one, or go back when ElementNote can't.
  public override void Edited()
  {
    int shown = IndexOf(_target);
    if (Viewer == shown)
      return;
    string templateId = Viewer == 1 ? OverlayElementKinds.FootKeyViewer : OverlayElementKinds.HandKeyViewer;
    if (_kind.Switch(_target, templateId, created => _target = created))
      return;
    Viewer = shown;
    Runtime.Probe();
  }

  private static int IndexOf(IOverlayLayoutTarget target) =>
    target.TemplateId == OverlayElementKinds.FootKeyViewer ? 1 : 0;
}
