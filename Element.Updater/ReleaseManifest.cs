using System;
using System.IO;
using Element.Updates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Updater;

/// <summary>
/// Element.update.json, published with every release:
/// <code>
/// { "schemaVersion": 1, "version": "0.3.0",
///   "packages": { "macos":   { "asset": "Element-macos.zip", "bytes": 1, "sha256": "…",
///                              "runtimePath": "Element/Runtime/versions/0.3.0" },
///                 "windows": { … } } }
/// </code>
/// </summary>
internal sealed class ReleaseManifest
{
  public const int SchemaVersion = 1;

  public string Version { get; private set; }

  private JObject _packages;

  public static ReleaseManifest Parse(string json)
  {
    JObject root;
    try
    {
      root = JObject.Parse(json);
    }
    catch (JsonException exception)
    {
      throw new InvalidDataException("The update manifest is not valid JSON.", exception);
    }
    if (root.Value<int?>("schemaVersion") != SchemaVersion)
      throw new InvalidDataException("The update manifest schema is not supported.");
    string version = root.Value<string>("version");
    if (!SemanticVersion.TryParse(version, out SemanticVersion parsed) || parsed.ToString() != version)
      throw new InvalidDataException("The update manifest version is invalid: " + version);
    return new ReleaseManifest { Version = version, _packages = root["packages"] as JObject };
  }

  /// <summary>The package for <paramref name="platform"/> (macos or windows), validated.</summary>
  public ReleasePackage PackageFor(string platform)
  {
    if (_packages?[platform] is not JObject entry)
      throw new InvalidDataException("The release has no " + platform + " package.");
    ReleasePackage package = new()
    {
      Asset = entry.Value<string>("asset"),
      Bytes = entry.Value<long?>("bytes") ?? 0,
      Sha256 = entry.Value<string>("sha256"),
      RuntimePath = entry.Value<string>("runtimePath"),
    };
    if (package.Asset != ModLayout.ModId + "-" + platform + ".zip")
      throw new InvalidDataException("The update manifest names an unexpected package: " + package.Asset);
    if (package.Bytes <= 0 || package.Bytes > UpdateManager.MaximumPackageBytes)
      throw new InvalidDataException("The update manifest package size is invalid.");
    if (!IsSha256(package.Sha256))
      throw new InvalidDataException("The update manifest checksum is invalid.");
    if (package.RuntimePath != ModLayout.ModId + "/Runtime/versions/" + Version)
      throw new InvalidDataException("The update manifest runtime path is invalid: " + package.RuntimePath);
    return package;
  }

  private static bool IsSha256(string value)
  {
    if (value == null || value.Length != 64)
      return false;
    foreach (char character in value)
    {
      if (!Uri.IsHexDigit(character))
        return false;
    }
    return true;
  }
}

internal sealed class ReleasePackage
{
  public string Asset { get; set; }
  public long Bytes { get; set; }
  public string Sha256 { get; set; }
  public string RuntimePath { get; set; }

  public bool SameAs(ReleasePackage other) =>
    other != null
    && Asset == other.Asset
    && Bytes == other.Bytes
    && string.Equals(Sha256, other.Sha256, StringComparison.OrdinalIgnoreCase);
}
