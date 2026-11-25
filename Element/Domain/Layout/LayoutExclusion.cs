namespace Element.Domain.Layout;

// A rounded editor panel that native key viewers must leave uncovered.
internal readonly struct LayoutExclusion
{
  public readonly LayoutBounds Bounds;
  public readonly float Radius;

  public LayoutExclusion(LayoutBounds bounds, float radius)
  {
    Bounds = bounds;
    Radius = radius;
  }
}
