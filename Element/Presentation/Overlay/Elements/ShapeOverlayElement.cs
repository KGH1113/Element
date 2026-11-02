using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// A square, triangle or circle whose box is the element's rectangle. Numbers
/// tied to live values are worked out into a copy of the settings, which is
/// what is drawn; it is redone only when one of those values changes.
/// </summary>
internal sealed class ShapeOverlayElement : OverlayElementTarget
{
  private readonly ShapeGraphic _graphic;
  private readonly OverlayValues _values;
  private readonly OverlayShapeSettings _resolved = new();
  private readonly OverlayShapeResolver _formulas = new();
  private int _themeVersion = -1;
  private float _resolvedScale = -1;
  private bool _preview;

  public ShapeOverlayElement(Transform parent, OverlayValues values)
    : base(parent, "Shape element")
  {
    _values = values;
    _graphic = Rect.gameObject.AddComponent<ShapeGraphic>();
    _graphic.raycastTarget = false;
  }

  public override string Label =>
    Shape?.Kind switch
    {
      OverlayShapeKind.Triangle => L("삼각형", "Triangle"),
      OverlayShapeKind.Circle => L("원", "Circle"),
      _ => L("사각형", "Square"),
    };

  public OverlayShapeSettings Shape => Settings?.Shape;

  /// <summary>The shape as drawn right now, with live values worked in.</summary>
  public OverlayShapeSettings Resolved => _resolved;

  internal OverlayValues Values => _values;

  /// <summary>The values this shape's numbers use, as catalog bits.</summary>
  public ulong ValueMask => _formulas.Mask;

  /// <summary>Screen pixels per reference unit of the shape's box.</summary>
  public float PixelsPerUnit
  {
    get
    {
      Rect bounds = RectBounds;
      return _resolved.Width > 0 ? bounds.width / _resolved.Width : 1;
    }
  }

  protected override bool TryGetLocalContent(out Rect local)
  {
    local = Rect.rect;
    return true;
  }

  protected override void ApplyContent()
  {
    Settings.Shape ??= OverlayShapeGeometry.Create();
    OverlayShapeGeometry.Normalize(Settings.Shape);
    _formulas.Compile(Settings.Shape);
    Redraw();
  }

  protected override void ResetPose()
  {
    SetPose(
      Rect,
      new Vector2(.5f, .5f),
      new Vector2(.5f, .5f),
      Vector2.zero,
      new Vector2(_resolved.Width, _resolved.Height)
    );
    if (Rect.localScale != Vector3.one)
      Rect.localScale = Vector3.one;
  }

  public override void SetPreview(bool preview)
  {
    if (_preview == preview)
      return;
    _preview = preview;
    if (Shape != null && _formulas.Mask != 0)
      Redraw();
  }

  public void Redraw()
  {
    if (Shape == null)
      return;
    _themeVersion = OverlayTheme.Version;
    _resolvedScale = EffectiveScale;
    IOverlayNumberSource source = _preview ? _values?.WithSamples : _values;
    _formulas.Resolve(Shape, source, _resolvedScale, _resolved);
    Vector2 size = new(_resolved.Width, _resolved.Height);
    bool resized = Rect.sizeDelta != size;
    if (resized)
      Rect.sizeDelta = size;
    _graphic.SetShape(
      _resolved,
      OverlayTextComposer.Resolve(_resolved.Fill, OverlayTheme.Accent, _formulas.FillAlpha, source),
      OverlayTextComposer.Resolve(_resolved.Stroke, OverlayTheme.Accent, _formulas.StrokeAlpha, source)
    );
    _graphic.SetPixelsPerUnit(PixelsPerUnit);
    // A size that follows a value grows away from the point that stays put.
    if (resized)
      ContentSizeChanged();
  }

  /// <summary>Redraws when its values, the highlight color or (for formulas) its scale changed; a mask test otherwise.</summary>
  public void Refresh(ulong changes)
  {
    if (
      _themeVersion != OverlayTheme.Version
      || (changes & _formulas.Mask) != 0
      || (_formulas.Mask != 0 && !Mathf.Approximately(_resolvedScale, EffectiveScale))
    )
      Redraw();
  }

  /// <summary>Keeps edges one screen pixel soft after the screen or the layout changed.</summary>
  public void RefreshPixels() => _graphic.SetPixelsPerUnit(PixelsPerUnit);
}
