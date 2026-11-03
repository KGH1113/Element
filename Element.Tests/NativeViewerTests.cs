using System;
using System.Collections.Generic;
using Element.Configuration;
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
    ViewersFollowTheProfile();
  }

  // Switching profiles: the old profile's viewers leave the screen and stay
  // out of the new profile's list; the new profile's viewers come.
  private static void ViewersFollowTheProfile()
  {
    string[] gallery = { "dmnote-hand", "dmnote-foot" };
    var current = new List<string>(gallery) { "native-hand:old", "native-foot:kept" };
    var layouts = new List<OverlayLayoutSettings>
    {
      new() { Id = "text", TemplateId = OverlayElementKinds.Text },
      new() { Id = "native-foot:kept", TemplateId = OverlayElementKinds.FootKeyViewer },
      new() { Id = "native-hand:new", TemplateId = OverlayElementKinds.HandKeyViewer },
      null,
    };
    var removed = new List<string>();
    var added = new List<KeyValuePair<string, string>>();
    NativeViewerLayoutSync.Plan(current, gallery, layouts, removed, added);
    Check(
      removed.Count == 1 && removed[0] == "native-hand:old",
      "A viewer the new profile lacks must leave, and the gallery viewers stay."
    );
    Check(
      added.Count == 1 && added[0].Key == "native-hand:new" && added[0].Value == "hand",
      "A viewer only the new profile has must come, with its kind."
    );
    Check(layouts.Count == 4, "Following a profile must not add other viewers to its list.");

    removed.Clear();
    added.Clear();
    NativeViewerLayoutSync.Plan(new[] { "native-foot:kept", "native-hand:new" }, gallery, layouts, removed, added);
    Check(removed.Count == 0 && added.Count == 0, "Viewers already following the list change nothing.");
    Check(
      NativeViewerLayoutSync.Kind(OverlayElementKinds.Text) == null
        && NativeViewerLayoutSync.Kind(OverlayElementKinds.FootKeyViewer) == "foot",
      "Only key viewer templates are viewers."
    );
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
