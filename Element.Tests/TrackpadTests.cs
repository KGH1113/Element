using System;
using System.Runtime.InteropServices;
using Element.Infrastructure.Game.Editor;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests;

internal static class TrackpadTests
{
  [Suite("trackpad gesture ownership, zoom and native ABI")]
  public static void Run()
  {
    var canvas = new TrackpadGesture();
    Check(canvas.Route(1, 0, () => true), "canvas gesture must start");
    Check(canvas.Route(4, 0, () => false), "crossing UI must retain canvas ownership");
    Check(canvas.Route(8, 0, () => false), "finger-up must retain ownership for momentum");
    Check(canvas.Route(0, 1, () => false), "momentum must retain canvas ownership");
    Check(canvas.Route(0, 4, () => false), "momentum changes must retain ownership");
    Check(!canvas.Route(1, 0, () => false), "next gesture can start on UI");
    Check(!canvas.Route(4, 0, () => true), "UI gesture must not become camera movement");
    Check(!canvas.Route(0, 1, () => true), "UI momentum must not become camera movement");
    Check(canvas.Route(1, 0, () => true), "next canvas gesture must start");
    canvas.Cancel();
    Check(canvas.BlocksUi, "cancelled canvas momentum must not leak into UI scrolling");
    Check(!canvas.Route(0, 4, () => true), "cancelled momentum must stay cancelled");
    Check(!canvas.Route(4, 0, () => true), "cancelled gesture must not restart on changed");
    Check(canvas.Route(1, 0, () => true), "fresh begin must recover from cancellation");
    Check(!canvas.Route(16, 0, () => true), "OS cancellation must stop navigation");
    Check(!canvas.Route(1, 0, () => false) && !canvas.BlocksUi, "new UI gesture must recover ordinary scrolling");
    Check(!new TrackpadGesture().Route(0, 4, () => true), "orphan momentum must not acquire canvas");

    Equal(1, TrackpadGesture.ZoomSize(2, 1), 1e-9);
    Equal(4, TrackpadGesture.ZoomSize(2, -0.5), 1e-9);
    Equal(2, TrackpadGesture.ZoomSize(2, 0), 1e-9);
    double zoom = 2;
    for (int i = 0; i < 100; i++)
      zoom = TrackpadGesture.ZoomSize(zoom, 0.001);
    Equal(2 / Math.Pow(1.001, 100), zoom, 1e-9); // Tiny events must accumulate, never become wheel steps.
    Equal(0.5, TrackpadGesture.ZoomSize(0.5, 1), 1e-9);
    Equal(1, TrackpadGesture.ZoomSize(0.5, -0.5), 1e-9); // Reverse immediately at a limit.
    Equal(15, TrackpadGesture.ZoomSize(14, -0.9), 1e-9);
    Equal(2, TrackpadGesture.ZoomSize(2, double.NaN), 1e-9);
    Equal(2, TrackpadGesture.ZoomSize(2, double.PositiveInfinity), 1e-9);
    Equal(2, TrackpadGesture.ZoomSize(2, -1), 1e-9);
    Check(Marshal.SizeOf<TrackpadEvent>() == 56, "native event stride must match C ABI");
    Check(Marshal.OffsetOf<TrackpadEvent>(nameof(TrackpadEvent.X)).ToInt32() == 16, "native doubles must start at 16");
  }
}
