using System;

namespace Element.Domain.Layout;

/// <summary>Ids for elements added in the editor: a kind's prefix and a fresh GUID, e.g. "text:3f2a…".</summary>
internal static class OverlayElementIds
{
  /// <summary>The prefix of elements other mods draw; their kind is in the template id.</summary>
  public const string ModPrefix = "mod";

  public static string New(string prefix) => prefix + ":" + Guid.NewGuid().ToString("N");
}
