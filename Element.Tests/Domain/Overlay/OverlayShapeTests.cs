using System;
using System.Collections.Generic;
using System.Numerics;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Shape;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

/// <summary>
/// Shape defaults and edits, the point a shape grows from, corner radii and
/// outlines.
/// </summary>
internal static class OverlayShapeTests
{
  internal static void Run()
  {
    ShapeDefaultsAndEdits();
    GrowFromKeepsItsPoint();
    ShapeRadiiStayInsideTheCorner();
    ShapeOutlinesTriangulate();
    OverlappingCutoutsMerge();
  }

  private static void ShapeDefaultsAndEdits()
  {
    OverlayShapeSettings shape = OverlayShapeGeometry.Create();
    Check(shape.Points.Count == 4 && shape.Width == OverlayShapeGeometry.DefaultSize, "a new square has four corners");
    Check(OverlayShapeGeometry.Create(OverlayShapeKind.Triangle).Points.Count == 3, "a triangle has three");
    Check(OverlayShapeGeometry.Create(OverlayShapeKind.Circle).Points.Count == 0, "circles have no corners");
    OverlayShapeSettings damaged = new()
    {
      Kind = OverlayShapeKind.Triangle,
      Width = float.NaN,
      StrokeWidth = -2,
    };
    OverlayShapeGeometry.Normalize(damaged);
    Check(
      damaged.Points.Count == 3 && damaged.Width == OverlayShapeGeometry.DefaultSize && damaged.StrokeWidth == 0,
      "damaged settings are repaired"
    );
  }

  // A chosen grow point stays put when the size changes; Auto uses the anchor's point.
  private static void GrowFromKeepsItsPoint()
  {
    LayoutBounds bounds = new(300, 200, 100, 40);
    AnchoredLayoutPosition left = OverlayLayoutPosition.Capture(bounds, 1920, 1080, Anchor9.Center, OverlayGrowth.Left);
    OverlayLayoutSettings settings = new()
    {
      Customized = true,
      LayoutVersion = OverlayLayoutPosition.CurrentVersion,
      Anchor = left.Anchor,
      OffsetX = left.OffsetX,
      OffsetY = left.OffsetY,
      GrowFrom = OverlayGrowth.Left,
    };
    LayoutBounds wider = OverlayLayoutPosition.Resolve(settings, 300, 40, 1920, 1080);
    Check(
      Near(wider.X, 300, .01f) && Near(wider.Y, 200, .01f),
      "growing from the left keeps the left edge: " + wider.X
    );
    settings.GrowFrom = OverlayGrowth.BottomRight;
    AnchoredLayoutPosition corner = OverlayLayoutPosition.Capture(
      bounds,
      1920,
      1080,
      Anchor9.Center,
      OverlayGrowth.BottomRight
    );
    settings.OffsetX = corner.OffsetX;
    settings.OffsetY = corner.OffsetY;
    LayoutBounds bigger = OverlayLayoutPosition.Resolve(settings, 200, 80, 1920, 1080);
    Check(
      Near(bigger.X + bigger.Width, 400, .01f) && Near(bigger.Y + bigger.Height, 240, .01f),
      "growing from a corner keeps that corner"
    );
    Check(
      OverlayLayoutPosition.GrowPoint(OverlayGrowth.Auto, Anchor9.TopRight) == Anchor9.TopRight
        && OverlayLayoutPosition.GrowPoint(OverlayGrowth.Top, Anchor9.TopRight) == Anchor9.Top,
      "Auto grows from the anchor's point; a choice overrides it"
    );
  }

  private static void ShapeRadiiStayInsideTheCorner()
  {
    List<Vector2> square = new() { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
    Check(Near(OverlayShapeGeometry.MaxRadius(square, 0), 50, .01f), "a square's corner rounds to half its side");
    Check(OverlayShapeGeometry.ClampRadius(square, 0, 80) == OverlayShapeGeometry.MaxRadius(square, 0), "radii clamp");
    Check(OverlayShapeGeometry.ClampRadius(square, 0, -3) == 0, "negative radii clear");
    List<Vector2> straight = new() { new(0, 0), new(50, 0), new(100, 0), new(50, 50) };
    Check(OverlayShapeGeometry.MaxRadius(straight, 1) == 0, "a straight corner can't round");
  }

  private static void ShapeOutlinesTriangulate()
  {
    List<Vector2> square = new() { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
    List<Vector2> outline = new();
    OverlayShapeGeometry.Outline(square, new float[] { 0, 30, 0, 0 }, .5f, outline);
    Check(outline.Count > 5, "a rounded corner adds arc points");
    Check(
      outline.Contains(new Vector2(0, 0)) && !outline.Contains(new Vector2(100, 0)),
      "only the rounded corner is replaced"
    );
    Check(Near(outline[1].X, 70, .01f) && Near(outline[1].Y, 0, .01f), "the arc starts tangent to the edge");
    List<int> triangles = new();
    OverlayShapeGeometry.Triangulate(outline, triangles);
    Check(
      Near(Area(outline, triangles), Math.Abs(OverlayShapeGeometry.SignedArea(outline)), .01f),
      "convex outlines fill exactly"
    );
    List<Vector2> arrow = new() { new(0, 0), new(100, 0), new(40, 40), new(100, 100), new(0, 100) };
    OverlayShapeGeometry.Triangulate(arrow, triangles);
    Check(triangles.Count == 9, "a concave pentagon becomes three triangles");
    Check(
      Near(Area(arrow, triangles), Math.Abs(OverlayShapeGeometry.SignedArea(arrow)), .01f),
      "concave outlines fill exactly"
    );
    OverlayShapeGeometry.Ellipse(200, 100, 32, outline);
    Check(
      outline.Count == 32 && Near(outline[0].X, 200, .01f) && Near(outline[0].Y, 50, .01f),
      "ellipses fill their box"
    );
  }

  private static void OverlappingCutoutsMerge()
  {
    List<LayoutExclusion> exclusions = new()
    {
      new(new LayoutBounds(0, 400, 600, 100), 8),
      new(new LayoutBounds(100, 300, 200, 80), 8),
      new(new LayoutBounds(500, 450, 200, 100), 4),
      new(new LayoutBounds(0, 0, 0, 10), 8),
    };
    LayoutExclusions.Merge(exclusions);
    Check(exclusions.Count == 2, "separate panels stay separate and empty ones drop: " + exclusions.Count);
    LayoutExclusion merged = exclusions.Find(exclusion => exclusion.Bounds.Y == 400);
    Check(
      merged.Bounds.X == 0 && merged.Bounds.Width == 700 && merged.Bounds.Height == 150 && merged.Radius == 4,
      "overlapping panels become one cutout with the smaller radius"
    );
  }

  private static float Area(List<Vector2> points, List<int> triangles)
  {
    float area = 0;
    for (int i = 0; i < triangles.Count; i += 3)
    {
      Vector2 a = points[triangles[i]],
        b = points[triangles[i + 1]],
        c = points[triangles[i + 2]];
      area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * .5f;
    }
    return area;
  }
}
