using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Shape;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shape;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.Theme;
using ElementKit;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Mods;

/// <summary>A rounded box drawn as an Element square that follows its UI object's size at once.</summary>
internal sealed class ModBox : RoundedBox
{
  private readonly ShapeGraphic _graphic;
  private readonly OverlayShapeSettings _shape = OverlayShapeGeometry.Create();
  private readonly OverlayGradientFormulas _formulas = new();
  private readonly OverlayResolvedGradient _gradient = new();
  private Color _color = Color.white;
  private Paint _paint;
  private OverlayColorSettings _paintSettings;

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
      if (_paint == null && _color == value)
        return;
      _paint = null;
      _paintSettings = null;
      _color = value;
      _graphic.SetShape(_shape, _color, Color.clear);
    }
  }

  public override Paint Paint
  {
    get => _paint?.Clone();
    set
    {
      _paint = value?.Clone();
      _paintSettings = _paint == null ? null : ModPaints.ToSettings(_paint);
      if (_paintSettings != null)
        _formulas.Compile(_paintSettings);
      DrawPaint();
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

  /// <summary>Players picked another highlight color; a paint that follows it is drawn again.</summary>
  public void HighlightChanged()
  {
    if (_paint != null && _paint.Highlight)
      DrawPaint();
  }

  private void DrawPaint()
  {
    if (_paintSettings == null)
    {
      _graphic.SetShape(_shape, _color, Color.clear);
      return;
    }
    _color = OverlayTextComposer.Resolve(_paintSettings, UiTheme.Accent);
    Vector2 size = Rect.rect.size;
    bool gradient = _formulas.Resolve(
      _paintSettings,
      UiTheme.Accent,
      null,
      Mathf.Max(1, size.x),
      Mathf.Max(1, size.y),
      _gradient
    );
    _graphic.SetShape(_shape, _color, Color.clear, gradient ? _gradient : null, null);
  }
}
