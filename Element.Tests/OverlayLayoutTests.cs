using System;
using System.IO;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Domain.Layout;

namespace Element.Tests;

internal static class OverlayLayoutTests
{
  public static void Run()
  {
    AnchoredPositions();
    NativeGrowth();
    LayoutBounds start = new(100f, 80f, 200f, 100f);
    LayoutHandle[] handles =
    {
      LayoutHandle.Left,
      LayoutHandle.Right,
      LayoutHandle.Top,
      LayoutHandle.Bottom,
      LayoutHandle.TopLeft,
      LayoutHandle.TopRight,
      LayoutHandle.BottomLeft,
      LayoutHandle.BottomRight,
    };
    foreach (LayoutHandle handle in handles)
    {
      bool left = handle == LayoutHandle.Left || handle == LayoutHandle.TopLeft || handle == LayoutHandle.BottomLeft;
      bool right =
        handle == LayoutHandle.Right || handle == LayoutHandle.TopRight || handle == LayoutHandle.BottomRight;
      bool top = handle == LayoutHandle.Top || handle == LayoutHandle.TopLeft || handle == LayoutHandle.TopRight;
      bool bottom =
        handle == LayoutHandle.Bottom || handle == LayoutHandle.BottomLeft || handle == LayoutHandle.BottomRight;
      LayoutBounds result = OverlayLayoutGeometry.Drag(
        start,
        handle,
        left ? -40f
          : right ? 40f
          : 0f,
        top ? -20f
          : bottom ? 20f
          : 0f,
        1920f,
        1080f
      );
      Near(240f, result.Width, handle + " grows in the grabbed direction");
      Near(120f, result.Height, handle + " preserves aspect ratio");
      if (left)
        Near(start.Right, result.Right, handle + " anchors right");
      else if (right)
        Near(start.X, result.X, handle + " anchors left");
      else
        Near(start.X + start.Width / 2f, result.X + result.Width / 2f, handle + " anchors horizontal center");
      if (top)
        Near(start.Bottom, result.Bottom, handle + " anchors bottom");
      else if (bottom)
        Near(start.Y, result.Y, handle + " anchors top");
      else
        Near(start.Y + start.Height / 2f, result.Y + result.Height / 2f, handle + " anchors vertical center");

      LayoutBounds stationary = OverlayLayoutGeometry.Drag(start, handle, 0f, 0f, 1920f, 1080f);
      Near(start.X, stationary.X, "pointer-down must not snap to an anchor");
      Near(start.Y, stationary.Y, "pointer-down must preserve grab offset");
      Near(start.Width, stationary.Width, "zero delta keeps scale");
    }

    LayoutBounds shrunk = OverlayLayoutGeometry.Drag(start, LayoutHandle.TopLeft, 2000f, 2000f, 1920f, 1080f);
    Near(40f, shrunk.Width, "minimum scale prevents inverted bounds");
    Near(start.Right, shrunk.Right, "minimum clamp preserves opposite X anchor");
    Near(start.Bottom, shrunk.Bottom, "minimum clamp preserves opposite Y anchor");
    LayoutBounds enlarged = OverlayLayoutGeometry.Drag(
      start,
      LayoutHandle.BottomRight,
      20000f,
      20000f,
      1920f,
      1080f,
      0.1f,
      2f
    );
    Near(400f, enlarged.Width, "absolute scale limit supplied relative to drag start");
    Near(start.X, enlarged.X, "maximum clamp preserves opposite anchor");
    LayoutBounds nearEdge = new(800f, 400f, 200f, 100f);
    LayoutBounds viewport = OverlayLayoutGeometry.Drag(nearEdge, LayoutHandle.BottomRight, 1000f, 1000f, 1024f, 600f);
    Near(1000f, viewport.Width, "growth can extend beyond the viewport");
    Near(500f, viewport.Height, "offscreen growth preserves ratio");
    Near(800f, viewport.X, "viewport clamp does not move opposite corner");
    Near(400f, viewport.Y, "viewport clamp does not move opposite corner vertically");
    viewport = OverlayLayoutGeometry.Drag(nearEdge, LayoutHandle.Left, -1000f, 0f, 1024f, 600f);
    Near(1000f, viewport.Width, "edge drag can extend beyond its other axis");
    Near(1000f, viewport.Right, "edge growth preserves opposite edge");
    Near(450f, viewport.Y + viewport.Height * 0.5f, "edge growth preserves other axis center");

    LayoutBounds moved = OverlayLayoutGeometry.Drag(start, LayoutHandle.Move, 35f, -20f, 1920f, 1080f);
    Near(135f, moved.X, "drag is one-to-one in X");
    Near(60f, moved.Y, "drag is one-to-one in Y");
    moved = OverlayLayoutGeometry.Drag(start, LayoutHandle.Move, -999f, 9999f, 1920f, 1080f);
    Near(-180f, moved.X, "left edge keeps 10 percent visible");
    Near(1070f, moved.Y, "bottom edge keeps 10 percent visible");
    moved = OverlayLayoutGeometry.Drag(start, LayoutHandle.Move, 99999, -99999, 1920, 1080);
    Near(1900, moved.X, "right edge keeps 10 percent visible");
    Near(-90, moved.Y, "top edge keeps 10 percent visible");

    // Exercise every fixed anchor from partially offscreen starting positions.
    // Shrinking is just as important: it must not hide the last visible sliver.
    Random random = new(721);
    for (int i = 0; i < 2000; i++)
    {
      LayoutBounds sample = OverlayLayoutGeometry.KeepVisible(
        new LayoutBounds(
          (float)random.NextDouble() * 2400 - 800,
          (float)random.NextDouble() * 1400 - 400,
          20 + (float)random.NextDouble() * 900,
          10 + (float)random.NextDouble() * 500
        ),
        1024,
        600
      );
      LayoutHandle handle = handles[i % handles.Length];
      LayoutBounds result = OverlayLayoutGeometry.Drag(
        sample,
        handle,
        (float)random.NextDouble() * 10000 - 5000,
        (float)random.NextDouble() * 10000 - 5000,
        1024,
        600
      );
      Visible(result, 1024, 600);
      Near(sample.Width / sample.Height, result.Width / result.Height, "random resize preserves ratio");
      LayoutBounds scaled = OverlayLayoutGeometry.ScaleAtCenter(sample, i % 2 == 0 ? .1f : 50f, 1024, 600, .2f, 5f);
      Visible(scaled, 1024, 600);
      Near(sample.X + sample.Width * .5f, scaled.X + scaled.Width * .5f, "size input preserves center");
    }

    Hit(LayoutHandle.TopLeft, start, 103f, 83f);
    Hit(LayoutHandle.TopRight, start, 298f, 78f);
    Hit(LayoutHandle.BottomLeft, start, 99f, 181f);
    Hit(LayoutHandle.BottomRight, start, 302f, 182f);
    Hit(LayoutHandle.Top, start, 200f, 82f);
    Hit(LayoutHandle.Bottom, start, 200f, 181f);
    Hit(LayoutHandle.Left, start, 102f, 130f);
    Hit(LayoutHandle.Right, start, 298f, 130f);
    Hit(LayoutHandle.Move, start, 200f, 130f);
    Hit(LayoutHandle.None, start, 80f, 130f);

    LayoutBounds invalid = OverlayLayoutGeometry.Drag(start, LayoutHandle.Left, float.NaN, 0f, 1920f, 1080f);
    Near(start.Width, invalid.Width, "invalid pointer positions cannot poison saved layout");
    LayoutBounds empty = OverlayLayoutGeometry.Drag(
      new LayoutBounds(1f, 2f, 0f, 0f),
      LayoutHandle.Right,
      30f,
      0f,
      1920f,
      1080f
    );
    Near(0f, empty.Width, "unavailable zero-sized targets are safe");
  }

