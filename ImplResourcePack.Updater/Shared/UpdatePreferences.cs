using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImplResourcePack.Updates;

/// <summary>
/// UpdateSettings.json at the mod root. The launcher reads it before the mod
/// loads, so it lives outside the mod's UMM settings.
/// </summary>
internal sealed class UpdatePreferences
{
  public bool AutomaticUpdates { get; set; } = true;

  /// <summary>
  /// A local test server (http://127.0.0.1:port/) that stands in for GitHub
  /// Releases; anything else is ignored. See docs/auto-update.md.
  /// </summary>
  public string Source { get; set; }

  public static string PathFor(string install) => System.IO.Path.Combine(install, ModLayout.PreferencesFile);

  /// <summary>A missing, unreadable or hand-mistyped file means the defaults: updates on.</summary>
  public static UpdatePreferences Load(string install)
  {
    try
    {
      string path = PathFor(install);
      if (!File.Exists(path))
        return new UpdatePreferences();
      JObject root = JObject.Parse(File.ReadAllText(path));
      return new UpdatePreferences
      {
        AutomaticUpdates = root.Value<bool?>("AutomaticUpdates") ?? true,
        Source = root.Value<string>("Source"),
      };
    }
    catch (Exception)
    {
      // e.g. "AutomaticUpdates": "off" throws FormatException; never stop the mod over it.
      return new UpdatePreferences();
    }
  }

  public void Save(string install)
  {
    JObject root = new() { ["AutomaticUpdates"] = AutomaticUpdates };
    if (!string.IsNullOrEmpty(Source))
      root["Source"] = Source;
    AtomicFile.WriteAllText(PathFor(install), root.ToString(Formatting.Indented) + "\n");
  }

  /// <summary>The test server URL when <see cref="Source"/> names a loopback HTTP address.</summary>
  public Uri LocalSource()
  {
    if (
      !Uri.TryCreate(Source, UriKind.Absolute, out Uri uri)
      || uri.Scheme != Uri.UriSchemeHttp
      || !uri.IsLoopback
      || uri.Query.Length != 0
      || uri.Fragment.Length != 0
    )
      return null;
    return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");
  }
}
