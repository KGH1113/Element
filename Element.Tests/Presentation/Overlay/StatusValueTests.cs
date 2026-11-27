using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Status;
using Element.Presentation.Overlay.Values;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Presentation.Overlay;

// In an OrbitRender video the screen's frame rate says nothing about the video,
// so FPS shows "-" instead of a number that looks real.
internal static class StatusValueTests
{
  [Suite("an OrbitRender video shows FPS as -; progress and X-Accuracy are written the game's way")]
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

    // Progress and X-Accuracy are written as the game writes them, and kept whole for places that read more.
    status.Render(snapshot.WithMetrics(0, .996f, .99996f, UnityEngine.SystemLanguage.English));
    Check(Text(values, OverlayValueId.ProgressCurrent) == "99", "progress 99.6% shows 99, as the game floors it");
    Check(Text(values, OverlayValueId.Accuracy) == "99.99", "X-Accuracy 99.996% shows 99.99, never rounded up to 100");
    Check(
      values.TryGetNumber(OverlayValueId.ProgressCurrent, out double progress) && progress > 99.5 && progress < 99.7,
      "the number underneath is whole, for a place that reads decimals: " + progress
    );
    // Away from a fourth-decimal edge, so the floats' own noise can't cross it.
    status.Render(snapshot.WithMetrics(0, .9961234f, .99996f, UnityEngine.SystemLanguage.English));
    long stamp = values.Stamp;
    status.Render(snapshot.WithMetrics(0, .9961236f, .99996f, UnityEngine.SystemLanguage.English));
    Check(values.Stamp == stamp, "a change past the fourth decimal is no change: nothing redraws");
    status.Render(snapshot.WithMetrics(0, .997f, .99996f, UnityEngine.SystemLanguage.English));
    Check(
      values.Stamp > stamp && Text(values, OverlayValueId.ProgressCurrent) == "99",
      "a finer change is one, though it still shows 99"
    );
  }

  private static string Text(OverlayValues values, OverlayValueId id) =>
    values.TryGet(id, out StringBuilder text) ? text.ToString() : null;

  private static string Shown(OverlayValues values) =>
    values.TryGet(OverlayValueId.Fps, out StringBuilder text) ? text.ToString() : null;
}
