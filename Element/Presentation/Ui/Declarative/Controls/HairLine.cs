using System;
using System.Linq.Expressions;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A thin line with hard edges, in the theme's line color or one it is given. ElementUI's Divider
/// softens its edges by half a unit, so a one-unit line in a panel scaled
/// below 1 (the layout editor's toolbars on a small view) fades away; this
/// one stays a crisp line. It takes one unit in the layout and draws a
/// little more, so it covers a pixel down to a scale of 0.8.
/// </summary>
internal sealed class HairLine : NativeView
{
  private const float Bleed = .125f;
  private readonly bool _upright;
  private readonly float _length;
  private readonly Expression<Func<Color>> _color;

  private HairLine(bool upright, float length, Expression<Func<Color>> color = null)
  {
    _upright = upright;
    _length = length;
    _color = color;
  }

  /// <summary>A line across the width it is offered.</summary>
  public static View Across() => new HairLine(false, float.NaN);

  /// <summary>A line across the width it is offered, in a color that follows the view model, such as a drop target's.</summary>
  public static View Across(Expression<Func<Color>> color) => new HairLine(false, float.NaN, color);

  /// <summary>A standing line <paramref name="height"/> tall, between groups in a row.</summary>
  public static View Upright(float height) => new HairLine(true, height);

  protected override void Build(RectTransform rect)
  {
    RectTransform line = RectPeer.Child(rect, "Line");
    if (_upright)
      RectPeer.Stretch(line, -Bleed, 0, -Bleed, 0);
    else
      RectPeer.Stretch(line, 0, -Bleed, 0, -Bleed);
    Image image = line.gameObject.AddComponent<Image>();
    image.color = UguiTheme.Color(ColorToken.Line);
    image.raycastTarget = false;
    if (_color != null)
      Watch(_color, color => image.color = color);
  }

  protected override Size PreferredSize(Proposal proposal) =>
    _upright
      ? new Size(1, _length)
      : new Size(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 0, 1);
}
