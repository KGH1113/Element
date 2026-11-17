using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// The object a mod got from a part's <c>Rect</c>, spread over the part
/// wherever it is shown; between mounts the page keeps it.
/// </summary>
internal sealed class ModPartRectView : NativeView
{
  private readonly ModPart _part;

  public ModPartRectView(ModPart part) => _part = part;

  protected override void Build(RectTransform rect)
  {
    RectTransform own = _part.OwnRect;
    if (own == null)
      return;
    own.SetParent(rect, false);
    RectPeer.Stretch(own, 0, 0, 0, 0);
  }

  // As large as the part it covers; it adds nothing to its size.
  protected override Size PreferredSize(Proposal proposal) => new(Finite(proposal.Width), Finite(proposal.Height));

  private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;

  protected override void Teardown() => _part.Park();
}
