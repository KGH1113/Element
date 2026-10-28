using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImplResourcePack.Updates;

/// <summary>Checks that a version folder holds a complete, matching runtime.</summary>
internal static class RuntimeFiles
{
  private static readonly (string File, string Assembly)[] Assemblies =
  {
    (ModLayout.PayloadAssembly, "ImplResourcePack"),
    (ModLayout.LauncherAssembly, "ImplResourcePack.Launcher"),
    (ModLayout.UpdaterAssembly, "ImplResourcePack.Updater"),
  };

  /// <summary>Throws <see cref="InvalidDataException"/> unless <paramref name="directory"/> runs <paramref name="version"/>.</summary>
  public static void Validate(string directory, string version)
  {
    string info = Path.Combine(directory, ModLayout.InfoFile);
    if (!File.Exists(info))
      throw new InvalidDataException("The runtime has no Info.json: " + directory);
    string declared;
    try
    {
      declared = JObject.Parse(File.ReadAllText(info)).Value<string>("Version");
    }
    catch (JsonException exception)
    {
      throw new InvalidDataException("The runtime Info.json is unreadable: " + info, exception);
    }
    if (!SemanticVersion.Same(declared, version))
      throw new InvalidDataException("The runtime Info.json declares " + declared + ", expected " + version + ".");
    foreach (string file in ModLayout.IpcBundle)
    {
      if (!File.Exists(Path.Combine(directory, file)))
        throw new InvalidDataException("The runtime is missing " + file + ".");
    }
    foreach ((string file, string assembly) in Assemblies)
    {
      string path = Path.Combine(directory, file);
      if (!File.Exists(path))
        throw new InvalidDataException("The runtime is missing " + file + ".");
      string name;
      try
      {
        name = AssemblyName.GetAssemblyName(path).Name;
      }
      catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
      {
        throw new InvalidDataException("The runtime has an invalid " + file + ".", exception);
      }
      if (name != assembly)
        throw new InvalidDataException("The runtime " + file + " is " + name + ".");
    }
  }

  public static bool IsValid(string directory, string version)
  {
    try
    {
      Validate(directory, version);
      return true;
    }
    catch (InvalidDataException)
    {
      return false;
    }
    catch (IOException)
    {
      return false;
    }
  }
}