  private static void AnchoredPositions()
  {
    Near(1, OverlayLayoutPosition.ScreenScale(1920, 1080), "reference viewport has unit scale");
    Near(2, OverlayLayoutPosition.ScreenScale(3840, 2160), "uniform viewport growth scales both axes equally");
    Near(
      (float)Math.Sqrt(4d / 3),
      OverlayLayoutPosition.ScreenScale(2560, 1080),
      "ultrawide scale matches the existing half-width half-height CanvasScaler"
    );
    foreach (Anchor9 anchor in Enum.GetValues(typeof(Anchor9)))
    {
      float ax = OverlayLayoutPosition.AnchorX(anchor);
      float ay = OverlayLayoutPosition.AnchorY(anchor);
      LayoutBounds source = new((.1f + ax * .8f) * 1920 - 100, (.1f + ay * .8f) * 1080 - 50, 200, 100);
      AnchoredLayoutPosition captured = OverlayLayoutPosition.Capture(source, 1920, 1080);
      Check(captured.IsValid && captured.Anchor == anchor, "normalized center selects all nine anchors");
      OverlayLayoutSettings settings = Anchored(captured);
      LayoutBounds restored = OverlayLayoutPosition.Resolve(settings, 200, 100, 1920, 1080);
      Near(source.X, restored.X, anchor + " captures and restores without horizontal displacement");
      Near(source.Y, restored.Y, anchor + " captures and restores without vertical displacement");

      foreach (var viewport in new[] { (1280f, 720f), (2560f, 1080f), (1080f, 1920f), (800f, 600f) })
      {
        float scale = OverlayLayoutPosition.ScreenScale(viewport.Item1, viewport.Item2);
        restored = OverlayLayoutPosition.Resolve(settings, 200 * scale, 100 * scale, viewport.Item1, viewport.Item2);
        Near(
          captured.OffsetX,
          (restored.X + ax * restored.Width - ax * viewport.Item1) / scale,
          anchor + " retains the horizontal logical margin across aspect ratios"
        );
        Near(
          captured.OffsetY,
          (restored.Y + ay * restored.Height - ay * viewport.Item2) / scale,
          anchor + " retains the vertical logical margin across aspect ratios"
        );
      }
    }

    foreach (float centerX in new[] { .25f, .75f })
    foreach (float centerY in new[] { .25f, .75f })
      Check(
        OverlayLayoutPosition.SelectAnchor(new(centerX * 1920 - 50, centerY * 1080 - 30, 100, 60), 1920, 1080)
          == Anchor9.Center,
        "equal-distance ties favor the center on each axis"
      );
    Check(
      OverlayLayoutPosition.SelectAnchor(new(.26f * 4000 - 50, .24f * 500 - 30, 100, 60), 4000, 500) == Anchor9.Top,
      "anchor distance uses normalized axes even on a very wide viewport"
    );

    LayoutBounds bottomRight = new(1696, 956, 200, 100);
    OverlayLayoutSettings edge = Anchored(OverlayLayoutPosition.Capture(bottomRight, 1920, 1080));
    Near(-24, edge.OffsetX, "right anchor stores a signed 24px inward margin");
    Near(-24, edge.OffsetY, "bottom anchor stores a signed 24px inward margin");
    LayoutBounds larger = OverlayLayoutPosition.Resolve(edge, 400, 200, 3840, 2160);
    Near(48, 3840 - larger.Right, "right margin scales with the shared viewport scale");
    Near(48, 2160 - larger.Bottom, "bottom margin scales with the same viewport scale");
    LayoutBounds widerContent = OverlayLayoutPosition.Resolve(edge, 700, 80, 1920, 1080);
    Near(bottomRight.Right, widerContent.Right, "restoration applies current content width before right alignment");
    Near(bottomRight.Bottom, widerContent.Bottom, "restoration applies current content height before bottom alignment");

    LayoutBounds movedAcrossCenter = new(80, 60, 300, 120);
    AnchoredLayoutPosition resize = OverlayLayoutPosition.Capture(movedAcrossCenter, 1920, 1080, edge.Anchor);
    Check(resize.Anchor == Anchor9.BottomRight, "explicit resize capture retains the saved anchor");
    Check(
      OverlayLayoutPosition.Capture(movedAcrossCenter, 1920, 1080).Anchor == Anchor9.TopLeft,
      "completed move capture selects its new nearest anchor"
    );

    OverlayLayoutSettings offscreen = Anchored(OverlayLayoutPosition.Capture(new(-180, -90, 200, 100), 1920, 1080));
    LayoutBounds raw = OverlayLayoutPosition.Resolve(offscreen, 400, 200, 3840, 2160);
    Near(-360, raw.X, "signed offsets preserve deliberate horizontal offscreen placement");
    Near(-180, raw.Y, "signed offsets preserve deliberate vertical offscreen placement");
    float savedOffsetX = offscreen.OffsetX;
    float savedOffsetY = offscreen.OffsetY;
    OverlayLayoutGeometry.KeepVisible(OverlayLayoutPosition.Resolve(offscreen, 40, 20, 400, 300), 400, 300);
    Near(savedOffsetX, offscreen.OffsetX, "display-only visibility fitting never rewrites horizontal persistence");
    Near(savedOffsetY, offscreen.OffsetY, "display-only visibility fitting never rewrites vertical persistence");
    Check(
      !OverlayLayoutPosition.Capture(new(0, 0, 0, 100), 1920, 1080).IsValid
        && !OverlayLayoutPosition.Capture(new(float.NaN, 0, 100, 100), 1920, 1080).IsValid
        && !OverlayLayoutPosition.Capture(new(0, 0, 100, 100), 0, 1080).IsValid
        && !OverlayLayoutPosition.Capture(new(0, 0, 100, 100), 1920, 1080, (Anchor9)99).IsValid,
      "unavailable geometry cannot produce a persisted capture"
    );
    AnchorPersistence();
  }

