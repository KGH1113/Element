using System.IO;
using Element.Updates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Launcher;

/// <summary>
/// Points the root Info.json at a version's own launcher, so UMM starts that
/// launcher next time. The running launcher's file is never replaced.
/// </summary>
internal static class LauncherInfo
{
  public static void Commit(string install, RuntimeCandidate runtime)
  {
    string assemblyName = ModLayout.LauncherAssemblyName(runtime.Version);
    AtomicFile.Copy(runtime.LauncherPath, Path.Combine(install, assemblyName));
    string infoPath = Path.Combine(install, ModLayout.InfoFile);
    JObject info = File.Exists(infoPath) ? JObject.Parse(File.ReadAllText(infoPath)) : new JObject();
    // The runtime's Info.json carries the mod's identity; only the entry differs.
    JObject runtimeInfo = JObject.Parse(File.ReadAllText(Path.Combine(runtime.RuntimePath, ModLayout.InfoFile)));
    foreach (JProperty property in runtimeInfo.Properties())
      info[property.Name] = property.Value;
    info["Version"] = runtime.Version;
    info["AssemblyName"] = assemblyName;
    info["EntryMethod"] = ModLayout.LauncherEntry;
    AtomicFile.WriteAllText(infoPath, info.ToString(Formatting.Indented) + "\n");
  }
}
