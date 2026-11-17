using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>The grow picker's custom arrow path. Direction is a normal RotationEffect on its view.</summary>
internal sealed class OverlayGrowArrowView : NativeView
{
  private const float Size = 24;

  protected override void Build(RectTransform rect)
  {
    OverlayGrowArrow arrow = RectPeer.Child(rect, "Arrow").gameObject.AddComponent<OverlayGrowArrow>();
    arrow.raycastTarget = false;
    arrow.color = UguiTheme.Color(ColorToken.Foreground);
  }

  protected override ElementUI.Size PreferredSize(Proposal proposal) => new(Size, Size);
}
