using System;
using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.Overlay.Defaults;

internal sealed class OverlayDefaultElement
{
  public readonly string Id;
  public readonly string TemplateId;

  // The built-in overlay's font size over the text element's base size.
  public readonly float BaseScale;

  // The point a built-in shape keeps when its size follows a value.
  public readonly OverlayGrowth GrowFrom;
  private readonly Func<List<OverlayTextRun>> _runs;
  private readonly Func<OverlayShapeSettings> _shape;

  public OverlayDefaultElement(string id, float baseScale, Func<List<OverlayTextRun>> runs)
  {
    Id = id;
    TemplateId = OverlayElementKinds.Text;
    BaseScale = baseScale;
    GrowFrom = OverlayGrowth.Auto;
    _runs = runs;
  }

  public OverlayDefaultElement(string id, OverlayGrowth growFrom, Func<OverlayShapeSettings> shape)
  {
    Id = id;
    TemplateId = OverlayElementKinds.Shape;
    BaseScale = 1;
    GrowFrom = growFrom;
    _shape = shape;
  }

  public bool IsShape => _shape != null;

  public List<OverlayTextRun> CreateRuns() => _runs?.Invoke() ?? new List<OverlayTextRun>();

  public OverlayShapeSettings CreateShape() => _shape?.Invoke();
}
