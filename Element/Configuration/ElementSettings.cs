using System;
using System.Collections.Generic;
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

  public const KeyCode DefaultSettingsShortcutKey = KeyCode.F9;
  public const KeyCode DefaultSettingsShortcutModifier = KeyCode.LeftControl;

  public KeyCode SettingsShortcutKey = DefaultSettingsShortcutKey;
  public KeyCode SettingsShortcutModifier = DefaultSettingsShortcutModifier;

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
    if (SettingsVersion < 3)
      Domain.Elements.OverlayElementMigration.Migrate(OverlayLayouts);
    // Left Alt + Tab was the old default and also switches windows on Windows.
    // Move only that untouched default; a custom shortcut stays as chosen.
    if (
      SettingsVersion < 4
      && SettingsShortcutKey == KeyCode.Tab
      && SettingsShortcutModifier == KeyCode.LeftAlt
    )
    {
      SettingsShortcutKey = DefaultSettingsShortcutKey;
      SettingsShortcutModifier = DefaultSettingsShortcutModifier;
    }
    SettingsVersion = Math.Max(SettingsVersion, 4);
  }

  public override void Save(UnityModManager.ModEntry modEntry)
  {
    Save(this, modEntry);
  }
}
