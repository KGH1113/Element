using System;
using System.Collections.Generic;
using Element.Application.Rendering;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Domain.Rendering;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Rendering;

/// <summary>Every viewer is laid over the video from its own page.</summary>
internal static class KeyViewerOverlayTests
{
  internal static void Run()
  {
    OverlaysEveryViewerOnItsPage();
  }

  // Two hand viewers share one page, drawn at the larger one's scale.
  private static void OverlaysEveryViewerOnItsPage()
  {
    var big = new LayoutBounds(20, 2880 - 600 - 40, 1600, 600);
    var small = new LayoutBounds(3000, 100, 800, 300);
    RenderViewerPlacement large = RenderViewerPlacement.Create(
      "hand",
      big,
      5120,
      2880,
      2,
      NativeViewerGrowth.Capture(big, 5120, 2880, 1, Anchor9.BottomLeft),
      860,
      320,
      1920,
      1080
    );
    RenderViewerPlacement smaller = RenderViewerPlacement.Create(
      "hand",
      small,
      5120,
      2880,
      1,
      NativeViewerGrowth.Capture(small, 5120, 2880, 1, Anchor9.TopRight),
      860,
      320,
      1920,
      1080
    );
    RenderViewerPlacement foot = RenderViewerPlacement.Create("foot", big, 5120, 2880, 2, null, 400, 300, 1920, 1080);
    var surfaces = new[]
    {
      new RenderSurface
      {
        ViewerKind = "hand",
        Width = 645,
        Height = 240,
        ViewportWidth = 860,
        ViewportHeight = 320,
        Padding = 30,
      },
      new RenderSurface
      {
        ViewerKind = "foot",
        Width = 300,
        Height = 225,
        ViewportWidth = 400,
        ViewportHeight = 300,
        Padding = 0,
      },
    };
    var videos = new[]
    {
      new RenderVideo
      {
        ViewerKind = "hand",
        Path = "/v/hand.mov",
        Width = 645,
        Height = 240,
        Frames = 10,
      },
      new RenderVideo
      {
        ViewerKind = "foot",
        Path = "/v/foot.mov",
        Width = 300,
        Height = 225,
        Frames = 10,
      },
    };
    IReadOnlyList<KeyViewerOverlay> overlays = KeyViewerRenderPlan.Overlays(
      new[] { large, smaller, foot },
      surfaces,
      videos
    );
    Check(overlays.Count == 3, "Every placed viewer goes over the video.");
    Check(
      overlays[0].Path == "/v/hand.mov" && overlays[1].Path == "/v/hand.mov" && overlays[2].Path == "/v/foot.mov",
      "Viewers of a kind share their page's video."
    );
    Check(overlays[0].Source.Equals(overlays[1].Source), "Both show the same part of the page.");
    Check(
      overlays[1].Target.Width < overlays[0].Target.Width && overlays[1].Target.Height < overlays[0].Target.Height,
      "The smaller viewer gets a smaller target: " + overlays[0].Target + " vs " + overlays[1].Target
    );
    Check(
      Math.Abs(overlays[1].Target.Width - overlays[0].Source.Width / 2) <= 1,
      "Half the scale, half the size: " + overlays[1].Target
    );
    Check(overlays[1].Target.X > 960, "Each viewer keeps its own place.");
    Check(
      KeyViewerRenderPlan.Overlays(new[] { large }, surfaces, Array.Empty<RenderVideo>()).Count == 0,
      "No video, nothing to lay over."
    );
  }
}
