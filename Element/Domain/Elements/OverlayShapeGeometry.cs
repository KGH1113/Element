using System;
using System.Collections.Generic;
using System.Numerics;
using Element.Configuration;

namespace Element.Domain.Elements;

// Shape math in box space: reference units with the origin at the box's top
// left and y pointing down. Kept free of Unity so editing can be tested.
internal static class OverlayShapeGeometry
{
  public const float MinimumSize = 1f;
  public const float DefaultSize = 160f;
  private const float Epsilon = 1e-4f;

  private static readonly Vector2[] SquarePoints = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
  private static readonly Vector2[] TrianglePoints = { new(.5f, 0), new(1, 1), new(0, 1) };

  public static OverlayShapeSettings Create(OverlayShapeKind kind = OverlayShapeKind.Square)
  {
    OverlayShapeSettings shape = new() { Width = DefaultSize, Height = DefaultSize };
    SetKind(shape, kind);
    return shape;
  }

  // Changing the kind keeps the box, colors and stroke but drops vertex edits.
  public static void SetKind(OverlayShapeSettings shape, OverlayShapeKind kind)
  {
    shape.Kind = kind;
    shape.Points = new List<OverlayShapePoint>();
    foreach (Vector2 point in DefaultPoints(kind))
      shape.Points.Add(new OverlayShapePoint { X = point.X, Y = point.Y });
  }

  public static IReadOnlyList<Vector2> DefaultPoints(OverlayShapeKind kind) =>
    kind switch
    {
      OverlayShapeKind.Square => SquarePoints,
      OverlayShapeKind.Triangle => TrianglePoints,
      _ => Array.Empty<Vector2>(),
    };

  public static bool HasCorners(OverlayShapeKind kind) => kind != OverlayShapeKind.Circle;

  /// <summary>Vertices were moved or rounded; changing the kind would lose that.</summary>
  public static bool IsEdited(OverlayShapeSettings shape)
  {
    if (shape == null || !HasCorners(shape.Kind))
      return false;
    IReadOnlyList<Vector2> defaults = DefaultPoints(shape.Kind);
    if (shape.Points == null || shape.Points.Count != defaults.Count)
      return true;
    for (int i = 0; i < defaults.Count; i++)
    {
      OverlayShapePoint point = shape.Points[i];
      if (
        point == null
        || Math.Abs(point.X - defaults[i].X) > Epsilon
        || Math.Abs(point.Y - defaults[i].Y) > Epsilon
        || point.Radius > Epsilon
      )
        return true;
    }
    return false;
  }

  /// <summary>Repairs hand-edited or damaged settings so they can be drawn.</summary>
  public static void Normalize(OverlayShapeSettings shape)
  {
    if (!Enum.IsDefined(typeof(OverlayShapeKind), shape.Kind))
      shape.Kind = OverlayShapeKind.Square;
    shape.Width = Size(shape.Width);
    shape.Height = Size(shape.Height);
    shape.StrokeWidth = Finite(shape.StrokeWidth) ? Math.Max(0, shape.StrokeWidth) : 0;
    shape.Fill ??= OverlayColorSettings.HighlightColor();
    shape.Stroke ??= OverlayColorSettings.Custom("#FFFFFF");
    int expected = DefaultPoints(shape.Kind).Count;
    if (shape.Points == null || shape.Points.Count != expected || shape.Points.Contains(null))
    {
      SetKind(shape, shape.Kind);
      return;
    }
    foreach (OverlayShapePoint point in shape.Points)
    {
      point.X = Finite(point.X) ? Clamp(point.X, 0, 1) : 0;
      point.Y = Finite(point.Y) ? Clamp(point.Y, 0, 1) : 0;
      point.Radius = Finite(point.Radius) ? Math.Max(0, point.Radius) : 0;
    }
  }

  public static void Corners(OverlayShapeSettings shape, List<Vector2> output)
  {
    output.Clear();
    if (shape?.Points == null)
      return;
    foreach (OverlayShapePoint point in shape.Points)
      output.Add(new Vector2(point.X * shape.Width, point.Y * shape.Height));
  }

  // Each corner may use half of each adjacent edge, so neighbours never overlap.
  public static float MaxRadius(IReadOnlyList<Vector2> corners, int index)
  {
    if (!TryCorner(corners, index, out Vector2 _, out Vector2 _, out float half, out float shortest))
      return 0;
    return shortest * .5f * (float)Math.Tan(half);
  }

  public static float ClampRadius(IReadOnlyList<Vector2> corners, int index, float radius) =>
    Finite(radius) ? Clamp(radius, 0, MaxRadius(corners, index)) : 0;

