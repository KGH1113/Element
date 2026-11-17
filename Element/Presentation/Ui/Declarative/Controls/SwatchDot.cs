using System;
using System.Linq.Expressions;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.ColorPicker;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>A color or domain gradient in a circular ring; its enclosing control owns input.</summary>
internal sealed class SwatchDot : ComponentView
{
  public const float Diameter = 18;
  private readonly Computed<Color> _color;
  private readonly Expression<Func<OverlayResolvedGradient>> _gradient;

  public SwatchDot(Expression<Func<Color>> color, Expression<Func<OverlayResolvedGradient>> gradient)
  {
    _color = Computed(color);
    _gradient = gradient;
  }

  public override View Body =>
    Circle()
      .Fill(() => (UiColor)_color.Value)
      .Overlay(new GradientContent(_gradient))
      .ClipShape(Shapes.Circle)
      .Frame(width: Diameter - 4, height: Diameter - 4)
      .Background(Circle().Fill(new Color(1, 1, 1, .5f)).Frame(width: Diameter, height: Diameter))
      .Frame(width: Diameter, height: Diameter)
      .AllowsHitTesting(false);

  // The resolved overlay gradient and its checkerboard remain domain-specific drawing.
  private sealed class GradientContent : NativeView
  {
    private readonly Expression<Func<OverlayResolvedGradient>> _gradient;

    public GradientContent(Expression<Func<OverlayResolvedGradient>> gradient) => _gradient = gradient;

    protected override void Build(RectTransform rect)
    {
      UiGradientPreview preview = RectPeer.Child(rect, "Gradient").gameObject.AddComponent<UiGradientPreview>();
      preview.raycastTarget = false;
      preview.Columns = preview.Rows = 6;
      preview.Cell = 4;
      Watch(
        _gradient,
        gradient =>
        {
          preview.gameObject.SetActive(gradient != null);
          if (gradient != null)
            preview.SetGradient(gradient);
        }
      );
    }

    protected override Size PreferredSize(Proposal proposal) => new(Diameter - 4, Diameter - 4);
  }
}
