using System;

namespace ImplResourcePack.Updater;

/// <summary>
/// Where releases come from. The manifest is read from the latest release; the
/// package from that version's own tag, so publishing a newer release while a
/// download runs cannot mix two versions.
/// </summary>
internal sealed class UpdateSource
{
  public const string ManifestAsset = "ImplResourcePack.update.json";
  public static readonly UpdateSource GitHub = new(
    new Uri("https://github.com/KGH1113/ImplResourcePack/releases/"),
    local: false
  );

  private readonly Uri _releases;
  private readonly bool _local;

  private UpdateSource(Uri releases, bool local)
  {
    _releases = releases;
    _local = local;
  }

  /// <summary>A loopback test server laid out like GitHub's release URLs.</summary>
  public static UpdateSource Local(Uri releases) => new(releases, local: true);

  public Uri Manifest => new(_releases, "latest/download/" + ManifestAsset);

  public Uri Package(string version, string asset) => new(_releases, "download/v" + version + "/" + asset);

  /// <summary>
  /// GitHub serves assets from github.com through a redirect to its content
  /// hosts; anything else after the redirects is refused.
  /// </summary>
  public bool Trusts(Uri response)
  {
    if (response == null)
      return false;
    if (_local)
      return response.IsLoopback && response.Scheme == Uri.UriSchemeHttp;
    return response.Scheme == Uri.UriSchemeHttps
      && (
        string.Equals(response.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        || response.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)
      );
  }
}