  private static void NativeGrowth()
  {
    foreach (
      var sample in new[]
      {
        (Anchor9.TopLeft, 100f, 100f),
        (Anchor9.TopRight, 1820f, 100f),
        (Anchor9.BottomLeft, 100f, 980f),
        (Anchor9.BottomRight, 1820f, 980f),
        (Anchor9.Center, 960f, 540f),
      }
    )
    {
      LayoutBounds original = new(sample.Item2 - 50, sample.Item3 - 30, 100, 60);
      OverlayLayoutSettings placement = NativeViewerGrowth.Capture(original, 1920, 1080, .75f);
      Check(placement?.Anchor == sample.Item1, "native viewer selects its nearest of five screen points");
      float ax = OverlayLayoutPosition.AnchorX(placement.Anchor);
      float ay = OverlayLayoutPosition.AnchorY(placement.Anchor);
      float fixedX = original.X + ax * original.Width;
      float fixedY = original.Y + ay * original.Height;
      foreach (var size in new[] { (800f, 500f), (1600f, 900f), (70f, 40f), (100f, 60f) })
      {
        LayoutBounds resized = OverlayLayoutPosition.Resolve(placement, size.Item1, size.Item2, 1920, 1080);
        Near(fixedX, resized.X + ax * resized.Width, "growth and shrinkage retain the horizontal anchor");
        Near(fixedY, resized.Y + ay * resized.Height, "growth and shrinkage retain the vertical anchor");
        Near(size.Item1, resized.Width, "growth beyond 430px does not fit or shrink content");
        Near(size.Item2, resized.Height, "growth beyond 270px does not fit or shrink content");
        Near(.75f, placement.Scale, "automatic content changes preserve the user scale");
        Check(placement.Anchor == sample.Item1, "growing center cannot reselect the captured anchor");
      }
    }
    Check(
      NativeViewerGrowth.SelectAnchor(new(1950, 20, 100, 60), 4000, 500) == Anchor9.Center,
      "edge midpoints are not candidates, including on an ultrawide screen"
    );
    Check(
      NativeViewerGrowth.SelectAnchor(new(950, 70, 100, 60), 4000, 500) == Anchor9.TopLeft,
      "five-point selection compares actual pixel distances rather than normalized axes"
    );
    Check(
      NativeViewerGrowth.SelectAnchor(new(430, 240, 100, 60), 1920, 1080) == Anchor9.Center,
      "equal corner/center distance favors symmetric center growth"
    );
    OverlayLayoutSettings moved = NativeViewerGrowth.Capture(new(1750, 20, 100, 60), 1920, 1080, .75f);
    Check(moved.Anchor == Anchor9.TopRight, "moving a viewer selects a new growth direction");
    OverlayLayoutSettings kept = NativeViewerGrowth.Capture(
      new(400, 200, 1200, 600),
      1920,
      1080,
      .75f,
      Anchor9.BottomLeft
    );
    Check(
      kept.Anchor == Anchor9.BottomLeft,
      "manual resize preserves the selected growth direction even across center"
    );
    Check(
      NativeViewerGrowth.Capture(new(400, 200, 100, 60), 1920, 1080, .75f, Anchor9.Left) == null,
      "edge midpoints cannot enter native growth persistence"
    );
    Check(
      NativeViewerGrowth.Capture(new(0, 0, 0, 60), 1920, 1080, 1) == null,
      "unavailable native geometry cannot initialize growth"
    );
  }

