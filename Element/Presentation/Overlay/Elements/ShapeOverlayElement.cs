using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// A square, triangle or circle whose box is the element's rectangle. Corner
/// and radius edits change the shape settings directly; where the box sits is
/// staged and committed like any other element.
/// </summary>
internal sealed class ShapeOverlayElement : OverlayElementTarget
{
  private readonly ShapeGraphic _graphic;
  private readonly List<NVector2> _corners = new(4);
  private int _themeVersion = -1;
  private bool _contentEdited;

  public ShapeOverlayElement(Transform parent)
    : base(parent, "Shape element")
  {
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

  /// <summary>Screen pixels per reference unit of the shape's box.</summary>
  public float PixelsPerUnit
  {
    get
    {
      Rect bounds = RectBounds;
      return Shape != null && Shape.Width > 0 ? bounds.width / Shape.Width : 1;
    }
  }

  protected override bool ContentChangedWhileStaged => _contentEdited;

  protected override bool TryGetLocalContent(out Rect local)
  {
    local = Rect.rect;
    return true;
  }

  protected override void ApplyContent()
  {
    Settings.Shape ??= OverlayShapeGeometry.Create();
    OverlayShapeGeometry.Normalize(Settings.Shape);
    _contentEdited = false;
    Redraw();
  }

  protected override void ResetPose()
  {
    SetPose(Rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(Shape.Width, Shape.Height));
    if (Rect.localScale != Vector3.one)
      Rect.localScale = Vector3.one;
  }

  public void Redraw()
  {
    if (Shape == null)
      return;
    _themeVersion = OverlayTheme.Version;
    Vector2 size = new(Shape.Width, Shape.Height);
    if (Rect.sizeDelta != size)
      Rect.sizeDelta = size;
    _graphic.SetShape(Shape, ShapeGraphic.Resolve(Shape.Fill), ShapeGraphic.Resolve(Shape.Stroke));
    _graphic.SetPixelsPerUnit(PixelsPerUnit);
  }

  /// <summary>Follows highlight color and on-screen size changes; cheap when neither changed.</summary>
  public void Refresh()
  {
    if (_themeVersion != OverlayTheme.Version)
      Redraw();
    else
      _graphic.SetPixelsPerUnit(PixelsPerUnit);
  }

  /// <summary>Corners in box space, reference units.</summary>
  public IReadOnlyList<NVector2> Corners
  {
    get
    {
      OverlayShapeGeometry.Corners(Shape, _corners);
      return _corners;
    }
  }

  public Vector2 BoxToScreen(NVector2 point)
  {
    Rect bounds = RectBounds;
    float scale = PixelsPerUnit;
    return new Vector2(bounds.x + point.X * scale, bounds.y + point.Y * scale);
  }

  public NVector2 ScreenToBox(Vector2 point)
  {
    Rect bounds = RectBounds;
    float scale = Mathf.Max(.0001f, PixelsPerUnit);
    return new NVector2((point.x - bounds.x) / scale, (point.y - bounds.y) / scale);
  }

  /// <summary>
  /// Gives the box a new size in place, keeping <paramref name="pixelsPerUnit"/>
  /// from the gesture's start: the new box's top left lands at <paramref name="topLeft"/>
  /// on screen. Staged until the gesture commits.
  /// </summary>
  public void SetBox(float width, float height, Vector2 topLeft, float pixelsPerUnit)
  {
    if (Shape == null)
      return;
    BeginStage();
    Shape.Width = Mathf.Max(OverlayShapeGeometry.MinimumSize, width);
    Shape.Height = Mathf.Max(OverlayShapeGeometry.MinimumSize, height);
    _contentEdited = true;
    Redraw();
    Vector2 size = new Vector2(Shape.Width, Shape.Height) * pixelsPerUnit;
    StageCenter(topLeft + size * .5f);
  }

  /// <summary>Stretches the box to <paramref name="screen"/> without scaling the shape.</summary>
  public void SetStretchedBounds(Rect screen, float pixelsPerUnit)
  {
    pixelsPerUnit = Mathf.Max(.0001f, pixelsPerUnit);
    SetBox(screen.width / pixelsPerUnit, screen.height / pixelsPerUnit, screen.position, pixelsPerUnit);
  }

  public void CommitShape()
  {
    CommitLayout(false, true);
    _contentEdited = false;
  }
}
