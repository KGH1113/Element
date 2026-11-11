using System;
using System.IO;
using Element.Configuration;
using UnityModManagerNet;

namespace Element.Infrastructure.Storage;

/// <summary>The settings file Unity Mod Manager keeps for the mod.</summary>
internal static class SettingsFile
{
  /// <summary>
  /// Reads the saved settings. Unity Mod Manager's own loader drops text that
  /// is only spaces, such as the space between two values in an overlay text,
  /// so the same file is read here with whitespace kept.
  /// </summary>
  public static ElementSettings Load(UnityModManager.ModEntry modEntry)
  {
    ElementSettings fresh = new();
    string path = fresh.GetPath(modEntry);
    if (!File.Exists(path))
      return fresh;
    try
    {
      using FileStream stream = File.OpenRead(path);
      return SettingsXml.Read<ElementSettings>(stream) ?? fresh;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Warning("Settings were read without keeping spaces: " + exception.Message);
      return UnityModManager.ModSettings.Load<ElementSettings>(modEntry) ?? fresh;
    }
  }
}
