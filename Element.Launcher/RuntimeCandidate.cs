using System.IO;
using Element.Updates;
using Newtonsoft.Json.Linq;

namespace Element.Launcher;

/// <summary>One validated version folder and how to start it.</summary>
internal sealed class RuntimeCandidate
{
  private RuntimeCandidate(string version, string path, string assembly, string entryMethod)
  {
    Version = version;
    RuntimePath = path;
    AssemblyPath = Path.Combine(path, assembly);
    EntryMethod = entryMethod;
  }

  public string Version { get; }
  public string RuntimePath { get; }
  public string AssemblyPath { get; }
  public string EntryMethod { get; }
  public string UpdaterPath => Path.Combine(RuntimePath, ModLayout.UpdaterAssembly);
  public string LauncherPath => Path.Combine(RuntimePath, ModLayout.LauncherAssembly);
  public bool IsDevelopment => File.Exists(Path.Combine(RuntimePath, ModLayout.DevelopmentMarker));

  /// <summary>Validates the folder and reads its own Info.json entry point.</summary>
  public static RuntimeCandidate Open(string path, string version)
  {
    RuntimeFiles.Validate(path, version);
    JObject info = JObject.Parse(File.ReadAllText(Path.Combine(path, ModLayout.InfoFile)));
    string assembly = info.Value<string>("AssemblyName") ?? ModLayout.PayloadAssembly;
    string entryMethod = info.Value<string>("EntryMethod") ?? ModLayout.PayloadEntry;
    if (assembly != Path.GetFileName(assembly) || !File.Exists(Path.Combine(path, assembly)))
      throw new InvalidDataException("The runtime Info.json names an invalid assembly: " + assembly);
    return new RuntimeCandidate(SemanticVersion.Normalize(version), path, assembly, entryMethod);
  }
}
