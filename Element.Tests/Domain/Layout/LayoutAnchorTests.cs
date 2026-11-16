using System;
using System.IO;
using System.Xml.Serialization;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Layout;

/// <summary>
/// Elements keep their place from the nearest anchor, on any screen, through
/// saving and on the canvas.
/// </summary>
internal static class LayoutAnchorTests
{
  internal static void Run()
  {
    AnchoredPositions();
  }

  private static void AnchoredPositions()
  {
    Equal(1, OverlayLayoutPosition.ScreenScale(1920, 1080), 0.001f, "reference viewport has unit scale");
    Equal(2, OverlayLayoutPosition.ScreenScale(3840, 2160), 0.001f, "uniform viewport growth scales both axes equally");
    Equal(
      (float)Math.Sqrt(4d / 3),
      OverlayLayoutPosition.ScreenScale(2560, 1080),
      0.001f,
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
      Equal(source.X, restored.X, 0.001f, anchor + " captures and restores without horizontal displacement");
      Equal(source.Y, restored.Y, 0.001f, anchor + " captures and restores without vertical displacement");

      foreach (var viewport in new[] { (1280f, 720f), (2560f, 1080f), (1080f, 1920f), (800f, 600f) })
      {
        float scale = OverlayLayoutPosition.ScreenScale(viewport.Item1, viewport.Item2);
        restored = OverlayLayoutPosition.Resolve(settings, 200 * scale, 100 * scale, viewport.Item1, viewport.Item2);
        Equal(
          captured.OffsetX,
          (restored.X + ax * restored.Width - ax * viewport.Item1) / scale,
          0.001f,
          anchor + " retains the horizontal logical margin across aspect ratios"
        );
        Equal(
          captured.OffsetY,
          (restored.Y + ay * restored.Height - ay * viewport.Item2) / scale,
          0.001f,
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
    Equal(-24, edge.OffsetX, 0.001f, "right anchor stores a signed 24px inward margin");
    Equal(-24, edge.OffsetY, 0.001f, "bottom anchor stores a signed 24px inward margin");
    LayoutBounds larger = OverlayLayoutPosition.Resolve(edge, 400, 200, 3840, 2160);
    Equal(48, 3840 - larger.Right, 0.001f, "right margin scales with the shared viewport scale");
    Equal(48, 2160 - larger.Bottom, 0.001f, "bottom margin scales with the same viewport scale");
    LayoutBounds widerContent = OverlayLayoutPosition.Resolve(edge, 700, 80, 1920, 1080);
    Equal(
      bottomRight.Right,
      widerContent.Right,
      0.001f,
      "restoration applies current content width before right alignment"
    );
    Equal(
      bottomRight.Bottom,
      widerContent.Bottom,
      0.001f,
      "restoration applies current content height before bottom alignment"
    );

    LayoutBounds movedAcrossCenter = new(80, 60, 300, 120);
    AnchoredLayoutPosition resize = OverlayLayoutPosition.Capture(movedAcrossCenter, 1920, 1080, edge.Anchor);
    Check(resize.Anchor == Anchor9.BottomRight, "explicit resize capture retains the saved anchor");
    Check(
      OverlayLayoutPosition.Capture(movedAcrossCenter, 1920, 1080).Anchor == Anchor9.TopLeft,
      "completed move capture selects its new nearest anchor"
    );

    OverlayLayoutSettings offscreen = Anchored(OverlayLayoutPosition.Capture(new(-180, -90, 200, 100), 1920, 1080));
    LayoutBounds raw = OverlayLayoutPosition.Resolve(offscreen, 400, 200, 3840, 2160);
    Equal(-360, raw.X, 0.001f, "signed offsets preserve deliberate horizontal offscreen placement");
    Equal(-180, raw.Y, 0.001f, "signed offsets preserve deliberate vertical offscreen placement");
    float savedOffsetX = offscreen.OffsetX;
    float savedOffsetY = offscreen.OffsetY;
    OverlayLayoutGeometry.KeepVisible(OverlayLayoutPosition.Resolve(offscreen, 40, 20, 400, 300), 400, 300);
    Equal(
      savedOffsetX,
      offscreen.OffsetX,
      0.001f,
      "display-only visibility fitting never rewrites horizontal persistence"
    );
    Equal(
      savedOffsetY,
      offscreen.OffsetY,
      0.001f,
      "display-only visibility fitting never rewrites vertical persistence"
    );
    Check(
      !OverlayLayoutPosition.Capture(new(0, 0, 0, 100), 1920, 1080).IsValid
        && !OverlayLayoutPosition.Capture(new(float.NaN, 0, 100, 100), 1920, 1080).IsValid
        && !OverlayLayoutPosition.Capture(new(0, 0, 100, 100), 0, 1080).IsValid
        && !OverlayLayoutPosition.Capture(new(0, 0, 100, 100), 1920, 1080, (Anchor9)99).IsValid,
      "unavailable geometry cannot produce a persisted capture"
    );
    AnchorPersistence();
    CanvasPlacement();
  }

  // OrbitRender draws the overlay canvas into its video, whose aspect can differ
  // from the game window's. A 3600x2260 window is about 1817x1141 canvas units;
  // a 16:9 video is 1920x1080 at any resolution.
  private static void CanvasPlacement()
  {
    float scale = OverlayLayoutPosition.ScreenScale(3600, 2260);
    float windowWidth = 3600 / scale;
    float windowHeight = 2260 / scale;
    Equal(1, OverlayLayoutPosition.ScreenScale(windowWidth, windowHeight), 0.001f, "canvas units are reference pixels");

    OverlayLayoutSettings bottom = Anchored(new AnchoredLayoutPosition(Anchor9.Bottom, 0, -4.1875f));
    LayoutBounds pixels = OverlayLayoutPosition.Resolve(bottom, 300 * scale, 30 * scale, 3600, 2260);
    LayoutBounds window = OverlayLayoutPosition.ResolveInCanvas(bottom, 300, 30, windowWidth, windowHeight);
    Equal(pixels.X / scale, window.X, 0.001f, "canvas units place an element where the window's pixels do, across");
    Equal(pixels.Y / scale, window.Y, 0.001f, "canvas units place an element where the window's pixels do, down");

    // Kept at its distance from its anchor, the element lands in the video where
    // resolving for the video puts it: at the bottom, not below the frame.
    (float x, float y) = OverlayLayoutPosition.AnchorPoint(bottom);
    Equal(.5f, x, 0.001f, "a bottom anchor is centered across");
    Equal(1, y, 0.001f, "a bottom anchor is at the bottom");
    LayoutBounds video = OverlayLayoutPosition.ResolveInCanvas(bottom, 300, 30, 1920, 1080);
    Equal(
      video.X,
      x * 1920 + window.X - x * windowWidth,
      0.001f,
      "the pinned element matches the video's layout across"
    );
    Equal(
      video.Y,
      y * 1080 + window.Y - y * windowHeight,
      0.001f,
      "the pinned element matches the video's layout down"
    );
    Equal(1080 - 4.1875f, video.Bottom, 0.001f, "an element at the window's bottom stays at the video's bottom");
    float fromCenter = window.Y - windowHeight * .5f;
    Check(1080 * .5f + fromCenter + 30 > 1080, "measured from the center, as before, it fell below the video");

    OverlayLayoutSettings top = Anchored(new AnchoredLayoutPosition(Anchor9.Top, 0, 43.875f));
    Equal(
      43.875f,
      OverlayLayoutPosition.ResolveInCanvas(top, 400, 40, 1920, 1080).Y,
      0.001f,
      "a title under the top edge keeps its margin in the video"
    );

    OverlayLayoutSettings legacy = new()
    {
      Customized = true,
      CenterX = .25f,
      CenterY = .9f,
    };
    (x, y) = OverlayLayoutPosition.AnchorPoint(legacy);
    Equal(.25f, x, 0.001f, "a legacy placement keeps its saved center across");
    Equal(.9f, y, 0.001f, "a legacy placement keeps its saved center down");
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
    Equal(1436, before.X, 0.001f, "legacy horizontal normalized center retains its original pixel placement");
    Equal(220, before.Y, 0.001f, "legacy vertical normalized center retains its original pixel placement");
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
    Equal(before.X, after.X, 0.001f, "explicit legacy migration is visually stationary horizontally");
    Equal(before.Y, after.Y, 0.001f, "explicit legacy migration is visually stationary vertically");

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
      Equal(
        350,
        resolved.Width,
        0.001f,
        "position resolution uses the current rendered size without applying scale twice"
      );
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
}
