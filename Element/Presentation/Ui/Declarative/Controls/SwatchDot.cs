using System;
using System.Linq.Expressions;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A color in a light ring, or a gradient in the same circle: what a button
/// that opens a color picker holds, such as the editor bar's Fill. It
/// follows the color and the gradient its expressions pick, and takes no
/// clicks itself.
/// </summary>
internal sealed class SwatchDot : NativeView
{
  public const float Diameter = 18;

  private readonly Expression<Func<Color>> _color;
  private readonly Expression<Func<OverlayResolvedGradient>> _gradient;
  private UiCapsuleGraphic _fill;
  private UiGradientPreview _preview;

  /// <param name="color">The color, opaque, such as <c>() =&gt; VM.Swatch.Value</c>.</param>
  /// <param name="gradient">A gradient shown instead of the color; null shows the color.</param>
  public SwatchDot(Expression<Func<Color>> color, Expression<Func<OverlayResolvedGradient>> gradient)
  {
    _color = color;
    _gradient = gradient;
  }

  protected override void Build(RectTransform rect)
  {
    Capsule(rect, "Ring", 0, new Color(1, 1, 1, .5f));
    _fill = Capsule(rect, "Color", 2, Color.white);
    // A gradient shows inside the same circle.
    _fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    _preview = RectPeer.Child(_fill.rectTransform, "Gradient").gameObject.AddComponent<UiGradientPreview>();
    _preview.raycastTarget = false;
    _preview.Columns = _preview.Rows = 6;
    _preview.Cell = 4;
    _preview.gameObject.SetActive(false);
    Watch(_color, color => _fill.color = color);
    Watch(_gradient, Show);
  }

  private void Show(OverlayResolvedGradient gradient)
  {
    _preview.gameObject.SetActive(gradient != null);
    if (gradient != null)
      _preview.SetGradient(gradient);
  }

  private static UiCapsuleGraphic Capsule(RectTransform parent, string name, float inset, Color color)
  {
    RectTransform rect = RectPeer.Child(parent, name);
    RectPeer.Stretch(rect, inset, inset, inset, inset);
    UiCapsuleGraphic capsule = rect.gameObject.AddComponent<UiCapsuleGraphic>();
    capsule.color = color;
    capsule.raycastTarget = false;
    return capsule;
  }

  protected override Size PreferredSize(Proposal proposal) => new(Diameter, Diameter);
}
