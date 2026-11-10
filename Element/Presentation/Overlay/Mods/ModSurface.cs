using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using ElementKit.Elements;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// What a mod element draws into. Boxes are Element's own shapes, so colors
/// keep their gradients and the highlight color; texts use the overlay's
/// default font. Drawing is kept between draws: the n-th box of one draw is
/// the n-th of the next, and what a draw doesn't use is hidden, not destroyed.
/// </summary>
internal sealed class ModSurface : Surface, IDisposable
{
  private sealed class BoxPart
  {
    public RectTransform Rect;
    public ShapeGraphic Graphic;
    public readonly OverlayShapeSettings Shape = OverlayShapeGeometry.Create();
    public readonly OverlayResolvedGradient Gradient = new();
  }

  private sealed class TextPart
  {
    public RectTransform Rect;
    public TextMeshProUGUI Text;
  }

  private readonly RectTransform _drawn;
  private readonly RectTransform _own;
  private readonly List<BoxPart> _boxes = new();
  private readonly List<TextPart> _texts = new();
  private OverlayFonts _fonts;
  private int _boxCount;
  private int _textCount;
  private float _pixelsPerUnit = 1;
  private Vector2 _size;

  /// <param name="parent">Fills the element; the surface sits inside it.</param>
  public ModSurface(RectTransform parent, OverlayFonts fonts)
  {
    _fonts = fonts;
    _drawn = Layer("Element drawing", parent);
    _own = Layer("Mod drawing", parent);
  }

  public override Vector2 Size => _size;

  public override RectTransform Root => _own;

  public void SetFonts(OverlayFonts fonts) => _fonts = fonts;

  /// <summary>Starts a draw of an element <paramref name="size"/> big, shown at <paramref name="pixelsPerUnit"/>.</summary>
  public void Begin(Vector2 size, float pixelsPerUnit)
  {
    _size = size;
    _pixelsPerUnit = pixelsPerUnit > 0 ? pixelsPerUnit : 1;
    _boxCount = 0;
    _textCount = 0;
  }

  /// <summary>Hides what this draw didn't use.</summary>
  public void End()
  {
    for (int i = _boxCount; i < _boxes.Count; i++)
      Show(_boxes[i].Rect, false);
    for (int i = _textCount; i < _texts.Count; i++)
      Show(_texts[i].Rect, false);
  }

  /// <summary>Hides everything, the mod's own drawing included.</summary>
  public void Clear()
  {
    _boxCount = 0;
    _textCount = 0;
    End();
    Show(_own, false);
  }

  /// <summary>Shows the mod's own drawing again after <see cref="Clear"/>.</summary>
  public void Restore() => Show(_own, true);

  public override void Box(Rect rect, ElementColor fill, float rounding = 0)
  {
    if (!Finite(rect) || rect.width <= 0 || rect.height <= 0)
      return;
    BoxPart part = NextBox();
    Place(part.Rect, rect);
    OverlayShapeSettings shape = part.Shape;
    shape.Width = rect.width;
    shape.Height = rect.height;
    float radius = Mathf.Clamp(float.IsNaN(rounding) ? 0 : rounding, 0, Mathf.Min(rect.width, rect.height) * .5f);
    foreach (OverlayShapePoint point in shape.Points)
      point.Radius = radius;
    shape.StrokeWidth = 0;
    Unwrap(fill, out ModPaint paint, out float factor);
    Color solid = fill.Solid;
    OverlayResolvedGradient gradient = null;
    if (paint != null && paint.TryGradient(rect.width, rect.height, part.Gradient))
    {
      gradient = part.Gradient;
      if (factor < 1)
        Fade(gradient, factor);
    }
    part.Graphic.SetShape(shape, solid, Color.clear, gradient, null);
    part.Graphic.SetPixelsPerUnit(_pixelsPerUnit);
  }

