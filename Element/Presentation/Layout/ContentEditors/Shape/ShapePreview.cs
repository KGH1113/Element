using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// The shape at its in-game size in the middle of the editor, made smaller
/// only to fit between the bar and the dock, with its formulas worked out on
/// the samples. Its box, in frame units, is where the numbers and handles go.
/// </summary>
internal sealed class ShapePreview
{
  private readonly OverlayValues _values;
  private readonly OverlayShapeResolver _resolver = new();
  private readonly OverlayShapeSettings _resolved = new();
  private RectTransform _shapeRect;
  private ShapeGraphic _graphic;

  public ShapePreview(OverlayValues values)
  {
    _values = values;
  }

  /// <summary>The shape as drawn: its formulas worked out.</summary>
  public OverlayShapeSettings Resolved => _resolved;

  /// <summary>The values the drawn shape uses.</summary>
  public OverlayValueMask Mask => _resolver.Mask;

  public OverlayResolvedGradient FillGradient => _resolver.FillGradient;

  public OverlayResolvedGradient StrokeGradient => _resolver.StrokeGradient;

  /// <summary>The box's top left in frame units.</summary>
  public Vector2 TopLeft { get; private set; }

  /// <summary>Frame units per unit of the shape.</summary>
  public float UnitsToFrame { get; private set; } = 1;

  public void Build(OverlayEditorFrame screen)
  {
    _shapeRect = screen.Ui.Rect("Shape", screen.Stage, 0, 0, 10, 10);
    _graphic = _shapeRect.gameObject.AddComponent<ShapeGraphic>();
    _graphic.raycastTarget = false;
  }

  /// <summary>
  /// Draws <paramref name="shape"/> centered between <paramref name="left"/>
  /// and <paramref name="right"/>, from <paramref name="top"/> down to the dock.
  /// </summary>
  public void Draw(
    OverlayShapeSettings shape,
    float elementScale,
    OverlayEditorFrame screen,
    OverlayInputDock dock,
    float top,
    float left,
    float right
  )
  {
    _resolver.Compile(shape);
    _resolver.Resolve(shape, _values.WithSamples, elementScale, _resolved);
    float bottom = dock.Top - 28;
    float availableWidth = Mathf.Max(60, right - left - 260);
    float availableHeight = Mathf.Max(40, bottom - top - OverlayFormulaField.Height - ShapeMeasures.Gap - 20);
    float units = elementScale * ScreenScale / screen.Scale;
    float fit = Mathf.Min(
      1,
      availableWidth / Mathf.Max(1, _resolved.Width * units),
      availableHeight / Mathf.Max(1, _resolved.Height * units)
    );
    UnitsToFrame = units * fit;
    float boxWidth = _resolved.Width * UnitsToFrame,
      boxHeight = _resolved.Height * UnitsToFrame;
    TopLeft = new Vector2((left + right - boxWidth) * .5f, top + (availableHeight - boxHeight) * .5f);
    float pixels = UnitsToFrame * screen.Scale;
    _shapeRect.anchorMin = _shapeRect.anchorMax = _shapeRect.pivot = new Vector2(0, 1);
    _shapeRect.anchoredPosition = new Vector2(TopLeft.x * screen.Scale, -TopLeft.y * screen.Scale);
    _shapeRect.sizeDelta = new Vector2(_resolved.Width, _resolved.Height);
    _shapeRect.localScale = new Vector3(pixels, pixels, 1);
    IOverlayNumberSource samples = _values.WithSamples;
    _resolver.ResolvePaints(_resolved, samples, OverlayTheme.Accent);
    _graphic.SetShape(
      _resolved,
      OverlayTextComposer.Resolve(_resolved.Fill, OverlayTheme.Accent, _resolver.FillAlpha, samples),
      OverlayTextComposer.Resolve(_resolved.Stroke, OverlayTheme.Accent, _resolver.StrokeAlpha, samples),
      _resolver.FillGradient,
      _resolver.StrokeGradient
    );
    _graphic.SetPixelsPerUnit(pixels);
  }

  /// <summary>A point of the shape's box in frame units.</summary>
  public Vector2 Frame(NVector2 point) => TopLeft + new Vector2(point.X, point.Y) * UnitsToFrame;

  // Screen pixels per reference unit of the overlay canvas.
  private static float ScreenScale => OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
}
