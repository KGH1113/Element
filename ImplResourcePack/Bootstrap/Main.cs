using System;
using ImplResourcePack.Bootstrap;
using ImplResourcePack.Configuration;
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

    bool changed = false;

    GUILayout.Label("Judgment visuals");
    bool hidePerfect = GUILayout.Toggle(Instance.Settings.HidePerfectJudgmentText, "Hide Perfect judgment text");
    if (hidePerfect != Instance.Settings.HidePerfectJudgmentText)
    {
      Instance.Settings.HidePerfectJudgmentText = hidePerfect;
      changed = true;
    }

    GUILayout.Space(12f);
    GUILayout.Label("Recording");
    bool recordMode = GUILayout.Toggle(Instance.Settings.RecordMode, "Record mode (hide game and ImplResourcePack UI)");
    if (recordMode != Instance.Settings.RecordMode)
    {
      Instance.Settings.RecordMode = recordMode;
      changed = true;
    }

    GUILayout.Space(12f);
    GUILayout.Label("ImplDmNote");
    if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
    {
      bool showDmNote = GUILayout.Toggle(Instance.Settings.ShowDmNoteInGame, "Show hand and foot viewers in game");
      if (showDmNote != Instance.Settings.ShowDmNoteInGame)
      {
        Instance.Settings.ShowDmNoteInGame = showDmNote;
        changed = true;
      }
      if (showDmNote)
      {
        GUILayout.Label("Enable OBS mode in ImplDmNote.");
        if (
          GUILayout.Button(
            Instance.Runtime?.NativeViewerPreview == true ? "Hide viewer preview" : "Show viewer preview"
          )
        )
          Instance.Runtime?.ToggleNativeViewerPreview();
        GUILayout.Label(
          Instance.Runtime?.NativeViewerStatus ?? "Native viewers are unavailable while the mod is disabled."
        );
      }

      GUILayout.Space(12f);
      GUILayout.Label("Level editor");
      Instance.Settings.NativeEditorTrackpad = GUILayout.Toggle(
        Instance.Settings.NativeEditorTrackpad,
        "Native trackpad (two-finger pan / pinch zoom)"
      );
    }
    else
      GUILayout.Label("In-game viewers currently require macOS 11 or newer.");

    if (changed)
      Instance.Runtime?.ApplySettings();
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