  private static void AnchorPersistence()
  {
    XmlSerializer serializer = new(typeof(OverlayLayoutSettings));
    using StringReader source = new(
      "<OverlayLayoutSettings><Id>fps</Id><Customized>true</Customized><CenterX>0.8</CenterX>"
        + "<CenterY>0.25</CenterY><Scale>1.7</Scale></OverlayLayoutSettings>"
    );
    OverlayLayoutSettings legacy = (OverlayLayoutSettings)serializer.Deserialize(source);
    Check(
      legacy.LayoutVersion == 0 && !OverlayLayoutPosition.IsAnchored(legacy),
      "missing version retains legacy format"
    );
    LayoutBounds before = OverlayLayoutPosition.Resolve(legacy, 200, 100, 1920, 1080);
    Near(1436, before.X, "legacy horizontal normalized center retains its original pixel placement");
    Near(220, before.Y, "legacy vertical normalized center retains its original pixel placement");
    OverlayLayoutPosition.Resolve(legacy, 350, 150, 2560, 1440);
    Check(
      legacy.LayoutVersion == 0 && legacy.CenterX == .8f && legacy.CenterY == .25f && legacy.Scale == 1.7f,
      "loading and resolving legacy placements do not migrate or mutate stored values"
    );

    AnchoredLayoutPosition capture = OverlayLayoutPosition.Capture(before, 1920, 1080);
    // The editor performs this write only when an explicit edit is completed.
    legacy.LayoutVersion = OverlayLayoutPosition.CurrentVersion;
    legacy.Anchor = capture.Anchor;
    legacy.OffsetX = capture.OffsetX;
    legacy.OffsetY = capture.OffsetY;
    LayoutBounds after = OverlayLayoutPosition.Resolve(legacy, 200, 100, 1920, 1080);
    Near(before.X, after.X, "explicit legacy migration is visually stationary horizontally");
    Near(before.Y, after.Y, "explicit legacy migration is visually stationary vertically");

    using StringWriter writer = new();
    serializer.Serialize(writer, legacy);
    using StringReader saved = new(writer.ToString());
    OverlayLayoutSettings restored = (OverlayLayoutSettings)serializer.Deserialize(saved);
    Check(
      restored.LayoutVersion == 1
        && restored.Anchor == capture.Anchor
        && restored.OffsetX == capture.OffsetX
        && restored.OffsetY == capture.OffsetY
        && restored.Scale == 1.7f,
      "anchor, logical offsets, format version, and scale survive XML persistence"
    );
    foreach (float migratedScale in new[] { .05f, 8.5f })
    {
      restored.Scale = migratedScale;
      using StringWriter scaledWriter = new();
      serializer.Serialize(scaledWriter, restored);
      using StringReader scaledReader = new(scaledWriter.ToString());
      OverlayLayoutSettings scaled = (OverlayLayoutSettings)serializer.Deserialize(scaledReader);
      LayoutBounds resolved = OverlayLayoutPosition.Resolve(scaled, 350, 140, 2560, 1440);
      Check(
        scaled.Scale == migratedScale && scaled.LayoutVersion == 1,
        "migrated native scales outside the old slider range survive storage and rendering unchanged"
      );
      Near(350, resolved.Width, "position resolution uses the current rendered size without applying scale twice");
    }
    restored.LayoutVersion = 99;
    Check(!OverlayLayoutPosition.IsAnchored(restored), "unknown future layout formats are not silently reinterpreted");
    OverlayLayoutPosition.Resolve(restored, 200, 100, 1920, 1080);
    Check(restored.LayoutVersion == 99, "unsupported format reads leave persisted metadata intact");
  }

