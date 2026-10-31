using System;
using Element.Domain.DmNote;

namespace Element.Tests;

internal static class NativeViewerTests
{
  public static void Run()
  {
    var surface = new NativeViewerSurface
    {
      ViewerKind = "hand",
      Url = "http://127.0.0.1:34891/?token=fixture&viewer=hand",
      Width = 860,
      Height = 320,
    };
    Check(surface.IsValid(), "The loopback OBS viewer must be accepted.");
    foreach (
      string address in new[]
      {
        "https://127.0.0.1:34891/",
        "http://127.0.0.1.example.com/",
        "http://example.com/",
        "http://127.0.0.2/",
        "file:///etc/passwd",
        "javascript:alert(1)",
        "http://user:password@127.0.0.1/",
        "http://127.0.0.1/#fragment",
        null,
      }
    )
    {
      surface.Url = address;
      Check(!surface.IsValid(), "The native host must reject non-OBS destinations.");
    }
    surface.Url = "http://127.0.0.1:34891/?token=fixture&viewer=foot";
    surface.ViewerKind = "foot";
    Check(surface.IsValid(), "The foot viewer must also be accepted.");
    surface.ViewerKind = "main";
    Check(!surface.IsValid(), "Unknown viewer kinds cannot create native views.");
    surface.ViewerKind = "foot";
    surface.Width = 0;
    Check(!surface.IsValid(), "A zero viewport is invalid.");
    surface.Width = 4097;
    Check(!surface.IsValid(), "Oversized viewports cannot allocate native views.");
    surface.Width = 860;
    surface.Height = -1;
    Check(!surface.IsValid(), "Negative height is invalid.");
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