  public override void Text(Rect rect, string text, float size, ElementColor color, TextAlign align = TextAlign.Center)
  {
    if (_fonts == null || !Finite(rect) || string.IsNullOrEmpty(text))
      return;
    TextPart part = NextText();
    Place(part.Rect, rect);
    TextMeshProUGUI label = part.Text;
    if (label.font != _fonts.DefaultAsset)
    {
      label.font = _fonts.DefaultAsset;
      label.fontSharedMaterial = _fonts.DefaultMaterial;
    }
    float fontSize = Mathf.Clamp(float.IsNaN(size) ? 24 : size, 4, 400);
    if (!Mathf.Approximately(label.fontSize, fontSize))
      label.fontSize = fontSize;
    TextAlignmentOptions alignment = align switch
    {
      TextAlign.Left => TextAlignmentOptions.MidlineLeft,
      TextAlign.Right => TextAlignmentOptions.MidlineRight,
      _ => TextAlignmentOptions.Midline,
    };
    if (label.alignment != alignment)
      label.alignment = alignment;
    if (label.color != color.Solid)
      label.color = color.Solid;
    // Shown as written: markup in a mod's words is not read.
    if (label.text != text)
      label.text = text;
  }

  private BoxPart NextBox()
  {
    if (_boxCount == _boxes.Count)
    {
      RectTransform rect = Child("Box", _drawn);
      ShapeGraphic graphic = rect.gameObject.AddComponent<ShapeGraphic>();
      graphic.raycastTarget = false;
      _boxes.Add(new BoxPart { Rect = rect, Graphic = graphic });
    }
    BoxPart part = _boxes[_boxCount++];
    Show(part.Rect, true);
    // Later draws sit above earlier ones, boxes and texts alike.
    part.Rect.SetAsLastSibling();
    return part;
  }

  private TextPart NextText()
  {
    if (_textCount == _texts.Count)
    {
      RectTransform rect = Child("Text", _drawn);
      TextMeshProUGUI text = OverlayTextFactory.AddText(
        rect.gameObject,
        _fonts.DefaultAsset,
        _fonts.DefaultMaterial,
        24,
        TextAlignmentOptions.Midline
      );
      text.richText = false;
      text.textWrappingMode = TextWrappingModes.NoWrap;
      text.overflowMode = TextOverflowModes.Overflow;
      _texts.Add(new TextPart { Rect = rect, Text = text });
    }
    TextPart part = _texts[_textCount++];
    Show(part.Rect, true);
    part.Rect.SetAsLastSibling();
    return part;
  }

  private static void Unwrap(ElementColor color, out ModPaint paint, out float factor)
  {
    paint = color.Paint as ModPaint;
    factor = color.Opacity;
  }

  private static void Fade(OverlayResolvedGradient gradient, float factor)
  {
    for (int i = 0; i < gradient.Stops.Count; i++)
    {
      OverlayGradientSample stop = gradient.Stops[i];
      OverlayRgba color = stop.Color;
      gradient.Stops[i] = new OverlayGradientSample(
        stop.Position,
        new OverlayRgba(color.R, color.G, color.B, color.A * factor)
      );
    }
  }

  // Top-left origin, y down, in the element's units.
  private static void Place(RectTransform rect, Rect area)
  {
    Vector2 position = new(area.x, -area.y);
    if (rect.anchoredPosition != position)
      rect.anchoredPosition = position;
    if (rect.sizeDelta != area.size)
      rect.sizeDelta = area.size;
  }

  private static RectTransform Layer(string name, RectTransform parent)
  {
    RectTransform rect = Child(name, parent);
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = rect.offsetMax = Vector2.zero;
    return rect;
  }

  private static RectTransform Child(string name, Transform parent)
  {
    GameObject child = new(name, typeof(RectTransform));
    RectTransform rect = child.GetComponent<RectTransform>();
    rect.SetParent(parent, false);
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
    return rect;
  }

  private static void Show(RectTransform rect, bool shown)
  {
    if (rect.gameObject.activeSelf != shown)
      rect.gameObject.SetActive(shown);
  }

  private static bool Finite(Rect rect) =>
    !float.IsNaN(rect.x)
    && !float.IsNaN(rect.y)
    && !float.IsNaN(rect.width)
    && !float.IsNaN(rect.height)
    && !float.IsInfinity(rect.width)
    && !float.IsInfinity(rect.height);

  public void Dispose()
  {
    _boxes.Clear();
    _texts.Clear();
  }
}
