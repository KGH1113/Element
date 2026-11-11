using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace Element.Configuration;

[CopiedInPlace]
public sealed class ElementSettings : UnityModManager.ModSettings
{
  public bool HidePerfectJudgmentText = true;

  public bool RecordMode;

  public bool NativeEditorTrackpad = true;

  public bool ShowDmNoteInGame = true;

  /// <summary>Open the bundled ElementNote with the game; it closes when the game quits.</summary>
  public bool AutoOpenCloseDmNote = true;

  /// <summary>The version SettingsMigrator brings settings to. Raise it with each new migration step.</summary>
  public const int CurrentSettingsVersion = 9;

  public const string DefaultHighlightColor = "#BA712B";

  public int SettingsVersion;
  public string HighlightColor = DefaultHighlightColor;
  public List<string> RecentHighlightColors = new();
  public bool ReduceUiMotion;
  public bool LayoutSnapping = true;

  /// <summary>The overlay texts' default font as an OverlayFontKey; empty uses the bundled font.</summary>
  public string OverlayFont = string.Empty;

  public GameplayTweaksSettings Gameplay = new();
  public Effects.EffectSettings Effects = new();

  public const KeyCode DefaultSettingsShortcutKey = KeyCode.F9;
  public const KeyCode DefaultSettingsShortcutModifier = KeyCode.LeftControl;

  public KeyCode SettingsShortcutKey = DefaultSettingsShortcutKey;
  public KeyCode SettingsShortcutModifier = DefaultSettingsShortcutModifier;

  public List<OverlayLayoutSettings> OverlayLayouts = new();

  /// <summary>
  /// Every profile, the one in use included. The values above belong to the
  /// profile in use; the others keep theirs in <see cref="Profiles.ElementProfile.Content"/>.
  /// </summary>
  public List<Profiles.ElementProfile> Profiles = new();

  public string ActiveProfileId = "";

  public override void Save(UnityModManager.ModEntry modEntry)
  {
    Save(this, modEntry);
  }

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
      return Load<ElementSettings>(modEntry) ?? fresh;
    }
  }
}
