using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Element.Updates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Launcher;

/// <summary>Runtime/state.json and the version folders it refers to.</summary>
internal sealed class RuntimeStore
{
  // Files the mod kept at its root before versioned runtimes.
  private static readonly string[] LegacyFiles = { ModLayout.PayloadAssembly, "Element.pdb", "libElementTrackpad.dylib" };
  private static readonly string[] LegacyDirectories = { "native", "Assets", "ElementNote" };

  private readonly string _install;
  private readonly string _statePath;

  public RuntimeStore(string install)
  {
    _install = Path.GetFullPath(install);
    _statePath = ModLayout.StatePath(_install);
  }

  /// <summary>
  /// Reads the state and repairs what an interrupted launch left: a trial that
  /// never finished counts as failed, and a broken Current falls back to Previous.
  /// </summary>
  public RuntimeState LoadAndRepair()
  {
    string backup = _statePath + ".bak";
    if (!File.Exists(_statePath) && File.Exists(backup))
      File.Move(backup, _statePath);
    if (!File.Exists(_statePath))
      throw new InvalidDataException("Runtime/state.json is missing. Reinstall Element.");
    RuntimeState state;
    try
    {
      state = JsonConvert.DeserializeObject<RuntimeState>(File.ReadAllText(_statePath));
    }
    catch (JsonException exception)
    {
      throw new InvalidDataException("Runtime/state.json is unreadable. Reinstall Element.", exception);
    }
    if (state == null || state.SchemaVersion != RuntimeState.CurrentSchema || !SemanticVersion.TryParse(state.Current, out _))
      throw new InvalidDataException("Runtime/state.json is invalid. Reinstall Element.");
    state.Failed ??= new List<string>();

    if (!string.IsNullOrWhiteSpace(state.Trial))
    {
      string trial = state.Trial;
      state.Trial = null;
      MarkFailed(state, trial);
      Save(state);
      DeleteUnreferenced(trial, state);
    }
    if (!TryGet(state.Current, out _) && TryGet(state.Previous, out _))
    {
      state.Current = state.Previous;
      state.Previous = null;
      Save(state);
    }
    CleanupLegacyPayload();
    CleanupVersions(state);
    CleanupLaunchers(state);
    return state;
  }

  public RuntimeCandidate Get(string version) => RuntimeCandidate.Open(ModLayout.VersionPath(_install, version), version);

  /// <summary>Opens a folder the updater installed, which must be that version's folder.</summary>
  public RuntimeCandidate Open(string version, string runtimePath)
  {
    string expected = Path.GetFullPath(ModLayout.VersionPath(_install, version));
    if (!string.Equals(expected, Path.GetFullPath(runtimePath ?? ""), StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("The updater returned an unexpected runtime folder: " + runtimePath);
    return RuntimeCandidate.Open(expected, version);
  }

  public void Promote(RuntimeState state, string version)
  {
    string normalized = SemanticVersion.Normalize(version);
    if (!SemanticVersion.Same(state.Current, normalized))
      state.Previous = state.Current;
    state.Current = normalized;
    state.Trial = null;
    state.Failed.RemoveAll(item => SemanticVersion.Same(item, normalized));
    Save(state);
    CleanupVersions(state);
  }

  public static void MarkFailed(RuntimeState state, string version)
  {
    if (!SemanticVersion.TryParse(version, out SemanticVersion parsed))
      return;
    string normalized = parsed.ToString();
    state.Failed ??= new List<string>();
    if (!state.Failed.Any(item => SemanticVersion.Same(item, normalized)))
      state.Failed.Add(normalized);
    if (state.Failed.Count > RuntimeState.FailedLimit)
      state.Failed.RemoveRange(0, state.Failed.Count - RuntimeState.FailedLimit);
  }

  public void Save(RuntimeState state)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(_statePath));
    string temporary = _statePath + ".tmp";
    string backup = _statePath + ".bak";
    File.WriteAllText(temporary, JsonConvert.SerializeObject(state, Formatting.Indented) + "\n");
    if (File.Exists(_statePath))
    {
      if (File.Exists(backup))
        File.Delete(backup);
      File.Replace(temporary, _statePath, backup, true);
      TryDeleteFile(backup);
    }
    else
    {
      File.Move(temporary, _statePath);
    }
  }