  private static OverlayLayoutSettings Anchored(AnchoredLayoutPosition position) =>
    new()
    {
      Customized = true,
      LayoutVersion = OverlayLayoutPosition.CurrentVersion,
      Anchor = position.Anchor,
      OffsetX = position.OffsetX,
      OffsetY = position.OffsetY,
    };

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  private static void Hit(LayoutHandle expected, LayoutBounds bounds, float x, float y)
  {
    LayoutHandle actual = OverlayLayoutGeometry.HitTest(bounds, x, y);
    if (actual != expected)
      throw new InvalidOperationException("Expected " + expected + " at pointer, got " + actual);
  }

  private static void Visible(LayoutBounds value, float width, float height)
  {
    float visibleWidth = Math.Min(width, value.Right) - Math.Max(0, value.X);
    float visibleHeight = Math.Min(height, value.Bottom) - Math.Max(0, value.Y);
    if (visibleWidth + .002f < value.Width * .1f || visibleHeight + .002f < value.Height * .1f)
      throw new InvalidOperationException("Resize must keep at least 10 percent of both axes visible");
  }

  private static void Near(float expected, float actual, string message)
  {
    if (Math.Abs(expected - actual) > 0.001f)
      throw new InvalidOperationException(message + ": expected " + expected + ", got " + actual);
  }
}
