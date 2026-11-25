using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Migration;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Overlay.Shape;

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

  // The outline in screen pixels for the key viewers behind it; outer, then inner for a border.
  private readonly List<NVector2> _coverPoints = new(128);
  private readonly List<int> _coverContours = new(2);
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
    Id switch
    {
      OverlayElementDefaults.ProgressBarId => L("진행도 바", "Progress bar"),
      OverlayElementDefaults.ProgressTrackId => L("진행도 바 배경", "Progress bar track"),
      OverlayElementDefaults.ProgressFillId => L("진행도 바 채우기", "Progress bar fill"),
      _ => Shape?.Kind switch
      {
        OverlayShapeKind.Triangle => L("삼각형", "Triangle"),
        OverlayShapeKind.Circle => L("원", "Circle"),
        _ => L("사각형", "Square"),
      },
    };

  public OverlayShapeSettings Shape => Settings?.Shape;

  /// <summary>The shape as drawn right now, with live values worked in.</summary>
  public OverlayShapeSettings Resolved => _resolved;

  internal OverlayValues Values => _values;

  /// <summary>The values this shape's numbers use.</summary>
  public OverlayValueMask ValueMask => _formulas.Mask;

  /// <summary>
  /// Pixels per reference unit of the shape's box, in whatever the canvas is
  /// drawn into: the game window, or OrbitRender's video while it renders.
  /// </summary>
  public float PixelsPerUnit
  {
    get
    {
      Rect bounds = CanvasRectBounds;
      return _resolved.Width > 0 ? bounds.width * CanvasScale / _resolved.Width : 1;
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

  // Built-in shapes start where they belong; others in the middle.
  protected override void ResetPose()
  {
    Vector2 center = new(.5f, .5f);
    bool placed = ShapeDefaultPlacement.TryGet(
      Settings?.DefaultPlacement,
      out Vector2 anchor,
      out Vector2 pivot,
      out Vector2 position
    );
    SetPose(
      Rect,
      placed ? anchor : center,
      placed ? pivot : center,
      placed ? position : Vector2.zero,
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
    if (Shape != null && !_formulas.Mask.IsEmpty)
      Redraw();
  }

  public void Redraw()
  {
    if (Shape == null)
      return;
    _themeVersion = UiTheme.Version;
    _resolvedScale = EffectiveScale;
    IOverlayNumberSource source = _preview ? _values?.WithSamples : _values;
    _formulas.Resolve(Shape, source, _resolvedScale, _resolved);
    Vector2 size = new(_resolved.Width, _resolved.Height);
    bool resized = Rect.sizeDelta != size;
    if (resized)
      Rect.sizeDelta = size;
    _formulas.ResolvePaints(_resolved, source, UiTheme.Accent);
    _graphic.SetShape(
      _resolved,
      _formulas.FillColor(_resolved, source, UiTheme.Accent),
      _formulas.StrokeColor(_resolved, source, UiTheme.Accent),
      _formulas.FillGradient,
      _formulas.StrokeGradient
    );
    _graphic.SetPixelsPerUnit(PixelsPerUnit);
    // A size that follows a value grows away from the point that stays put.
    if (resized)
      ContentSizeChanged();
  }

  /// <summary>Redraws when its values, the highlight color or (for formulas) its scale changed; a mask test otherwise.</summary>
  public override void Refresh(OverlayValueMask changes, bool settle = false)
  {
    if (
      _themeVersion != UiTheme.Version
      || changes.Overlaps(_formulas.Mask)
      || (!_formulas.Mask.IsEmpty && !Mathf.Approximately(_resolvedScale, EffectiveScale))
    )
      Redraw();
  }

  /// <summary>Keeps edges one screen pixel soft after the screen or the layout changed.</summary>
  public void RefreshPixels() => _graphic.SetPixelsPerUnit(PixelsPerUnit);

  // Both only count up, so their sum changes whenever either does.
  public override int CoverVersion => base.CoverVersion + _graphic.DrawnVersion;

  public override bool TryGetCoverBounds(out LayoutBounds screenTopLeft)
  {
    screenTopLeft = default;
    return _graphic.Drawn && base.TryGetCoverBounds(out screenTopLeft);
  }

  /// <summary>
  /// The outline the graphic drew, with the border's width: all of it with a
  /// fill, the band inside the outline for a border alone.
  /// </summary>
  public override void Rasterize(ViewerCoverRuns runs)
  {
    if (!OnScreen || !_graphic.Drawn)
      return;
    IReadOnlyList<Vector2> outline = _graphic.DrawnOutline;
    IReadOnlyList<Vector2> normals = _graphic.DrawnNormals;
    Matrix4x4 toWorld = Rect.localToWorldMatrix;
    float height = Screen.height;
    _coverPoints.Clear();
    _coverContours.Clear();
    _coverContours.Add(0);
    for (int i = 0; i < outline.Count; i++)
      _coverPoints.Add(ToScreen(toWorld, outline[i], height));
    float border = _graphic.DrawnBorder;
    // A border as wide as half the shape leaves no hole.
    if (!_graphic.DrawnFill && border < Mathf.Min(_resolved.Width, _resolved.Height) * .5f - .01f)
    {
      _coverContours.Add(_coverPoints.Count);
      for (int i = 0; i < outline.Count && i < normals.Count; i++)
        _coverPoints.Add(ToScreen(toWorld, outline[i] - normals[i] * border, height));
    }
    runs.FillEvenOdd(_coverPoints, _coverContours);
  }
}
