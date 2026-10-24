using System;
using ImplResourcePack.Bootstrap;
using ImplResourcePack.Configuration;
using ImplResourcePack.Presentation.Settings;
using UnityEngine;
using UnityModManagerNet;

namespace ImplResourcePack;

public sealed class Main
{
  public static Main Instance { get; private set; }

  public UnityModManager.ModEntry ModEntry { get; }
  public string Path => ModEntry.Path;
  public string Version => ModEntry.Info.Version;

  internal ImplResourcePackSettings Settings { get; private set; }

  internal ModRuntime Runtime { get; private set; }

  private bool _enabled;

  private Main(UnityModManager.ModEntry modEntry)
  {
    ModEntry = modEntry;
  }

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    try
    {
      Instance = new Main(modEntry);
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