  /// <summary>
  /// The small round handle sits inside the corner on its bisector, <paramref name="offset"/>
  /// past where the arc's center is. Returns false for a straight corner.
  /// </summary>
  public static bool TryRadiusHandle(
    IReadOnlyList<Vector2> corners,
    int index,
    float radius,
    float offset,
    out Vector2 handle
  )
  {
    handle = default;
    if (!TryCorner(corners, index, out Vector2 vertex, out Vector2 bisector, out float half, out _))
      return false;
    float r = ClampRadius(corners, index, radius);
    handle = vertex + bisector * (offset + r / (float)Math.Sin(half));
    return true;
  }

  public static float RadiusFromHandle(IReadOnlyList<Vector2> corners, int index, Vector2 pointer, float offset)
  {
    if (!TryCorner(corners, index, out Vector2 vertex, out Vector2 bisector, out float half, out _))
      return 0;
    float along = Vector2.Dot(pointer - vertex, bisector) - offset;
    return ClampRadius(corners, index, Math.Max(0, along) * (float)Math.Sin(half));
  }

  /// <summary>
  /// Moves one corner. The box grows or shrinks to the corners' extent, so
  /// <paramref name="origin"/> is where the new box's top left lies in the old box.
  /// </summary>
  public static void MoveCorner(
    OverlayShapeSettings shape,
    IReadOnlyList<Vector2> startCorners,
    int index,
    Vector2 position,
    out Vector2 origin
  )
  {
    origin = Vector2.Zero;
    if (index < 0 || index >= startCorners.Count || !Finite(position.X) || !Finite(position.Y))
      return;
    Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
    Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int i = 0; i < startCorners.Count; i++)
    {
      Vector2 point = i == index ? position : startCorners[i];
      min = Vector2.Min(min, point);
      max = Vector2.Max(max, point);
    }
    float width = Math.Max(MinimumSize, max.X - min.X);
    float height = Math.Max(MinimumSize, max.Y - min.Y);
    for (int i = 0; i < startCorners.Count; i++)
    {
      Vector2 point = i == index ? position : startCorners[i];
      shape.Points[i].X = Clamp((point.X - min.X) / width, 0, 1);
      shape.Points[i].Y = Clamp((point.Y - min.Y) / height, 0, 1);
    }
    shape.Width = width;
    shape.Height = height;
    origin = min;
  }

  /// <summary>Corner outline with each radius replaced by a tangent arc.</summary>
  public static void Outline(
    IReadOnlyList<Vector2> corners,
    IReadOnlyList<float> radii,
    float segmentsPerUnitRadius,
    List<Vector2> output
  )
  {
    output.Clear();
    int count = corners.Count;
    for (int i = 0; i < count; i++)
    {
      float radius = i < radii.Count ? ClampRadius(corners, i, radii[i]) : 0;
      if (radius <= Epsilon || !TryCorner(corners, i, out Vector2 vertex, out Vector2 bisector, out float half, out _))
      {
        output.Add(corners[i]);
        continue;
      }
      Vector2 toPrevious = Vector2.Normalize(corners[(i + count - 1) % count] - vertex);
      Vector2 toNext = Vector2.Normalize(corners[(i + 1) % count] - vertex);
      float tangent = radius / (float)Math.Tan(half);
      Vector2 center = vertex + bisector * (radius / (float)Math.Sin(half));
      Vector2 start = vertex + toPrevious * tangent;
      Vector2 end = vertex + toNext * tangent;
      double from = Math.Atan2(start.Y - center.Y, start.X - center.X);
      double to = Math.Atan2(end.Y - center.Y, end.X - center.X);
      double sweep = to - from;
      while (sweep > Math.PI)
        sweep -= Math.PI * 2;
      while (sweep < -Math.PI)
        sweep += Math.PI * 2;
      int segments = Math.Max(2, Math.Min(24, (int)Math.Ceiling(Math.Abs(sweep) * radius * segmentsPerUnitRadius)));
      for (int step = 0; step <= segments; step++)
      {
        double angle = from + sweep * step / segments;
        output.Add(center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius);
      }
    }
  }

  public static void Ellipse(float width, float height, int segments, List<Vector2> output)
  {
    output.Clear();
    segments = Math.Max(8, segments);
    for (int i = 0; i < segments; i++)
    {
      double angle = Math.PI * 2 * i / segments;
      output.Add(
        new Vector2(
          width * .5f + (float)Math.Cos(angle) * width * .5f,
          height * .5f + (float)Math.Sin(angle) * height * .5f
        )
      );
    }
  }

  public static float SignedArea(IReadOnlyList<Vector2> polygon)
  {
    double area = 0;
    for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
      area += (double)polygon[j].X * polygon[i].Y - (double)polygon[i].X * polygon[j].Y;
    return (float)(area * .5);
  }

  /// <summary>
  /// Triangle indices covering a simple polygon. Convex outlines use a fan; a
  /// dragged corner can make the outline concave, which uses ear clipping.
  /// Self-crossing outlines fall back to a fan and may look folded.
  /// </summary>
  public static void Triangulate(IReadOnlyList<Vector2> polygon, List<int> output)
  {
    output.Clear();
    int count = polygon.Count;
    if (count < 3)
      return;
    float sign = SignedArea(polygon) >= 0 ? 1 : -1;
    if (IsConvex(polygon, sign))
    {
      for (int i = 1; i < count - 1; i++)
      {
        output.Add(0);
        output.Add(i);
        output.Add(i + 1);
      }
      return;
    }
    List<int> remaining = new(count);
    for (int i = 0; i < count; i++)
      remaining.Add(i);
    int guard = count * count;
    while (remaining.Count > 3 && guard-- > 0)
    {
      bool clipped = false;
      for (int i = 0; i < remaining.Count; i++)
      {
        int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
        int current = remaining[i];
        int next = remaining[(i + 1) % remaining.Count];
        float turn = Cross(polygon[current] - polygon[previous], polygon[next] - polygon[current]) * sign;
        if (turn < 0)
          continue;
        if (turn > Epsilon * Epsilon && ContainsOther(polygon, remaining, previous, current, next, sign))
          continue;
        output.Add(previous);
        output.Add(current);
        output.Add(next);
        remaining.RemoveAt(i);
        clipped = true;
        break;
      }
      if (!clipped)
        break;
    }
    for (int i = 1; i < remaining.Count - 1; i++)
    {
      output.Add(remaining[0]);
      output.Add(remaining[i]);
      output.Add(remaining[i + 1]);
    }
  }

  public static bool IsConvex(IReadOnlyList<Vector2> polygon, float sign)
  {
    int count = polygon.Count;
    for (int i = 0; i < count; i++)
    {
      Vector2 a = polygon[(i + count - 1) % count],
        b = polygon[i],
        c = polygon[(i + 1) % count];
      if (Cross(b - a, c - b) * sign < -Epsilon)
        return false;
    }
    return true;
  }

  private static bool ContainsOther(
    IReadOnlyList<Vector2> polygon,
    List<int> remaining,
    int a,
    int b,
    int c,
    float sign
  )
  {
    foreach (int index in remaining)
    {
      if (index == a || index == b || index == c)
        continue;
      Vector2 p = polygon[index];
      if (
        Cross(polygon[b] - polygon[a], p - polygon[a]) * sign >= 0
        && Cross(polygon[c] - polygon[b], p - polygon[b]) * sign >= 0
        && Cross(polygon[a] - polygon[c], p - polygon[c]) * sign >= 0
      )
        return true;
    }
    return false;
  }

  // The corner's vertex, the unit bisector between its edges, half the angle
  // between those edges, and the shorter edge's length.
  private static bool TryCorner(
    IReadOnlyList<Vector2> corners,
    int index,
    out Vector2 vertex,
    out Vector2 bisector,
    out float half,
    out float shortest
  )
  {
    vertex = bisector = default;
    half = shortest = 0;
    int count = corners.Count;
    if (count < 3 || index < 0 || index >= count)
      return false;
    vertex = corners[index];
    Vector2 toPrevious = corners[(index + count - 1) % count] - vertex;
    Vector2 toNext = corners[(index + 1) % count] - vertex;
    float previousLength = toPrevious.Length(),
      nextLength = toNext.Length();
    if (previousLength < Epsilon || nextLength < Epsilon)
      return false;
    toPrevious /= previousLength;
    toNext /= nextLength;
    float cosine = Clamp(Vector2.Dot(toPrevious, toNext), -1, 1);
    half = (float)Math.Acos(cosine) * .5f;
    Vector2 sum = toPrevious + toNext;
    if (sum.Length() < 1e-3f || half < 1e-3f)
      return false;
    bisector = Vector2.Normalize(sum);
    shortest = Math.Min(previousLength, nextLength);
    return true;
  }

  private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

  private static float Size(float value) => Finite(value) ? Math.Max(MinimumSize, value) : DefaultSize;

  private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

  private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}