  public void DeleteUnreferenced(string version, RuntimeState state)
  {
    if (!SemanticVersion.TryParse(version, out _) || Referenced(version, state))
      return;
    TryDeleteDirectory(ModLayout.VersionPath(_install, version));
  }

  private bool TryGet(string version, out RuntimeCandidate candidate)
  {
    candidate = null;
    if (!SemanticVersion.TryParse(version, out _))
      return false;
    try
    {
      candidate = Get(version);
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

  private static bool Referenced(string version, RuntimeState state) =>
    SemanticVersion.Same(version, state.Current)
    || SemanticVersion.Same(version, state.Previous)
    || SemanticVersion.Same(version, state.Trial);

  /// <summary>
  /// Keeps Current, Previous and the trial, plus a newer download waiting for
  /// the next launch. Everything else is an old version or a failed one.
  /// </summary>
  private void CleanupVersions(RuntimeState state)
  {
    string root = ModLayout.VersionsRoot(_install);
    if (!Directory.Exists(root))
      return;
    SemanticVersion current = SemanticVersion.Parse(state.Current);
    foreach (string directory in Directory.GetDirectories(root))
    {
      string name = Path.GetFileName(directory);
      if (Referenced(name, state))
        continue;
      bool waiting =
        SemanticVersion.TryParse(name, out SemanticVersion version)
        && version.ToString() == name
        && version.CompareTo(current) > 0
        && !state.Failed.Any(item => SemanticVersion.Same(item, name));
      if (!waiting)
        TryDeleteDirectory(directory);
    }
  }

  /// <summary>Keeps the launcher UMM started this time and the one for Current.</summary>
  private void CleanupLaunchers(RuntimeState state)
  {
    string root = ModLayout.LaunchersRoot(_install);
    if (!Directory.Exists(root))
      return;
    HashSet<string> keep = new(StringComparer.OrdinalIgnoreCase) { SemanticVersion.Normalize(state.Current) };
    string started = StartedLauncherVersion();
    if (started != null)
      keep.Add(started);
    foreach (string directory in Directory.GetDirectories(root))
    {
      if (!keep.Contains(Path.GetFileName(directory)))
        TryDeleteDirectory(directory);
    }
  }

  /// <summary>The version folder named by the root Info.json AssemblyName, e.g. Runtime/launchers/0.3.0/….</summary>
  private string StartedLauncherVersion()
  {
    try
    {
      string assembly = JObject.Parse(File.ReadAllText(Path.Combine(_install, ModLayout.InfoFile))).Value<string>("AssemblyName");
      string[] parts = (assembly ?? "").Replace('\\', '/').Split('/');
      return parts.Length == 4 && parts[0] == "Runtime" && parts[1] == "launchers" ? parts[2] : null;
    }
    catch (Exception exception) when (exception is IOException or JsonException)
    {
      return null;
    }
  }

  // Extracting a new package over an older install leaves its root payload behind.
  private void CleanupLegacyPayload()
  {
    foreach (string file in LegacyFiles)
      TryDeleteFile(Path.Combine(_install, file));
    // UMM's private copies of the DLL it used to load from the root.
    foreach (string cache in Directory.GetFiles(_install, ModLayout.PayloadAssembly + ".*.cache"))
      TryDeleteFile(cache);
    foreach (string directory in LegacyDirectories)
      TryDeleteDirectory(Path.Combine(_install, directory));
  }

  private static void TryDeleteFile(string path)
  {
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private static void TryDeleteDirectory(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, true);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }
}
