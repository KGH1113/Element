using System;
using System.Text;
using Element.Domain.Elements;
using Element.Domain.Status;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Status;

namespace Element.Tests;

// In an OrbitRender video the screen's frame rate says nothing about the video,
// so FPS shows "-" instead of a number that looks real.
internal static class StatusValueTests
{
  public static void Run()
  {
    OverlayValues values = new();
    StatusValues status = new(values);
    StatusSnapshot snapshot = default;

    status.Render(snapshot.WithFps(true, 144));
    Check(Shown(values) == "144", "a measured frame rate shows as a number");
    Check(values.TryGetNumber(OverlayValueId.Fps, out double number) && number == 144, "and calculates");

    status.Render(snapshot.WithFps(true, StatusSnapshot.UnknownFps));
    Check(Shown(values) == "-", "a video's frame rate shows as -");
    Check(!values.TryGetNumber(OverlayValueId.Fps, out _), "a formula with it shows nothing, not a wrong number");

    status.Render(snapshot.WithFps(true, 60));
    Check(Shown(values) == "60", "the number comes back after the render");
  }

  private static string Shown(OverlayValues values) =>
    values.TryGet(OverlayValueId.Fps, out StringBuilder text) ? text.ToString() : null;

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
