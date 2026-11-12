using System;
using System.Linq.Expressions;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>A grow picker point's arrow (<see cref="OverlayGrowArrow"/>), pointing where its expression says.</summary>
internal sealed class OverlayGrowArrowView : NativeView
{
  private const float Size = 24;
  private readonly Expression<Func<Vector2>> _direction;

  /// <param name="direction">Right and down positive, as on screen.</param>
  public OverlayGrowArrowView(Expression<Func<Vector2>> direction) => _direction = direction;

  protected override void Build(RectTransform rect)
  {
    OverlayGrowArrow arrow = RectPeer.Child(rect, "Arrow").gameObject.AddComponent<OverlayGrowArrow>();
    arrow.raycastTarget = false;
    arrow.color = UguiTheme.Color(ColorToken.Foreground);
    Watch(_direction, arrow.Point);
  }

  protected override ElementUI.Size PreferredSize(Proposal proposal) => new(Size, Size);
}
