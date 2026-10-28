using System.IO;

namespace ImplResourcePack.Updates;

/// <summary>
/// Where an installed mod keeps each part. User data (Settings.xml,
/// UpdateSettings.json) stays at the mod root; every version runs from its own
/// folder, so a running version is never overwritten.
/// <code>
/// Mods/ImplResourcePack/
///   Info.json                        UMM entry: the launcher of the current version
///   AdofaiIpc.*.dll, ipc/            shared IPC bundle other mods discover here
///   Runtime/state.json               Current / Previous / Trial / Failed
///   Runtime/launchers/&lt;v&gt;/          ImplResourcePack.Launcher.dll
///   Runtime/versions/&lt;v&gt;/           the mod, its updater, native libraries, fonts, ImplDmNote
/// </code>
/// </summary>
internal static class ModLayout
{
  public const string ModId = "ImplResourcePack";
  public const string PayloadAssembly = "ImplResourcePack.dll";
  public const string PayloadEntry = "ImplResourcePack.Main.Load";
  public const string LauncherAssembly = "ImplResourcePack.Launcher.dll";
  public const string LauncherEntry = "ImplResourcePack.Launcher.Entry.Load";
  public const string UpdaterAssembly = "ImplResourcePack.Updater.dll";
  public const string InfoFile = "Info.json";
  /// <summary>Marks a version installed by ./scripts/run.sh install; it never updates itself.</summary>
  public const string DevelopmentMarker = ".development";
  /// <summary>Written next to a downloaded version: the package it came from.</summary>
  public const string InstalledManifest = ".update.json";
  public const string PreferencesFile = "UpdateSettings.json";

  public static readonly string[] IpcBundle =
  {
    "AdofaiIpc.Contracts.dll",
    "AdofaiIpc.Loader.dll",
    "ipc/AdofaiIpc.Runtime.dll",
    "ipc/manifest.json",
  };

  public static string RuntimeRoot(string install) => Path.Combine(install, "Runtime");

  public static string StatePath(string install) => Path.Combine(RuntimeRoot(install), "state.json");

  public static string VersionsRoot(string install) => Path.Combine(RuntimeRoot(install), "versions");

  public static string VersionPath(string install, string version) =>
    Path.Combine(VersionsRoot(install), SemanticVersion.Normalize(version));

  public static string LaunchersRoot(string install) => Path.Combine(RuntimeRoot(install), "launchers");

  /// <summary>The Info.json AssemblyName of a version's launcher; UMM resolves it from the mod root.</summary>
  public static string LauncherAssemblyName(string version) =>
    "Runtime/launchers/" + SemanticVersion.Normalize(version) + "/" + LauncherAssembly;
}
