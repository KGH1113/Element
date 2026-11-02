using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace Element.Configuration;

public sealed class ElementSettings : UnityModManager.ModSettings
{
  public bool HidePerfectJudgmentText = true;

  public bool RecordMode;

  public bool NativeEditorTrackpad = true;

  public bool ShowDmNoteInGame = true;

  /// <summary>Open the bundled ElementNote with the game; it closes when the game quits.</summary>
  public bool AutoOpenCloseDmNote = true;

  public int SettingsVersion;
  public string HighlightColor = "#BA712B";
  public List<string> RecentHighlightColors = new();
  public bool ReduceUiMotion;
  public bool LayoutSnapping = true;

  public GameplayTweaksSettings Gameplay = new();
  public Effects.EffectSettings Effects = new();

  public KeyCode SettingsShortcutKey = KeyCode.Tab;
  public KeyCode SettingsShortcutModifier = KeyCode.LeftAlt;

  public List<OverlayLayoutSettings> OverlayLayouts = new();

  public void Migrate()
  {
    // Only replace the previous default. After the first migration users can
    // deliberately choose that green again without a later load changing it.
    if (
      SettingsVersion < 1
      && string.Equals(HighlightColor?.Trim().TrimStart('#'), "7CCE00", StringComparison.OrdinalIgnoreCase)
    )
      HighlightColor = "#BA712B";
    Gameplay ??= new();
    Gameplay.Judgement ??= new();
    if (SettingsVersion < 2)
    {
      Gameplay.Judgement.HidePerfect = HidePerfectJudgmentText;
    }
    Gameplay.Normalize();
    Effects ??= new();
    Effects.Normalize();
    OverlayLayouts ??= new();
    // Presets from before 1.1 become texts already in the current form; texts
    // saved by 1.1.0 are rewritten for split values and units.
    if (SettingsVersion < 3)
      Domain.Elements.OverlayElementMigration.Migrate(OverlayLayouts);
    else if (SettingsVersion < 4)
      Domain.Elements.OverlayElementMigration.MigrateFormulas(OverlayLayouts);
    // Built-ins added by an update, such as the progress bar, arrive once;
    // deleting one afterwards keeps it gone.
    if (SettingsVersion < 6)
    {
      Domain.Elements.OverlayElementMigration.UpgradeProgressBar(OverlayLayouts);
      Domain.Elements.OverlayElementDefaults.AddMissing(OverlayLayouts);
    }
    SettingsVersion = Math.Max(SettingsVersion, 6);
  }

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
