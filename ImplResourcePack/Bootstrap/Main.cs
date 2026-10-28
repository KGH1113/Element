using System;
using ImplResourcePack.Application.Updates;
using ImplResourcePack.Bootstrap;
using ImplResourcePack.Configuration;
using ImplResourcePack.Infrastructure.Ipc;
using ImplResourcePack.Infrastructure.Updates;
using ImplResourcePack.Presentation.Settings;
using UnityEngine;
using UnityModManagerNet;

namespace ImplResourcePack;

public sealed class Main
{
  public static Main Instance { get; private set; }

  public UnityModManager.ModEntry ModEntry { get; }

  /// <summary>The mod root: UMM settings, UpdateSettings.json and the shared IPC bundle.</summary>
  public string Path => ModEntry.Path;

  /// <summary>
  /// This version's folder (Runtime/versions/&lt;v&gt;): native libraries, fonts and
  /// the bundled ImplDmNote. The mod root itself when started without the launcher.
  /// </summary>
  public string RuntimePath { get; }

  public string Version => ModEntry.Info.Version;

  internal IModUpdates Updates { get; private set; }

  internal ImplResourcePackSettings Settings { get; private set; }

  internal ModRuntime Runtime { get; private set; }

  private bool _enabled;

  private Main(UnityModManager.ModEntry modEntry)
  {
    ModEntry = modEntry;
    // UMM may load an assembly from memory, which leaves no location.
    string location = typeof(Main).Assembly.Location;
    RuntimePath = string.IsNullOrEmpty(location) ? modEntry.Path : System.IO.Path.GetDirectoryName(location);
  }

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    try
    {
      Instance = new Main(modEntry);
      // Other mods' IPC loaders look for this mod's runtime at the mod root.
      BundledIpcFiles.PublishToModRoot(Instance.RuntimePath, modEntry.Path, Instance.LogWarning);
      Instance.Updates = new LauncherModUpdates(modEntry.Path, Instance.LogException);
      Instance.Settings =
        UnityModManager.ModSettings.Load<ImplResourcePackSettings>(modEntry) ?? new ImplResourcePackSettings();
      Instance.Settings.Migrate();
      Instance.Settings.OverlayLayouts ??= new();
      modEntry.OnToggle = OnToggle;
      modEntry.OnGUI = OnGUI;
      modEntry.OnSaveGUI = OnSaveGUI;
      modEntry.OnUnload = OnUnload;
      modEntry.OnUpdate = (_, _) => Instance?.Runtime?.TickTrackpad();
      Instance.Enable();
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
  }

  public void Log(string message)
  {
    ModEntry.Logger.Log(message);
  }

  public void LogException(string context, Exception exception)
  {
    ModEntry.Logger.Error("[" + context + "] " + exception);
  }

  public void LogWarning(string message)
  {
    ModEntry.Logger.Warning(message);
  }

  private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
  {
    try
    {
      if (value)
        Instance.Enable();
      else
      {
        Instance.Disable();
        Instance.Settings?.Save(modEntry);
      }
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
  }

  private static void OnGUI(UnityModManager.ModEntry modEntry)
  {
    if (Instance?.Settings == null)
      return;

    GUILayout.Label("ImplResourcePack · " + SettingsPanel.ShortcutLabel(Instance.Settings));
    GUILayout.Label(SettingsPanel.UpdateStatusText(Instance.Updates));
    bool wasEnabled = GUI.enabled;
    GUI.enabled = wasEnabled && Instance.Runtime != null;
    if (GUILayout.Button(RDString.language == SystemLanguage.Korean ? "설정 열기" : "Open settings"))
      Instance.Runtime?.OpenSettings();
    GUI.enabled = wasEnabled;
  }

  private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
  {
    Instance?.Settings?.Save(modEntry);
  }

  private static bool OnUnload(UnityModManager.ModEntry modEntry)
  {
    try
    {
      Instance?.Settings?.Save(modEntry);
      Instance?.Disable();
      Instance = null;
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
  }

  private void Enable()
  {
    if (_enabled)
      return;

    try
    {
      Runtime = new ModRuntime(this);
      if (Runtime.Initialize())
      {
        if (Runtime.OverlaysAvailable)
          Log("Enabled with IPC key limiter and overlays.");
        else
          LogWarning("Enabled IPC key limiter and ImplDmNote viewers; font-based overlays are unavailable.");
      }
      else
      {
        Runtime.Dispose();
        Runtime = null;
        LogWarning("Enabled without overlays because their font could not be loaded.");
      }

      _enabled = true;
    }
    catch
    {
      Runtime?.Dispose();
      Runtime = null;
      throw;
    }
  }

  private void Disable()
  {
    if (!_enabled)
      return;

    _enabled = false;
    Runtime?.Dispose();
    Runtime = null;
    Log("Disabled.");
  }
}
