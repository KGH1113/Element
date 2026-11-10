using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using ElementKit;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// Element's drawing helpers for one mod element: texts in the overlay's
/// default font, which follow it when players pick another, and boxes drawn
/// as Element's own shapes with edges kept one pixel soft at any scale.
/// </summary>
internal sealed class ModDrawing : Drawing
{
  private readonly List<TextMeshProUGUI> _texts = new();
  private readonly List<ModBox> _boxes = new();
  private OverlayFonts _fonts;
  private int _fontVersion = -1;
  private float _pixelsPerUnit = 1;

  public ModDrawing(OverlayFonts fonts) => _fonts = fonts;

  public override Color Highlight => OverlayTheme.Accent;

  public void SetFonts(OverlayFonts fonts)
  {
    _fonts = fonts;
    _fontVersion = -1;
  }

  public override TextMeshProUGUI Text(Transform parent, string text = "", float size = 24)
  {
    GameObject child = new("Text", typeof(RectTransform));
    RectTransform rect = child.GetComponent<RectTransform>();
    rect.SetParent(parent, false);
    Place(rect, 0, 0, 200, Mathf.Max(1, size * 1.4f));
    TextMeshProUGUI label = OverlayTextFactory.AddText(
      child,
      _fonts?.DefaultAsset,
      _fonts?.DefaultMaterial,
      size,
      TextAlignmentOptions.TopLeft
    );
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.overflowMode = TextOverflowModes.Overflow;
    label.text = text ?? string.Empty;
    _texts.Add(label);
    return label;
  }

  public override RoundedBox Box(Transform parent)
  {
    ModBox box = new(parent, _pixelsPerUnit);
    _boxes.Add(box);
    return box;
  }

  /// <summary>
  /// Once a frame: texts take a newly picked font and boxes the screen's
  /// pixels; a few comparisons when nothing changed. Destroyed ones are let go.
  /// </summary>
  public void Tick(float pixelsPerUnit)
  {
    int fonts = _fonts?.Version ?? 0;
    bool fontChanged = fonts != _fontVersion;
    _fontVersion = fonts;
    for (int i = _texts.Count - 1; i >= 0; i--)
    {
      TextMeshProUGUI text = _texts[i];
      if (text == null)
      {
        _texts.RemoveAt(i);
        continue;
      }
      if (fontChanged && _fonts != null && text.font != _fonts.DefaultAsset)
      {
        text.font = _fonts.DefaultAsset;
        text.fontSharedMaterial = _fonts.DefaultMaterial;
      }
    }
    bool pixelsChanged = pixelsPerUnit > 0 && Mathf.Abs(pixelsPerUnit - _pixelsPerUnit) > .01f;
    if (pixelsChanged)
      _pixelsPerUnit = pixelsPerUnit;
    for (int i = _boxes.Count - 1; i >= 0; i--)
    {
      if (!_boxes[i].Alive)
      {
        _boxes.RemoveAt(i);
        continue;
      }
      if (pixelsChanged)
        _boxes[i].SetPixelsPerUnit(_pixelsPerUnit);
    }
  }

  /// <summary>Forgets what it made; the objects go with their element.</summary>
  public void Clear()
  {
    _texts.Clear();
    _boxes.Clear();
  }
}

/// <summary>A rounded box drawn as an Element square that follows its UI object's size at once.</summary>
internal sealed class ModBox : RoundedBox
{
  private readonly ShapeGraphic _graphic;
  private readonly OverlayShapeSettings _shape = OverlayShapeGeometry.Create();
  private Color _color = Color.white;

  public ModBox(Transform parent, float pixelsPerUnit)
  {
    GameObject child = new("Box", typeof(RectTransform));
    Rect = child.GetComponent<RectTransform>();
    Rect.SetParent(parent, false);
    Drawing.Place(Rect, 0, 0, 100, 100);
    _graphic = child.AddComponent<ShapeGraphic>();
    _graphic.raycastTarget = false;
    _graphic.FitsRect = true;
    _shape.StrokeWidth = 0;
    _graphic.SetPixelsPerUnit(pixelsPerUnit);
    _graphic.SetShape(_shape, _color, Color.clear);
  }

  public override RectTransform Rect { get; }

  public bool Alive => _graphic != null;

  public override Color Color
  {
    get => _color;
    set
    {
      if (_color == value)
        return;
      _color = value;
      _graphic.SetShape(_shape, _color, Color.clear);
    }
  }

  public override float Rounding
  {
    get => _graphic.FitRounding;
    set
    {
      value = float.IsNaN(value) ? 0 : Mathf.Max(0, value);
      if (Mathf.Approximately(_graphic.FitRounding, value))
        return;
      _graphic.FitRounding = value;
      _graphic.SetVerticesDirty();
    }
  }

  public override bool Visible
  {
    get => Rect != null && Rect.gameObject.activeSelf;
    set
    {
      if (Rect != null && Rect.gameObject.activeSelf != value)
        Rect.gameObject.SetActive(value);
    }
  }

  /// <summary>Keeps edges one screen pixel soft at a new scale.</summary>
  public void SetPixelsPerUnit(float pixelsPerUnit) => _graphic.SetPixelsPerUnit(pixelsPerUnit);
}
